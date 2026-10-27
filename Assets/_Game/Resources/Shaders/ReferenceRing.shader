Shader "OddsCab/Reference Ring"
{
    Properties { [MainColor][HDR] _BaseColor ("Glow", Color) = (0.12, 1.3, 0.95, 0.5) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float radial : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.radial = input.uv.y;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half feather = saturate(sin(saturate(input.radial) * PI));
                return half4(_BaseColor.rgb, feather * feather * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
