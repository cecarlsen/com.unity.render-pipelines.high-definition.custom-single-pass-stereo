
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
using UnityEngine.UI;

public class StereoHackEnabler : MonoBehaviour
{
	[SerializeField] float _eyeSeparation = 0.064f;
	[SerializeField] bool _force2D = false;
	[SerializeField] bool _swapEyes = false;
	[SerializeField] RenderTexture _targetSbsStereoTexture;
	[SerializeField,Tooltip("Just for testing. We have issues with y-flipping in main display.")] bool _testBlitSbsInCustomPass = false;
	[SerializeField] Toggle _swapEyesToggle;


	Camera _camera;
	OffAxisCamera _offAxisCamera;
	Matrix4x4 _prevViewLeft, _prevViewRight;
	bool _hasPrevView = false;

	RenderTexture _cameraStereoTextureArray; 				// This texture is normally provided by the XR System.
	RenderTexture _cameraStereoMotionVectorTextureArray;	// Same as above.

	CommandBuffer _cmd;
	Material _blitMaterial;

	RenderTargetIdentifier _cameraRenderTargetId;

	Vector2Int _perEyeResolution;

	static StereoHackEnabler _instance;

	const string ENABLE_VR = nameof( ENABLE_VR );
	const string ENABLE_XR_MODULE = nameof( ENABLE_XR_MODULE );

	const GraphicsFormat hdrpColorFormat = GraphicsFormat.B10G11R11_UFloatPack32;// GraphicsFormat.B10G11R11_UFloatPack32;// GraphicsFormat.R8G8B8A8_SRGB; // HDRP default color format. MockHMD uses R8G8B8A8_SRGB.
	const GraphicsFormat hdrpDepthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt; // HDRP default depth stencil format.
	const int hdrpDepthBufferBits = 32;
	const GraphicsFormat hdrpMotionVectorFormat = GraphicsFormat.R8G8B8A8_UNorm;// GraphicsFormat.R16G16_SFloat; // HDRP default motion vector format.
	const GraphicsFormat hdrpMotionVectorDepthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt;// GraphicsFormat.None; // HDRP default motion vector depth stencil format.
	const int hdrpMotionVectorDepthBufferBits = 32;

	public bool force2D {
		get => _force2D;
		set { _force2D = value; }
	}

	public bool swapEyes {
		get => _swapEyes;
		set { _swapEyes = value; }
	}

	public float eyeSeparation {
		get => _eyeSeparation;
		set { _eyeSeparation = value; }
	}

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
		// Checks.
		if( !_targetSbsStereoTexture ) throw new Exception( "Target SBS stereo texture not set." );
		if( _targetSbsStereoTexture.graphicsFormat != hdrpColorFormat ) throw new Exception( $"Target SBS stereo texture must be {hdrpColorFormat}." );

		// Get resolution.
		_perEyeResolution = new Vector2Int( _targetSbsStereoTexture.width/2, _targetSbsStereoTexture.height );

		// Get components.
		_camera = Camera.main;
		if( !_camera ) throw new Exception( "Main camera not found." );
		_offAxisCamera = _camera.GetComponent<OffAxisCamera>();
		if( !_offAxisCamera ) throw new Exception( "OffAxisCamera component not found on main camera." );
		if( !_offAxisCamera.enabled ) _offAxisCamera.enabled = true; // Ensure it is enabled.

		// Create resources.
		_cmd = new CommandBuffer();
		_cmd.name = "StereoHack CopySliceToSbsStereoTexture";
		_cameraRenderTargetId = new RenderTargetIdentifier( BuiltinRenderTextureType.CameraTarget );

		Shader shader = Shader.Find( "Hidden/StereoHackSbsBlit" );
		if( !shader ) throw new Exception( "Shader 'Hidden/StereoHackSbsBlit' not found." );
		_blitMaterial = new Material( shader );
		_blitMaterial.hideFlags = HideFlags.HideAndDontSave;
		//if( Application.isEditor ) _blitMaterial.EnableKeyword( "_IS_EDITOR" ); // Quick workaround for flipped texture, only in editor.

		_cameraStereoTextureArray = CreateTexArray(
			_perEyeResolution, hdrpColorFormat, hdrpDepthStencilFormat, hdrpDepthBufferBits, VRTextureUsage.TwoEyes, "StereoHackCameraTextureArray"
		);
		_cameraStereoMotionVectorTextureArray = CreateTexArray(
			_perEyeResolution, hdrpMotionVectorFormat, hdrpMotionVectorDepthStencilFormat, hdrpMotionVectorDepthBufferBits, VRTextureUsage.None, "StereoHackCameraMotionVectorTextureArray"
		);

		// Subscribe to events.
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

