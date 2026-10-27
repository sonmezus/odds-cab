Shader "OddsCab/Reference Cloud"
{
    Properties
    {
        [MainTexture] _BaseMap ("Cloud Density", 2D) = "white" {}
        _TopColor ("Sunlit Top", Color) = (1, 0.98, 0.94, 1)
        _BottomColor ("Sky Bounce", Color) = (0.62, 0.70, 0.80, 1)
        _Opacity ("Opacity", Range(0,1)) = 0.7
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-100" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _TopColor, _BottomColor;
                half _Opacity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 density = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 color = lerp(_BottomColor.rgb, _TopColor.rgb, saturate(density.r));
                // Sky cards have their own atmospheric palette; street fog must not flatten their lighting.
                return half4(color, density.a * _Opacity);
            }
            ENDHLSL
        }
    }
}
