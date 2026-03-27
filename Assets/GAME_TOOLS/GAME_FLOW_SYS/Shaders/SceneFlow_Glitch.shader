// 故障轉場：畫面撕裂 + RGB 色彩偏移 + 噪點閃爍
Shader "CatzTools/SceneFlow/Glitch"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _TearStrength ("撕裂強度", Range(0, 0.2)) = 0.06
        _TearCount ("撕裂條數", Range(2, 30)) = 8
        _RGBShift ("色偏強度", Range(0, 0.05)) = 0.015
        _NoiseAmount ("噪點量", Range(0, 1)) = 0.3
        _FlickerSpeed ("閃爍速度", Range(1, 20)) = 8
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
            fixed4 _Color;
            float _Progress;
            float _Opacity;
            float _TearStrength;
            float _TearCount;
            float _RGBShift;
            float _NoiseAmount;
            float _FlickerSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 偽隨機雜訊
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 區塊雜訊（同一水平帶回傳相同值）
            float blockNoise(float y, float seed)
            {
                return hash(float2(floor(y * _TearCount), seed));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                const float PI = 3.14159265;

                // 強度曲線：中段最強（sin 曲線）
                float intensity = sin(_Progress * PI);
                float t = floor(_Time.y * _FlickerSpeed);

                // ── 撕裂：水平帶隨機位移 ──
                float tearSeed = t * 7.13;
                float tearBlock = blockNoise(i.uv.y, tearSeed);
                // 只有部分帶會撕裂（閾值隨強度變化）
                float tearThreshold = 1.0 - intensity * 0.6;
                float tearActive = step(tearThreshold, tearBlock);
                float tearOffset = (tearBlock - 0.5) * 2.0 * _TearStrength * intensity * tearActive;

                float2 uv = i.uv;
                uv.x += tearOffset;
                uv.x = clamp(uv.x, 0.001, 0.999);

                // ── RGB 色偏 ──
                float shift = _RGBShift * intensity;
                // 偏移方向隨時間跳動
                float shiftDir = hash(float2(t, 3.7)) - 0.5;
                float2 rOffset = float2(shift * shiftDir, 0);
                float2 gOffset = float2(0, 0);
                float2 bOffset = float2(-shift * shiftDir, 0);

                float2 uvR = clamp(uv + rOffset, 0.001, 0.999);
                float2 uvG = uv;
                float2 uvB = clamp(uv + bOffset, 0.001, 0.999);

                float r = tex2D(_ScreenTex, uvR).r;
                float g = tex2D(_ScreenTex, uvG).g;
                float b = tex2D(_ScreenTex, uvB).b;
                fixed3 screenCol = fixed3(r, g, b);

                // ── 噪點閃爍 ──
                float noise = hash(i.uv * 500.0 + t * 13.7);
                float noiseMask = _NoiseAmount * intensity;
                screenCol = lerp(screenCol, fixed3(noise, noise, noise), noiseMask);

                // ── 隨機亮度閃爍（整畫面） ──
                float flicker = 1.0 + (hash(float2(t, 1.23)) - 0.5) * 0.3 * intensity;
                screenCol *= flicker;

                // ── 遮罩色混合（Progress 越大越多遮罩色） ──
                float colorBlend = _Progress * _Progress * _Opacity;

                fixed4 result;
                result.rgb = lerp(screenCol, _Color.rgb, colorBlend);

                // Alpha：有任何效果就顯示
                result.a = saturate(max(intensity, colorBlend));
                result.a *= step(0.001, _Progress);

                return result;
            }
            ENDCG
        }
    }
}
