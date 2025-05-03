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

			// Flip y, convert to pixels, and stretch x2 horizontally.
			output.texcoord.y = 1.0 - output.texcoord.y;
			output.texcoord.x *= 2.0;

			return output;
		}


		float4 Frag( Varyings varyings ) : SV_Target
		{
			int eyeIndex = varyings.texcoord.x < 1.0 ? 0 : 1;
			varyings.texcoord.x -= eyeIndex;
			float3 color = SAMPLE_TEXTURE2D_ARRAY( _MainTex, sampler_LinearClamp, varyings.texcoord, eyeIndex ).rgb;

			color.g = saturate( color.g + eyeIndex * 0.1 );
			// FROM XRMIRRORVIEW.HLSL:
			// Convert the encoded output image into linear
			//color = InverseOETF( color, _SourceMaxNits, _SourceHDREncoding );
			// Now we need to convert the color space from source to destination;
			//color = mul((float3x3)_ColorTransform, color);
			// Convert the linear image into the correct encoded output for the display
			//color = OETF( color, _MaxNits );

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