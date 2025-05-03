/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk
*/

Shader "Hidden/StereoHackSbsBlit"
{
	Properties {
		[NoScaleOffset] _MainTex ("Just here to make Blit happy", 2D) = "white" {}
	}

	HLSLINCLUDE

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/HDROutput.hlsl"

		#pragma multi_compile_local __ _IS_EDITOR

		TEXTURE2D_ARRAY( _MainTex );
		SamplerState sampler_LinearClamp;

		float _MaxNits;
		float _SourceMaxNits;
		int _SourceHDREncoding;


		struct Attributes
		{
			uint vertexID : SV_VertexID;
		};

		struct Varyings
		{
			float4 positionCS : SV_POSITION;
			float2 texcoord   : TEXCOORD0;
		};


		Varyings Vert( Attributes input )
		{
			Varyings output;

			output.positionCS = GetFullScreenTriangleVertexPosition( input.vertexID );
			output.texcoord   = GetFullScreenTriangleTexCoord( input.vertexID );

			// Flip y. For some reason the y coordinate it not flipped in builds. TODO: find out why.
			#ifdef _IS_EDITOR
				output.texcoord.y = 1.0 - output.texcoord.y;
			#endif

			// Stretch x2 horizontally to fit the two eyes.
			output.texcoord.x *= 2.0;

			return output;
		}


		float4 Frag( Varyings varyings ) : SV_Target
		{
			int eyeIndex = varyings.texcoord.x < 1.0 ? 0 : 1;
			varyings.texcoord.x -= eyeIndex;
			float3 color = SAMPLE_TEXTURE2D_ARRAY( _MainTex, sampler_LinearClamp, varyings.texcoord, eyeIndex ).rgb;

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