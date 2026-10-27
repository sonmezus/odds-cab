Shader "OddsCab/Cab Weather Glass"
{
    Properties
    {
        _Tint ("Moisture Tint", Color) = (0.77,0.87,0.88,1)
        _FogAmount ("Condensation", Range(0,1)) = 0
        _RainAmount ("Rain Beads", Range(0,1)) = 0
        _Interior ("Interior Moisture", Range(0,1)) = 1
        _WipedGlass ("Windscreen Wiper Area", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "CabMoisture"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _FogAmount, _RainAmount, _Interior, _WipedGlass;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            float Hash(float2 p)
            {
                float3 h = frac(float3(p.xyx) * 0.1031);
                h += dot(h, h.yzx + 33.33);
                return frac((h.x + h.y) * h.z);
            }
            half Wiped(float2 uv)
            {
                float2 dl = uv - float2(0.25,0.025), dr = uv - float2(0.75,0.025);
                float al = atan2(dl.x,dl.y), ar = atan2(dr.x,dr.y);
                half left = (1.0-smoothstep(0.62,0.68,length(dl))) * (1.0-smoothstep(0.74,0.81,abs(al+0.15)));
                half right = (1.0-smoothstep(0.62,0.68,length(dr))) * (1.0-smoothstep(0.74,0.81,abs(ar-0.15)));
                return max(left,right) * _WipedGlass;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y;
                // The boundary fades before the edge of each actual window, avoiding a rectangular overlay.
                half border = smoothstep(0.0,0.035,min(min(uv.x,1.0-uv.x),min(uv.y,1.0-uv.y)));
                float2 cells = uv * float2(68.0,34.0);
                float2 id = floor(cells);
                float random = Hash(id);
                float2 q = frac(cells) - float2(0.2+0.6*random,0.25+0.5*Hash(id+9.13));
                float radius = lerp(0.09,0.25,random);
                float aa = max(length(fwidth(cells)),0.03);
                half dew = 1.0-smoothstep(radius-aa*0.45,radius+aa*0.45,length(q*float2(1.0,1.32)));
                float2 rainCells = uv*float2(32.0,12.0);
                float column = floor(rainCells.x);
                float drift = Hash(float2(column,13.7));
                rainCells.y += t*(0.55+drift*0.8);
                float2 rid=floor(rainCells), rq=frac(rainCells);
                float rx=0.25+Hash(rid+2.1)*0.5;
                float2 drop=(rq-float2(rx,0.28))*float2(1.0,0.8);
                half bead=pow(saturate(1.0-length(drop*float2(6.5,4.3))),2.0);
                half tail=(1.0-smoothstep(0.012,0.037,abs(rq.x-rx)))
                    *smoothstep(0.0,0.2,rq.y)*(1.0-smoothstep(0.34,0.95,rq.y));
                half wiped=Wiped(uv);
                half wet=(bead+tail*0.35)*(1.0-wiped*0.9)*_RainAmount;
                float2 center=(uv-float2(0.5,0.48))*float2(1.45,1.0);
                half edges=smoothstep(0.20,0.68,length(center));
                half breath=0.91+0.09*sin(t*0.31+uv.x*5.0);
                half haze=_FogAmount*(0.035+edges*(0.20+_Interior*0.13))*breath;
                half droplets=dew*_FogAmount*(0.08+edges*0.12)+wet*0.42;
                half opacity=saturate(haze+droplets)*border;
                half highlight=pow(saturate(1.0-length((q+float2(0.055,-0.045))*9.0)),10.0);
                half3 color=_Tint.rgb*(0.83+dew*0.13)+highlight*_FogAmount*0.72+bead*_RainAmount*0.24;
                return half4(color,opacity);
            }
            ENDHLSL
        }
    }
}