		// Done.
		_instance = this;
	 }


	void OnDisable()
	{
		RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

		_cameraStereoTextureArray?.Release();
		_cameraStereoMotionVectorTextureArray?.Release();
		if( _cmd != null ) _cmd.Dispose();

		_cameraStereoTextureArray = null;
		_cameraStereoMotionVectorTextureArray = null;
		_cmd = null;
	}


	void Start()
	{
		// Set swap eyes state on toggle.
		if( _swapEyesToggle )
		{
			_swapEyesToggle.SetIsOnWithoutNotify( _swapEyes );
			_swapEyesToggle.onValueChanged.AddListener( isOn => _swapEyes = isOn );
		}
	}


	// Called from HDRenderPipeline.cs
	public XRPass CreateXRPass()
	{
		TextureXR.maxViews = 2;
		//TextureXR.GetBlackTextureArray();

		ScriptableCullingParameters cullingParams;
		//_camera.TryGetCullingParameters( stereoAware: true, out cullingParams );
		_camera.TryGetCullingParameters( out cullingParams ); // Default is stereoAware: false. Not sure what difference it makes.

		// Necessary? This just forward the values to the native XR device.
		//XRSystem.SetDisplayZRange( _camera.nearClipPlane, _camera.farClipPlane );

		// Disable legacy stereo culling path. As in XRPass.AssignCullingParams().
		cullingParams.cullingOptions &= ~CullingOptions.Stereo;

		// Use mono camera view projection for culling. Otherwise we will see artifacts in volumetric fog and perhaps other effects.
		cullingParams.stereoViewMatrix = _camera.worldToCameraMatrix;
		cullingParams.stereoProjectionMatrix = _camera.projectionMatrix;
		cullingParams.stereoSeparationDistance = _force2D ? 0f : _eyeSeparation;

		var createInfo = new XRPassCreateInfo()
		{
			renderTarget = new RenderTargetIdentifier( _cameraStereoTextureArray ),
			renderTargetDesc = _cameraStereoTextureArray.descriptor,
			motionVectorRenderTarget = new RenderTargetIdentifier( _cameraStereoMotionVectorTextureArray ),
			motionVectorRenderTargetDesc = _cameraStereoMotionVectorTextureArray.descriptor,
			cullingParameters = cullingParams,
			occlusionMeshMaterial = null, // Used to crop edges of head set view.
			occlusionMeshScale = 1f,
			renderTargetScaledWidth = _perEyeResolution.x,
			renderTargetScaledHeight = _perEyeResolution.y,
			foveatedRenderingInfo = IntPtr.Zero,
			multipassId = 0,
			cullingPassId = 0, // -1
			copyDepth = true, // This is true for vanilla MockHMD and it will trigger a DepthCopy in HDRenderPipeline.RenderGraph.cs. on line 391.
			hasMotionVectorPass = true
		};

		var xrPass = XRPass.CreateDefault( createInfo ); // Apparently it's not our job to release the pass. If we do in OnDisable it will cause errors.
		xrPass.AddView( new XRView() );
		xrPass.AddView( new XRView() );

		// Compute off-axis views.
		var windowTransform = _offAxisCamera.windowTransform;
		var windowSize = new Vector2( windowTransform.lossyScale.x, windowTransform.lossyScale.y );
		var windowPosition = windowTransform.position;
		var windowRotation = windowTransform.rotation;
		float eyeSeperationExtents = _force2D ? 0f : _eyeSeparation * 0.5f;
		float eyeSign = _swapEyes ? -1f : 1f; // Swap eyes if requested.
		var positionLeft = _camera.transform.position + ( _camera.transform.right * eyeSeperationExtents * -eyeSign );
		var positionRight = _camera.transform.position + ( _camera.transform.right * eyeSeperationExtents * eyeSign );
		Matrix4x4 viewLeft = Matrix4x4.identity, viewRight = Matrix4x4.identity, projectionLeft = Matrix4x4.identity, projectionRight = Matrix4x4.identity;
		OffAxisUtils.ComputeOffAxisCameraMatrices( positionLeft, windowPosition, windowRotation, windowSize, _camera.nearClipPlane, _camera.farClipPlane, ref viewLeft, ref projectionLeft );
		OffAxisUtils.ComputeOffAxisCameraMatrices( positionRight, windowPosition, windowRotation, windowSize, _camera.nearClipPlane, _camera.farClipPlane, ref viewRight, ref projectionRight );
		Rect viewport = new Rect( 0, 0, _perEyeResolution.x, _perEyeResolution.y );

		// Presuming we've modified SRP Core.
		var xrViewLeft = new XRView( projectionLeft, viewLeft, _prevViewLeft, _hasPrevView, viewport, occlusionMesh: null, textureArraySlice: 0 );
		var xrViewRight = new XRView( projectionRight, viewRight, _prevViewRight, _hasPrevView, viewport, occlusionMesh: null, textureArraySlice: 1 );
		xrPass.AssignView( 0, xrViewLeft );
		xrPass.AssignView( 1, xrViewRight );

		// Prepare next frame.
		_prevViewLeft = viewLeft;
		_prevViewRight = viewRight;
		_hasPrevView = true;

		return xrPass;
	}


	void OnEndCameraRendering( ScriptableRenderContext ctx, Camera camera )
	{
		if( camera.cameraType != CameraType.Game ) return;

		//var hdCam = HDCamera.GetOrCreate( camera );
		//hdCam.m_AdditionalCameraData.flipYMode = HDAdditionalCameraData.FlipYMode.Automatic;

		// Render single pass stereo render texture array to SBS stereo texture.
		if( !_testBlitSbsInCustomPass ) _cmd.Blit( _cameraStereoTextureArray, _targetSbsStereoTexture, _blitMaterial, 0 );

		// Draw UI on top of the HDRP camera display target.
		var uiRenderlist = ctx.CreateUIOverlayRendererList( camera, UISubset.UIToolkit_UGUI );
		_cmd.SetRenderTarget( _cameraRenderTargetId );
		_cmd.ClearRenderTarget( true, true, Color.black );
		_cmd.DrawRendererList( uiRenderlist );

		// Execute.
		Graphics.ExecuteCommandBuffer( _cmd );

		_cmd.Clear();
	}


	static RenderTexture CreateTexArray( Vector2Int resolution, GraphicsFormat colorFormat, GraphicsFormat depthStencilFormat, int depthBufferBits, VRTextureUsage vrUsage, string name )
	{
		var tex = new RenderTexture( resolution.x, resolution.y, colorFormat, depthStencilFormat );
		tex.dimension = TextureDimension.Tex2DArray;
		tex.volumeDepth = 2;
		tex.name = name;
		tex.depth = depthBufferBits;
		tex.vrUsage = vrUsage;
		tex.Create();
		return tex;
	}
}