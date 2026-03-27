// CRT 掃描線轉場：掃描線逐漸浮現覆蓋全畫面 + 閃爍 + 亮度衰減
Shader "CatzTools/SceneFlow/Scanline"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _LineCount ("掃描線數", Range(50, 800)) = 300
        _LineDarkness ("掃描線深度", Range(0, 1)) = 0.5
        _FlickerSpeed ("閃爍速度", Range(1, 20)) = 8
        _FlickerStrength ("閃爍強度", Range(0, 0.5)) = 0.15
        _Distortion ("畫面扭曲", Range(0, 0.03)) = 0.008
        _VignetteStrength ("暗角強度", Range(0, 2)) = 1
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

            fixed4 _Color;
            float _Progress;
            float _LineCount;
            float _LineDarkness;
            float _FlickerSpeed;
            float _FlickerStrength;
            float _Distortion;
            float _VignetteStrength;

            float hash(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                const float PI = 3.14159265;
                float t = floor(_Time.y * _FlickerSpeed);

                // ── CRT 掃描線紋路（全畫面同時浮現） ──
                float screenY = i.uv.y * _ScreenParams.y;
                float linePhase = frac(screenY / 3.0);
                float scanline = smoothstep(0.0, 0.35, linePhase) *
                                 (1.0 - smoothstep(0.65, 1.0, linePhase));
                // 掃描線強度隨 Progress 漸增
                float lineStrength = _LineDarkness * _Progress;

                // ── 行間隨機閃爍 ──
                float lineID = floor(i.uv.y * _LineCount * 0.3);
                float flicker = (hash(lineID + t * 7.31) - 0.5) * 2.0;
                float flickerAmount = flicker * _FlickerStrength * _Progress;

                // ── 整體亮度閃爍 ──
                float globalFlicker = 1.0 + (hash(t * 3.17) - 0.5) * _FlickerStrength * _Progress;

                // ── CRT 畫面微扭曲（桶狀變形） ──
                float2 centered = i.uv - 0.5;
                float dist2 = dot(centered, centered);
                float distortion = dist2 * _Distortion * _Progress;
                // 不實際移動UV（純遮罩shader），但影響暗角

                // ── 暗角（CRT 邊緣變暗） ──
                float vignette = 1.0 - dist2 * _VignetteStrength * _Progress * 4.0;
                vignette = saturate(vignette);

                // ── 亮度衰減：Progress 越大整體越暗 ──
                float brightness = 1.0 - _Progress * _Progress;

                // ── 合成 ──
                fixed3 col = _Color.rgb;

                // 掃描線區域明暗交替
                float lineMask = 1.0 - scanline * lineStrength;

                // 最終亮度
                col *= lineMask;
                col *= vignette;
                col *= globalFlicker;
                col += flickerAmount;
                col *= brightness;

                // Alpha：掃描線和亮度變化隨 Progress 漸增
                float alpha = _Progress;

                return fixed4(saturate(col), saturate(alpha));
            }
            ENDCG
        }
    }
}
