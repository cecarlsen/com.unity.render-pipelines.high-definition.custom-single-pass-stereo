
/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class StereoHackEnabler : MonoBehaviour
{
	[SerializeField] float _eyeSeparation = 0.064f;
	[SerializeField] Vector2Int _perEyeResolution = new Vector2Int( 1920, 1080 );
	[SerializeField] RenderTexture _targetSbsStereoTexture;
	[SerializeField,Tooltip("Just for testing. We have issues with y-flipping in main display.")] bool _testBlitSbsInCustomPass = false;

	Camera _camera;
	OffAxisCamera _offAxisCamera;
	Matrix4x4 _prevViewLeft, _prevViewRight;
	bool _hasPrevView = false;

	RenderTexture _cameraStereoTextureArray; 				// This texture is normally provided by the XR System.
	RenderTexture _cameraStereoMotionVectorTextureArray;	// Same as above.

	CommandBuffer _cmd;
	Material _blitMaterial;

	RenderTargetIdentifier _cameraRenderTargetId;

	static StereoHackEnabler _instance;

	const string ENABLE_VR = nameof( ENABLE_VR );
	const string ENABLE_XR_MODULE = nameof( ENABLE_XR_MODULE );
	const GraphicsFormat _hdrpColorFormat = GraphicsFormat.B10G11R11_UFloatPack32; // HDRP default color format.
	const GraphicsFormat _hdrpMotionVectorFormat = GraphicsFormat.R32_SFloat; // HDRP default motion vector format.


	public static StereoHackEnabler instance => _instance;

	public RenderTexture cameraStereoTextureArray => _cameraStereoTextureArray;
	public RenderTexture targetSbsStereoTexture => _targetSbsStereoTexture;


	#if UNITY_EDITOR
	 [UnityEditor.InitializeOnLoadMethod]
	static void EnsureScriptingDefines()
	{
		// Force presence of XR scripting defines. Otherwise XR will be ignored in a range of methods. The XRSystem usually does this, but we are not using it.
		string[] scriptingDefineSymbols;
		UnityEditor.PlayerSettings.GetScriptingDefineSymbols( UnityEditor.Build.NamedBuildTarget.Standalone, out scriptingDefineSymbols );
		var scriptingDefineSymbolsList = new List<string>( scriptingDefineSymbols );
		if( !scriptingDefineSymbolsList.Contains( ENABLE_VR ) ) scriptingDefineSymbolsList.Add( ENABLE_VR );
		if( !scriptingDefineSymbolsList.Contains( ENABLE_XR_MODULE ) ) scriptingDefineSymbolsList.Add( ENABLE_XR_MODULE );
		if( scriptingDefineSymbolsList.Count != scriptingDefineSymbols.Length ) UnityEditor.PlayerSettings.SetScriptingDefineSymbols( UnityEditor.Build.NamedBuildTarget.Standalone, scriptingDefineSymbolsList.ToArray() );
	}
	#endif


	void OnEnable()
	{
		if( !_targetSbsStereoTexture ) throw new Exception( "Target SBS stereo texture not set." );
		if( _targetSbsStereoTexture.graphicsFormat != _hdrpColorFormat ) throw new Exception( $"Target SBS stereo texture must be {_hdrpColorFormat}." );

		_camera = Camera.main;
		if( !_camera ) throw new Exception( "Main camera not found." );

		_offAxisCamera = _camera.GetComponent<OffAxisCamera>();
		if( !_offAxisCamera ) throw new Exception( "OffAxisCamera component not found on main camera." );

		_cmd = new CommandBuffer();
		_cameraRenderTargetId = new RenderTargetIdentifier( BuiltinRenderTextureType.CameraTarget );

		Shader shader = Shader.Find( "Hidden/StereoHackSbsBlit" );
		if( !shader ) throw new Exception( "Shader 'Hidden/StereoHackSbsBlit' not found." );
		_blitMaterial = new Material( shader );
		_blitMaterial.hideFlags = HideFlags.HideAndDontSave;
		if( Application.isEditor ) _blitMaterial.EnableKeyword( "_IS_EDITOR" ); // Quick workaround for flipped texture, only in editor.

		_cameraStereoTextureArray = CreateTexArray( _perEyeResolution, _hdrpColorFormat, "StereoHackCameraTextureArray" );
		_cameraStereoMotionVectorTextureArray = CreateTexArray( _perEyeResolution, _hdrpMotionVectorFormat, "StereoHackCameraMotionVectorTextureArray" );

		RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

		// ALTERNATIVE: Use CustomPass to do the xr->sbs blit.
		if( _testBlitSbsInCustomPass ){
			var customPass = gameObject.AddComponent<CustomPassVolume>();
			customPass.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
			var stereoPass = customPass.AddPassOfType<StereoHackCustomPass>();
			stereoPass.name = "StereoHackCustomPass";
			stereoPass.targetColorBuffer = CustomPass.TargetBuffer.None;
			stereoPass.targetDepthBuffer = CustomPass.TargetBuffer.None;
		}

		_instance = this;
	 }


	void OnDisable()
	{
		RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

		_cameraStereoTextureArray?.Release();
		_cameraStereoMotionVectorTextureArray?.Release();
		_cmd.Dispose();
	}


	public XRPass CreateXRPass()
	{
		TextureXR.maxViews = 2;
		//TextureXR.GetBlackTextureArray();

		ScriptableCullingParameters cullingParams;
		_camera.TryGetCullingParameters( out cullingParams );

		var createInfo = new XRPassCreateInfo()
		{
			renderTarget = new RenderTargetIdentifier( _cameraStereoTextureArray ),
			renderTargetDesc = _cameraStereoTextureArray.descriptor,
			motionVectorRenderTarget = new RenderTargetIdentifier( _cameraStereoMotionVectorTextureArray ),
			motionVectorRenderTargetDesc = _cameraStereoMotionVectorTextureArray.descriptor,
			cullingParameters = cullingParams,
			occlusionMeshMaterial = null,
			occlusionMeshScale = 1f,
			renderTargetScaledWidth = _perEyeResolution.x,
			renderTargetScaledHeight = _perEyeResolution.y,
			foveatedRenderingInfo = IntPtr.Zero,
			multipassId = 0,
			cullingPassId = -1,
			copyDepth = false,
			hasMotionVectorPass = true
		};

		var xr = XRPass.CreateDefault( createInfo );
		xr.AddView( new XRView() );
		xr.AddView( new XRView() );

		// Compute off-axis views.
		var windowTransform = _offAxisCamera.windowTransform;
		var windowSize = new Vector2( windowTransform.lossyScale.x, windowTransform.lossyScale.y );
		var windowPosition = windowTransform.position;
		var windowRotation = windowTransform.rotation;
		float eyeSeperationExtents = _eyeSeparation * 0.5f;
		var positionLeft = _camera.transform.position - _camera.transform.right * eyeSeperationExtents;
		var positionRight = _camera.transform.position + _camera.transform.right * eyeSeperationExtents;
		Matrix4x4 viewLeft = Matrix4x4.identity, viewRight = Matrix4x4.identity, projectionLeft = Matrix4x4.identity, projectionRight = Matrix4x4.identity;
		OffAxisUtils.ComputeOffAxisCameraMatrices( positionLeft, windowPosition, windowRotation, windowSize, _camera.nearClipPlane, _camera.farClipPlane, ref viewLeft, ref projectionLeft );
		OffAxisUtils.ComputeOffAxisCameraMatrices( positionRight, windowPosition, windowRotation, windowSize, _camera.nearClipPlane, _camera.farClipPlane, ref viewRight, ref projectionRight );
		Rect viewport = new Rect( 0, 0, _perEyeResolution.x, _perEyeResolution.y );

		// Presuming we've modified SRP Core.
		var xrViewLeft = new XRView( projectionLeft, viewLeft, _prevViewLeft, _hasPrevView, viewport, null, textureArraySlice: 0 );
		var xrViewRight = new XRView( projectionRight, viewRight, _prevViewRight, _hasPrevView, viewport, null, textureArraySlice: 1 );
		xr.AssignView( 0, xrViewLeft );
		xr.AssignView( 1, xrViewRight );

		// Prepare next frame.
		_prevViewLeft = viewLeft;
		_prevViewRight = viewRight;
		_hasPrevView = true;

		return xr;
	}


	void OnEndCameraRendering( ScriptableRenderContext ctx, Camera camera )
	{
		if( camera.cameraType != CameraType.Game ) return;

		// Render single pass stereo render texture array to SBS stereo texture.
		if( !_testBlitSbsInCustomPass ) _cmd.Blit( _cameraStereoTextureArray, _targetSbsStereoTexture, _blitMaterial, 0 );

		// Draw UI on top of everything.
		var uiRenderlist = ctx.CreateUIOverlayRendererList( camera, UISubset.UIToolkit_UGUI );
		_cmd.SetRenderTarget( _cameraRenderTargetId );
		_cmd.ClearRenderTarget( true, true, Color.black );
		_cmd.DrawRendererList( uiRenderlist );

		// Execute.
		Graphics.ExecuteCommandBuffer( _cmd );
		_cmd.Clear();
	}


	static RenderTexture CreateTexArray( Vector2Int resolution, GraphicsFormat format, string name )
	{
		var tex = new RenderTexture( resolution.x, resolution.y, 24, format );
		tex.dimension = TextureDimension.Tex2DArray;
		tex.volumeDepth = 2;
		tex.name = name;
		tex.Create();
		return tex;
	}
}