Shader "OddsCab/Reference Shoreline"
{
    Properties
    {
        [MainTexture] _BaseMap ("Istanbul Shoreline RGBA", 2D) = "white" {}
        _Tint ("Weather Tint", Color) = (1, 1, 1, 1)
        _Atmosphere ("Atmosphere RGB and Haze Amount", Color) = (0.69, 0.79, 0.87, 0.24)
        _Exposure ("Exposure", Range(0,2)) = 1
        _Saturation ("Saturation", Range(0,1.5)) = 0.92
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.4
        [Toggle] _UseVertexColor ("Use Batched Skyline Colors", Float) = 0
        [Toggle] _VertexColorsLinear ("Imported glTF Linear Vertex Colors", Float) = 0
        _EdgeFeather ("Matte Shoulder Coverage Width", Range(0,0.2)) = 0
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth Write", Float) = 1
        [HideInInspector] _AlphaToMask ("Alpha To Coverage", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ReferenceShoreline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            AlphaToMask [_AlphaToMask]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint, _Atmosphere;
                half _Exposure, _Saturation, _Cutoff, _UseVertexColor, _VertexColorsLinear, _EdgeFeather;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                // Mesh colors are authored in display RGB; unlike an sRGB texture they are
                // not decoded by the sampler. Match the matte's working color space.
                half3 vertexColor = input.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    vertexColor = lerp(SRGBToLinear(vertexColor), vertexColor, _VertexColorsLinear);
                #endif
                output.color = lerp(half3(1, 1, 1), vertexColor, _UseVertexColor);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(source.a - _Cutoff);
                if (_EdgeFeather > 0.001h)
                {
                    half coverage = smoothstep(0.0h, _EdgeFeather, min(input.uv.x, 1.0h - input.uv.x));
                    source.a *= coverage;
                }
                source.rgb *= input.color;
                half luminance = dot(source.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 color = lerp(luminance.xxx, source.rgb, _Saturation) * _Tint.rgb * _Exposure;
                // This is a far shore, beyond the street's fog range. An explicit weather haze keeps
                // the matte legible in both the PC vista stack and the single-camera mobile path.
                color = lerp(color, _Atmosphere.rgb, saturate(_Atmosphere.a));
                return half4(color, source.a);
            }
            ENDHLSL
        }
    }
}
