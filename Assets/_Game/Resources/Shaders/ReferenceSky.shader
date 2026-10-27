Shader "OddsCab/Reference Sky"
{
    Properties
    {
        _SkyTint ("Low Azure Sky", Color) = (0.23, 0.56, 0.78, 1)
        _ZenithColor ("High Blue Sky", Color) = (0.12, 0.36, 0.66, 1)
        _HorizonColor ("Horizon Haze", Color) = (0.97, 0.74, 0.62, 1)
        _GroundColor ("Below Horizon", Color) = (0.32, 0.38, 0.42, 1)
        _Exposure ("Exposure", Range(0,2)) = 1
        _HorizonFalloff ("Horizon Band Falloff", Range(2,32)) = 14
        _SunDirection ("Sun World Direction", Vector) = (0.5,0.4,-0.7,0)
        [HDR] _SunColor ("Sun Disc", Color) = (3,2.4,1.4,1)
        _CloudCoverage ("High Cloud Veil", Range(0,1)) = 0.32
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ReferenceSky"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _SkyTint, _ZenithColor, _HorizonColor, _GroundColor;
                half _Exposure, _HorizonFalloff;
                float4 _SunDirection;
                half4 _SunColor;
                half _CloudCoverage;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 directionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.directionWS = TransformObjectToWorldDir(input.positionOS.xyz);
                return output;
            }
            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),lerp(Hash(cell+float2(0,1)),Hash(cell+1.0),f.x),f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.directionWS);
                float elevation = direction.y;
                half3 sky = lerp(_SkyTint.rgb, _ZenithColor.rgb, smoothstep(0.15, 0.8, elevation));
                half horizonWeight = exp2(-max(elevation, 0.0) * _HorizonFalloff);
                half3 above = lerp(sky, _HorizonColor.rgb, horizonWeight);
                half3 below = lerp(_GroundColor.rgb, _HorizonColor.rgb, exp2(min(elevation, 0.0) * 18.0));
                // Sparse high cloud complements the photographed cumulus without a volumetric ray march.
                float2 cloudUV = direction.xz / max(0.14, direction.y + 0.18);
                cloudUV = cloudUV * float2(2.4, 7.0) + float2(_Time.y * 0.002, 0.0);
                float veil = Noise(cloudUV) * 0.58 + Noise(cloudUV * 2.03 + 17.3) * 0.28 + Noise(cloudUV * 4.07 - 8.1) * 0.14;
                half cloud = smoothstep(0.47, 0.78, veil) * _CloudCoverage * smoothstep(0.015, 0.16, elevation);
                above = lerp(above, lerp(_HorizonColor.rgb, half3(0.92,0.96,1.0),0.72), cloud);
                float sun = saturate(dot(direction, normalize(_SunDirection.xyz)));
                float angularWidth = max(fwidth(sun), 0.000018);
                half disc = smoothstep(0.99990-angularWidth, 0.99990+angularWidth, sun);
                half halo = pow(sun, 96.0) * 0.14 + pow(sun, 12.0) * 0.025;
                above += _SunColor.rgb * (disc + halo) * (1.0 - cloud * 0.8);
                // Literal art-directed colours avoid the green/yellow inversion produced by using
                // a blue RGB tint as a scattering-wavelength control in Skybox/Procedural.
                return half4((elevation >= 0.0 ? above : below) * _Exposure, 1.0h);
            }
            ENDHLSL
        }
    }
}
