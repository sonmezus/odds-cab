Shader "OddsCab/Ray Contact Composite"
{
    Properties { _Strength("Strength", Range(0,1)) = .85 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend DstColor Zero
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_RayContactTexture); SAMPLER(sampler_RayContactTexture);
            TEXTURE2D(_FullDepth); SAMPLER(sampler_FullDepth);
            CBUFFER_START(UnityPerMaterial)
                float _Strength;
            CBUFFER_END
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(uint id:SV_VertexID)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(id);
                o.uv = GetFullScreenTriangleTexCoord(id);
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 ray = SAMPLE_TEXTURE2D(_RayContactTexture, sampler_RayContactTexture, input.uv).rg;
                float depth = SAMPLE_TEXTURE2D(_FullDepth, sampler_FullDepth, input.uv).r;
                float edge = saturate(1 - abs(depth - ray.y) * 1500);
                half shade = lerp(1, ray.x, _Strength * edge);
                return half4(shade, shade, shade, 1);
            }
            ENDHLSL
        }
    }
}
