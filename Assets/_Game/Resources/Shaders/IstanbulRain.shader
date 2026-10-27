Shader "OddsCab/Istanbul Rain"
{
    Properties
    {
        _Tint ("Rain Light", Color) = (0.72,0.83,0.89,0.45)
        _Splash ("Ground Impacts", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Splash;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; half fog:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.uv=v.uv;o.color=v.color;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2.0-1.0;
                half streak=(1.0-smoothstep(0.1,0.95,abs(p.x)))*(1.0-smoothstep(0.35,1.0,abs(p.y)));
                half ring=1.0-smoothstep(0.07,0.20,abs(length(p)-0.66));
                half opacity=lerp(streak,ring,_Splash)*i.color.a*_Tint.a;
                half3 color=MixFog(i.color.rgb*_Tint.rgb,i.fog);
                return half4(color,opacity);
            }
            ENDHLSL
        }
    }
}
