// 閃光彈轉場：白光爆閃 → 過曝 → 模糊耳鳴感 → 淡出
Shader "CatzTools/SceneFlow/Flashbang"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _FlashColor ("閃光色", Color) = (1, 1, 0.95, 1)
        _FlashPeak ("閃光峰值位置", Range(0.05, 0.4)) = 0.15
        _FlashIntensity ("閃光強度", Range(1, 5)) = 3
        _BlurAmount ("模糊量", Range(0, 0.05)) = 0.02
        _Desaturate ("去飽和度", Range(0, 1)) = 0.7
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
            float _Opacity;
            float4 _FlashColor;
            float _FlashPeak;
            float _FlashIntensity;
            float _BlurAmount;
            float _Desaturate;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float PI = 3.14159265;

                // ── 閃光曲線：快速爆閃後緩慢衰減 ──
                // 峰值在 _FlashPeak，之後指數衰減
                float flashRise = saturate(_Progress / _FlashPeak);
                float flashDecay = exp(-(_Progress - _FlashPeak) * 5.0 / (1.0 - _FlashPeak));
                float flash = (_Progress < _FlashPeak)
                    ? flashRise * flashRise * _FlashIntensity
                    : flashDecay * _FlashIntensity;

                // ── 模糊（閃光後視覺模糊）──
                float blurPhase = saturate((_Progress - _FlashPeak) / (1.0 - _FlashPeak));
                float blur = _BlurAmount * sin(blurPhase * PI); // 中段最糊

                float3 col = float3(0, 0, 0);
                float2 offsets[8];
                offsets[0] = float2(1, 0); offsets[1] = float2(-1, 0);
                offsets[2] = float2(0, 1); offsets[3] = float2(0, -1);
                offsets[4] = float2(0.7, 0.7); offsets[5] = float2(-0.7, 0.7);
                offsets[6] = float2(0.7, -0.7); offsets[7] = float2(-0.7, -0.7);

                col += tex2D(_ScreenTex, i.uv).rgb;
                for (int s = 0; s < 8; s++)
                {
                    float2 sUV = clamp(i.uv + offsets[s] * blur, 0.001, 0.999);
                    col += tex2D(_ScreenTex, sUV).rgb;
                }
                col /= 9.0;

                // ── 去飽和（閃光後色彩流失）──
                float desatAmount = _Desaturate * blurPhase;
                float luma = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(col, float3(luma, luma, luma), desatAmount);

                // ── 過曝：閃光時畫面爆亮 ──
                col += _FlashColor.rgb * flash;

                // ── 暗角隨後段加深 ──
                float2 centered = i.uv - 0.5;
                float dist = length(centered);
                float vigAmount = saturate((_Progress - 0.3) / 0.7);
                float vignette = smoothstep(0.3, 0.7, dist) * vigAmount;

                // ── 淡出到遮罩色 ──
                float fadeOut = _Progress * _Progress * _Opacity;
                col = lerp(col, _Color.rgb, max(fadeOut, vignette));

                // Alpha：閃光立即可見，後段完全覆蓋
                float alpha = saturate(max(flash * 0.3, max(fadeOut, _Progress * 0.4)));
                alpha *= step(0.001, _Progress);

                return float4(saturate(col), alpha);
            }
            ENDCG
        }
    }
}
