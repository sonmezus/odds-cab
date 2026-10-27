Shader "OddsCab/Bosphorus Water"
{
    Properties
    {
        _MainTex ("Water Detail", 2D) = "gray" {}
        [NoScaleOffset] _NormalMap ("Tiling Ripple Normals (linear RG)", 2D) = "bump" {}
        _NormalScale ("Normal Tile Size (m)", Float) = 6
        _NearColor ("Near Water", Color) = (0.10, 0.48, 0.55, 1)
        _DeepColor ("Deep Water", Color) = (0.03, 0.20, 0.38, 1)
        _SkyColor ("Sky Reflection", Color) = (0.18, 0.48, 0.68, 1)
        _HorizonColor ("Coastal Horizon Reflection", Color) = (0.50, 0.64, 0.66, 1)
        _FoamColor ("Crest Foam", Color) = (0.83, 0.92, 0.91, 1)
        _WaveA ("Wave A (direction XZ, amplitude m, wavelength m)", Vector) = (1, 0.35, 0.06, 24)
        _WaveB ("Wave B (direction XZ, amplitude m, wavelength m)", Vector) = (-0.4, 1, 0.035, 16)
        _WaveSteepness ("Wave Steepness", Range(0, 1)) = 0.65
        _WaveSpeed ("Wave Speed", Range(0, 2)) = 0.8
        _DetailScale ("Ripple Scale (m)", Float) = 7
        _RippleStrength ("Ripple Strength", Range(0, 3)) = 1.1
        _SunReflection ("Sun Glitter", Range(0, 8)) = 2.0
        _GlossPower ("Glitter Sharpness", Range(16, 512)) = 320
        _DeepDistance ("Deep From (m)", Float) = 70
        _FoamStrength ("Crest Foam Strength", Range(0, 1)) = 0.18
        _Flow ("Extra Flow (world metres per second XZ)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForward" }
            // Chunk/vista parents mirror the sea when the route changes shore.
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_local _ _CURVED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Shaders/CurvedWorld.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _NearColor, _DeepColor, _SkyColor, _FoamColor, _HorizonColor;
                float4 _WaveA, _WaveB, _Flow;
                float _WaveSteepness, _WaveSpeed;
                float _DetailScale, _RippleStrength, _SunReflection, _GlossPower, _DeepDistance, _FoamStrength, _NormalScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 flatWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half2 fogCrest : TEXCOORD3;
            };

            void AddWave(float4 wave, float2 anchor, float time, inout float3 position,
                inout float3 tangentX, inout float3 tangentZ, inout float crest, inout float amplitudeSum)
            {
                float2 direction = wave.xy * rsqrt(max(dot(wave.xy, wave.xy), 0.0001));
                // These limits match BosphorusWaterMesh.DisplacementMargin and the grid cell size.
                float amplitude = clamp(wave.z, 0.0, 0.18);
                float k = TWO_PI / max(wave.w, 12.0);
                float speed = sqrt(9.81 / k) * clamp(_WaveSpeed, 0.0, 2.0);
                float phase = k * (dot(direction, anchor) - speed * time);
                float s, c;
                sincos(phase, s, c);
                float horizontal = saturate(_WaveSteepness) * amplitude;
                position += float3(direction.x * horizontal * c, amplitude * s, direction.y * horizontal * c);
                float3 derivative = float3(-direction.x * horizontal * s, amplitude * c, -direction.y * horizontal * s) * k;
                tangentX += derivative * direction.x;
                tangentZ += derivative * direction.y;
                crest += amplitude * s;
                amplitudeSum += amplitude;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(input.positionOS.xyz);
                o.flatWS = ws;
                // The distant vista deliberately slopes upwards. Derive base tangents from the mesh normal.
                float3 baseNormal = TransformObjectToWorldNormal(input.normalOS);
                float2 slope = -baseNormal.xz / max(abs(baseNormal.y), 0.05);
                float3 tangentX = float3(1, slope.x, 0);
                float3 tangentZ = float3(0, slope.y, 1);
                float crest = 0, amplitudeSum = 0;
                float2 anchor = ws.xz + _Flow.xy * _Time.y;
                AddWave(_WaveA, anchor, _Time.y, ws, tangentX, tangentZ, crest, amplitudeSum);
                AddWave(_WaveB, anchor, _Time.y, ws, tangentX, tangentZ, crest, amplitudeSum);
                #if defined(_CURVED)
                    // Apply the bend Jacobian to tangents, keeping highlights on the curved surface.
                    float d = clamp(ws.z - _CurveOrigin.z - _CurveOrigin.w, 0.0, CURVE_FULL_DISTANCE);
                    float3 bendDerivative = float3(_CurveAmount.x, _CurveAmount.y, 0) * (2.0 * d);
                    tangentX += bendDerivative * tangentX.z;
                    tangentZ += bendDerivative * tangentZ.z;
                    ws = CurveWorldPosition(ws);
                #endif
                o.normalWS = normalize(cross(tangentZ, tangentX));
                o.positionWS = ws;
                o.positionHCS = TransformWorldToHClip(ws);
                o.fogCrest = half2(ComputeFogFactor(o.positionHCS.z), crest / max(amplitudeSum, 0.001));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 waterPosition = i.flatWS.xz + _Flow.xy * t;
                float2 p = waterPosition / max(_DetailScale, 0.1);
                float2 uvA = p * _MainTex_ST.xy + _MainTex_ST.zw + float2(t * 0.018, t * 0.031);
                float2 uvB = p * 1.7 * _MainTex_ST.xy + _MainTex_ST.zw + float2(-t * 0.027, t * 0.012);
                half hA = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvA).rgb);
                half hB = Luminance(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB).rgb);

                // Two decorrelated scrolling normal samples break up wave fronts without regular sine stripes.
                // The generated map uses linear RG slopes, so it has the same encoding on every graphics API.
                float footprint = length(fwidth(waterPosition));
                float rippleFade = rcp(1.0 + footprint * footprint * 1.8);
                float normalSize = max(_NormalScale, 0.25);
                float2 normalUV = waterPosition / normalSize;
                float2 nA = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV + float2(t * 0.013, -t * 0.019)).rg * 2.0 - 1.0;
                float2 rotatedUV = float2(normalUV.x * 0.8 - normalUV.y * 0.6, normalUV.x * 0.6 + normalUV.y * 0.8) * 1.63;
                float2 nB = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, rotatedUV + float2(-t * 0.017, t * 0.011)).rg * 2.0 - 1.0;
                float2 microSlope = (nA + float2(nB.x * 0.8 + nB.y * 0.6, -nB.x * 0.6 + nB.y * 0.8) * 0.55)
                    * _RippleStrength * rippleFade;
                float3 n = normalize(float3(i.normalWS.x - microSlope.x, i.normalWS.y, i.normalWS.z - microSlope.y));

                float3 toCam = GetCameraPositionWS() - i.positionWS;
                float dist = max(length(toCam), 0.001);
                float3 v = toCam / dist;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #endif
                Light sun = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                half attenuation = sun.shadowAttenuation * sun.distanceAttenuation;
                half3 ambient = max(SampleSH(n), half3(0.02, 0.025, 0.03));
                // Broad currents separate quiet water masses. Small photo ripples are subordinate
                // to the real wave normal instead of printing parallel dark stripes over the sea.
                half current = sin(waterPosition.x * 0.027 + sin(waterPosition.y * 0.018))
                    * sin(waterPosition.y * 0.035 + waterPosition.x * 0.008);
                half deep = smoothstep(0.0, 1.0, saturate(dist / max(_DeepDistance, 0.1) + current * .085));
                half detail = saturate((hA + hB) * 1.8);
                half3 body = lerp(_NearColor.rgb, _DeepColor.rgb, deep)
                    * lerp(0.96, 1.025, detail) * (1.0 + current * .035);
                half3 diffuseLight = ambient * 0.75 + sun.color * (0.20 + 0.38 * saturate(dot(n, sun.direction))) * attenuation;
                body *= diffuseLight;

                half fres = pow(1.0 - saturate(dot(n, v)), 5.0);
                // Reflect the weather's warm horizon only at grazing angles; retain cool blue-green
                // body colour. The shore, sky and water now share one atmosphere without a flat tint.
                half skyLight = saturate(Luminance(ambient) * 0.8 + Luminance(sun.color) * 0.35);
                half reflectionHeight = saturate(reflect(-v, n).y);
                half horizonBand = 1.0 - smoothstep(0.015, 0.35, reflectionHeight);
                half3 sky = lerp(_SkyColor.rgb, _HorizonColor.rgb, horizonBand * .58)
                    * (0.22 + skyLight * 0.78);
                half3 col = lerp(body, sky, saturate(0.035 + fres * 0.58));

                float gloss = clamp(_GlossPower, 128.0, 512.0);
                float3 halfVector = SafeNormalize(sun.direction + v);
                half spec = pow(saturate(dot(n, halfVector)), gloss);
                // The source water photograph is dark in linear colour. Multiplying its two samples
                // made almost every pixel miss the old sparkle threshold; retain a continuous mask.
                half sparkle = lerp(0.55, 1.0, detail);
                half glintFilter = saturate(1.0 - length(fwidth(n)) * 1.25);
                // Narrow filtered highlights can exceed one for bloom without widening into white sheets.
                col += sun.color * attenuation * spec * sparkle * glintFilter * _SunReflection * (0.35 + fres * 0.65);

                // Crest foam brightens at grazing angles without a depth texture or reflection camera.
                half foam = smoothstep(0.65, 0.95, i.fogCrest.y) * _FoamStrength;
                foam *= (0.3 + fres * 0.7) * smoothstep(0.40, 0.90, detail) * rippleFade;
                half3 foamLight = ambient + sun.color * attenuation * 0.7;
                col = lerp(col, _FoamColor.rgb * foamLight, saturate(foam));
                return half4(MixFog(col, i.fogCrest.x), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
