
/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class StereoHackEnabler : MonoBehaviour
{
	[SerializeField] float _eyeSeparation = 0.064f;
	[SerializeField] Vector2Int _perEyeResolution = new Vector2Int( 1920, 1080 );
	[SerializeField] RenderTexture _targetSbsStereoTexture;

	// TEST
	//[SerializeField] float _maxNits = 1f;
	//[SerializeField] float _sourceMaxNits = 2.2f;

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


	void Awake()
	{
		// Force presence of XR scripting defines. Otherwise XR will be ignored in a range of methods. The XRSystem usually does this, but we are not using it.
		#if UNITY_EDITOR
		string[] scriptingDefineSymbols;
		UnityEditor.PlayerSettings.GetScriptingDefineSymbols( UnityEditor.Build.NamedBuildTarget.Standalone, out scriptingDefineSymbols );
		var scriptingDefineSymbolsList = new List<string>( scriptingDefineSymbols );
		if( !scriptingDefineSymbolsList.Contains( ENABLE_VR ) ) scriptingDefineSymbolsList.Add( ENABLE_VR );
		if( !scriptingDefineSymbolsList.Contains( ENABLE_XR_MODULE ) ) scriptingDefineSymbolsList.Add( ENABLE_XR_MODULE );
		if( scriptingDefineSymbolsList.Count != scriptingDefineSymbols.Length ) UnityEditor.PlayerSettings.SetScriptingDefineSymbols( UnityEditor.Build.NamedBuildTarget.Standalone, scriptingDefineSymbolsList.ToArray() );
		#endif
	}


	void OnEnable()
	{
		Debug.Log( "OnEnable" );

		if( !_targetSbsStereoTexture ) throw new Exception( "Target SBS stereo texture not set." );
		if( _targetSbsStereoTexture.graphicsFormat != _hdrpColorFormat ) throw new Exception( $"Target SBS stereo texture must be {_hdrpColorFormat}." );

		Debug.Log( "OnEnable2" );

		_camera = Camera.main;
		if( !_camera ) throw new Exception( "Main camera not found." );

		Debug.Log( "OnEnable3" );

		_offAxisCamera = _camera.GetComponent<OffAxisCamera>();
		if( !_offAxisCamera ) throw new Exception( "OffAxisCamera component not found on main camera." );

		Debug.Log( "OnEnable4" );

		_cmd = new CommandBuffer();
		_cameraRenderTargetId = new RenderTargetIdentifier( BuiltinRenderTextureType.CameraTarget );

		_blitMaterial = new Material( Shader.Find( "Hidden/StereoHackSbsBlit" ));
		_blitMaterial.hideFlags = HideFlags.HideAndDontSave;

		_cameraStereoTextureArray = CreateTexArray( _perEyeResolution, _hdrpColorFormat, "StereoHackCameraTextureArray" );
		_cameraStereoMotionVectorTextureArray = CreateTexArray( _perEyeResolution, _hdrpMotionVectorFormat, "StereoHackCameraMotionVectorTextureArray" );
		
		// If you just sample the color texture it comes out too dark, and adjusting gamma (pow(1/2.2)) makes
		// it look washed out. Instead we try to use the same color space conversion as the original XRMirrorView.
		// However, the _MaxNits and _SourceMaxNits values are just made up to visually match the original XRMirrorView output and
		// so something is still off.
		int sourceHdrEncoding;
		HDROutputUtils.GetColorEncodingForGamut( ColorGamut.sRGB, out sourceHdrEncoding);
		_blitMaterial.SetInteger( "_SourceHDREncoding", sourceHdrEncoding );
		_blitMaterial.SetFloat( "_MaxNits", 1f ); // Same as in XRPass.
		_blitMaterial.SetFloat( "_SourceMaxNits", 1.8f ); // This mysterious values was found by visually comparing to the original Mirror View output.

		// ALTERNATIVE: Use CustomPass to do the xr->sbs blit.
		//var customPass = gameObject.AddComponent<CustomPassVolume>();
		//customPass.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
		//var stereoPass = customPass.AddPassOfType<StereoHackCustomPass>();
		//stereoPass.name = "StereoHackCustomPass";
		//stereoPass.targetColorBuffer = CustomPass.TargetBuffer.None;
		//stereoPass.targetDepthBuffer = CustomPass.TargetBuffer.None;

		Debug.Log( "_targetSbsStereoTexture: " + ( _targetSbsStereoTexture == null ? "null" : _targetSbsStereoTexture.name ) );
		Debug.Log( "_cameraStereoTextureArray: " + _cameraStereoTextureArray == null ? "null" : _cameraStereoTextureArray.name );
		Debug.Log( "_cameraStereoMotionVectorTextureArray: " + _cameraStereoMotionVectorTextureArray == null ? "null" : _cameraStereoMotionVectorTextureArray.name );

		RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

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
		//Debug.Log( "_targetSbsStereoTexture: " + ( _targetSbsStereoTexture == null ? "null" : _targetSbsStereoTexture.name ) );
		//Debug.Log( "_cameraStereoTextureArray: " + _cameraStereoTextureArray == null ? "null" : _cameraStereoTextureArray.name );
		//Debug.Log( "_cameraStereoMotionVectorTextureArray: " + _cameraStereoMotionVectorTextureArray == null ? "null" : _cameraStereoMotionVectorTextureArray.name );
//
		//if( !_cameraStereoTextureArray || !_cameraStereoTextureArray.IsCreated() ) _cameraStereoTextureArray = CreateTexArray( _perEyeResolution, _hdrpColorFormat, "StereoHackCameraTextureArray" );
		//if( !_cameraStereoMotionVectorTextureArray || !_cameraStereoMotionVectorTextureArray.IsCreated() ) _cameraStereoMotionVectorTextureArray = CreateTexArray( _perEyeResolution, _hdrpMotionVectorFormat, "StereoHackCameraMotionVectorTextureArray" );
		//if( !_cameraStereoTextureArray || !_cameraStereoMotionVectorTextureArray ) return null;

		TextureXR.maxViews = 2;
		TextureXR.GetBlackTextureArray();

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

		// TEST
		//_blitMaterial.SetFloat( "_MaxNits", _maxNits );
		//_blitMaterial.SetFloat( "_SourceMaxNits", _sourceMaxNits );

		// Render single pass stereo render texture array to SBS stereo texture.
		_cmd.Blit( _cameraStereoTextureArray, _targetSbsStereoTexture, _blitMaterial, 0 );

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