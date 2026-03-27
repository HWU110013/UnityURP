// 老電視轉場
// 關機：畫面壓縮成橫線 → 縮成亮點 → 消失
// 開機：靜態雜訊 → 畫面閃爍浮現 → 穩定
Shader "CatzTools/SceneFlow/TVOff"
{
    Properties
    {
        _Color ("背景色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _LinePhase ("橫線階段比例", Range(0.3, 0.8)) = 0.6
        _LineBrightness ("橫線亮度", Range(0.5, 3)) = 1.5
        _DotSize ("亮點大小", Range(0.005, 0.05)) = 0.02
        _DotBrightness ("亮點亮度", Range(1, 5)) = 2.5
        _Scanlines ("掃描線強度", Range(0, 1)) = 0.3
        _StaticStrength ("雪花雜訊強度", Range(0, 1)) = 0.4
        [Toggle] _Reverse ("反向（開機模式）", Float) = 0
        _ScreenTex ("擷取畫面", 2D) = "white" {}
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _ScreenTex;
            float4 _Color;
            float _Progress;
            float _LinePhase;
            float _LineBrightness;
            float _DotSize;
            float _DotBrightness;
            float _Scanlines;
            float _StaticStrength;
            float _Reverse;

            float tvHash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // ── 關機效果（Out）──
            float4 tvOff(v2f i, float2 centered, float aspect)
            {
                float p1End = _LinePhase;
                float p2End = 0.9;

                // Phase 1：垂直壓縮
                float squashP = saturate(_Progress / p1End);
                float squash = 1.0 - pow(squashP, 3.0) * 0.998;
                float lineH = max(squash, 0.002);
                float inLine = step(abs(centered.y), lineH * 0.5);

                float2 sUV = float2(i.uv.x, centered.y / lineH + 0.5);
                sUV = clamp(sUV, 0.001, 0.999);

                // Phase 2：水平縮成點
                float shrinkP = saturate((_Progress - p1End) / (p2End - p1End));
                float lineW = max(1.0 - shrinkP * shrinkP * 0.98, 0.01);
                float inW = step(abs(centered.x), lineW * 0.5);
                sUV.x = centered.x / lineW + 0.5;
                sUV = clamp(sUV, 0.001, 0.999);

                // Phase 3：亮點消失
                float fadeP = saturate((_Progress - p2End) / (1.0 - p2End));

                float3 scr = tex2D(_ScreenTex, sUV).rgb;
                scr *= 1.0 + squashP * (_LineBrightness - 1.0);

                // 雪花雜訊：強度隨壓縮增加
                float t = floor(_Time.y * 30.0);
                float staticNoise = tvHash(i.uv * 800.0 + t * 17.3);
                float staticMix = _StaticStrength * squashP;
                scr = lerp(scr, float3(staticNoise, staticNoise, staticNoise), staticMix);

                float scanline = sin(i.uv.y * _ScreenParams.y * 0.5) * 0.5 + 0.5;
                scr *= 1.0 - scanline * _Scanlines * squashP;

                float vis = inLine * inW;
                float dist = length(centered * float2(aspect, 1.0));
                float glow = exp(-dist * dist / (_DotSize * _DotSize)) * shrinkP * _DotBrightness * (1.0 - fadeP);

                float3 col = lerp(_Color.rgb, scr, vis * (1.0 - fadeP));
                col += float3(1, 1, 1) * glow;

                float bgA = _Progress * _Progress;
                float cA = max(vis * (1.0 - fadeP), glow);
                float alpha = saturate(max(bgA, cA));
                alpha *= step(0.001, _Progress);

                return float4(saturate(col), alpha);
            }

            // ── 開機效果（In）──
            float4 tvOn(v2f i, float2 centered, float aspect)
            {
                // Progress 0→1 = 從全黑到畫面正常
                // 反轉：1-Progress 讓 0=全覆蓋 1=全透明
                float p = _Progress;

                // Phase 1 (0~0.3)：靜態雜訊從黑中浮現
                // Phase 2 (0.2~0.7)：畫面從雜訊中閃爍浮現
                // Phase 3 (0.6~1.0)：畫面穩定，雜訊消退

                float t = floor(_Time.y * 12.0);

                // 靜態雜訊
                float noise = tvHash(i.uv * 500.0 + t * 17.3);
                float noiseStrength = (1.0 - smoothstep(0.0, 0.6, p)) * _StaticStrength;

                // 畫面可見度（帶閃爍）
                float flicker = tvHash(float2(t * 3.7, 1.23));
                float flickerMask = (p > 0.15 && p < 0.7) ? step(0.3, flicker) : 1.0;
                float imageVisible = smoothstep(0.1, 0.8, p) * flickerMask;

                // 掃描線（開機時較重，漸消）
                float scanline = sin(i.uv.y * _ScreenParams.y * 0.5) * 0.5 + 0.5;
                float scanStrength = _Scanlines * (1.0 - smoothstep(0.3, 1.0, p));

                // 垂直抖動（開機初期畫面不穩）
                float jitter = (tvHash(float2(t * 5.1, 2.34)) - 0.5) * 0.02 * (1.0 - smoothstep(0.0, 0.5, p));
                float2 sUV = clamp(float2(i.uv.x, i.uv.y + jitter), 0.001, 0.999);

                float3 scr = tex2D(_ScreenTex, sUV).rgb;
                scr *= 1.0 - scanline * scanStrength;

                // 亮度波動
                float brightFlicker = 1.0 + (tvHash(float2(t, 7.7)) - 0.5) * 0.4 * (1.0 - smoothstep(0.2, 0.8, p));
                scr *= brightFlicker;

                // 合成
                float3 noiseCol = float3(noise, noise, noise) * 0.5;
                float3 col = lerp(_Color.rgb, noiseCol, noiseStrength * 0.8);
                col = lerp(col, scr, imageVisible);

                // Alpha：從全覆蓋到全透明
                float alpha = 1.0 - smoothstep(0.7, 1.0, p);
                alpha = max(alpha, noiseStrength * 0.5);
                alpha = max(alpha, (1.0 - imageVisible) * 0.8);

                return float4(saturate(col), saturate(alpha));
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 centered = i.uv - 0.5;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                if (_Reverse > 0.5)
                    return tvOn(i, centered, aspect);
                else
                    return tvOff(i, centered, aspect);
            }
            ENDCG
        }
    }
}
