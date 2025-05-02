/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class StereoHackCustomPass : CustomPass
{
	Material _material;


	protected override void Setup( ScriptableRenderContext ctx, CommandBuffer cmd )
	{
		var shader = Shader.Find( "Hidden/StereHackCustomPass" );
		if( !shader ) throw new System.Exception( "Shader not found: Hidden/StereHackCustomPass" );

		// WTF Unity. What the hell is wrong with new Material(), fucking API!!
		_material = CoreUtils.CreateEngineMaterial( shader );
	}


	protected override void Execute( CustomPassContext ctx )
	{
		if( !Application.isPlaying || !_material ) return;

	
		if( StereoHackEnabler.instance )
		{
			_material.SetTexture( "_StereoTex", StereoHackEnabler.instance.cameraStereoTextureArray );
			_material.SetVector( "_SourceSize", new Vector2( StereoHackEnabler.instance.cameraStereoTextureArray.width, StereoHackEnabler.instance.cameraStereoTextureArray.height ) );
			ctx.cmd.Blit( null, StereoHackEnabler.instance.targetSbsStereoTexture, _material );

			//ctx.cmd.Blit( null, new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget), _material );

			//ctx.cmd.Blit( null, _stereoHackEnabler.targetSbsStereoTexture, _material, pass: 0, destDepthSlice: 0 );
			StereoHackEnabler.instance.targetSbsStereoTexture.IncrementUpdateCount();
			
			// THIS WORKS TOO.
			//CoreUtils.SetRenderTarget( ctx.cmd, _stereoHackEnabler.targetSbsStereoTexture, ClearFlag.None );
			//CoreUtils.DrawFullScreen( ctx.cmd, _material, ctx.propertyBlock, shaderPassId: 0 );

			//var scale = RTHandles.rtHandleProperties.rtHandleScale;
			//ctx.cmd.Blit( ctx.cameraColorBuffer, _stereoHackEnabler.targetSbsStereoTexture, scale, Vector2.zero, 0, 0 );
		}
	}


	protected override void Cleanup() {

		// WTF Unity. Why not Object.DestroyImmediate( _blitMaterial ).
		CoreUtils.Destroy( _material );
	}
}