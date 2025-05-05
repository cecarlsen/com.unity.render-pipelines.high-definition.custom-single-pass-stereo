/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk
*/

Shader "Hidden/StereHackCustomPass"
{

	HLSLINCLUDE

		#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

		TEXTURE2D_ARRAY( _StereoTex );
		float2 _SourceSize;

		float4 Frag( Varyings varyings ) : SV_Target
		{
			// Flip y.
			varyings.positionCS.y = _SourceSize.y - varyings.positionCS.y;

			int eyeIndex = varyings.positionCS.x < _SourceSize.x ? 0 : 1;
			varyings.positionCS.x -= _SourceSize.x * eyeIndex;

			// Found "_AfterPostProcessColorBuffer" by looking at the LoadCameraColor source from CustomPassCommon.hlsl.
			float3 color = LOAD_TEXTURE2D_ARRAY_LOD( _StereoTex, varyings.positionCS.xy, eyeIndex, 0 ).rgb; // 0 mip

			return float4( color, 1 );
		}

	ENDHLSL


	SubShader
	{
		Pass
		{
			Cull Off
			ZWrite Off
			ZTest Always

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			ENDHLSL
		}
	}
}