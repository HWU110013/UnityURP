// 模糊轉場：擷取畫面後逐漸模糊，最終覆蓋為純色
// 使用多層圓盤取樣模擬高品質高斯模糊
Shader "CatzTools/SceneFlow/Blur"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _MaxBlur ("最大模糊強度", Range(1, 50)) = 20
        _ScreenTex ("擷取畫面（自動設定）", 2D) = "black" {}
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
            float4 _ScreenTex_TexelSize;
            fixed4 _Color;
            float _Progress;
            float _MaxBlur;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 多層圓盤取樣（共 37 點：1 中心 + 6 內圈 + 12 中圈 + 18 外圈）
            fixed3 DiscBlur(float2 uv, float2 texel)
            {
                fixed3 col = tex2D(_ScreenTex, uv).rgb * 0.1;
                float totalWeight = 0.1;

                // 角度間隔
                static const float PI2 = 6.28318;

                // 內圈（6 點，半徑 0.33）
                for (int j = 0; j < 6; j++)
                {
                    float angle = PI2 * j / 6.0;
                    float2 offset = float2(cos(angle), sin(angle)) * texel * 0.33;
                    col += tex2D(_ScreenTex, uv + offset).rgb * 0.08;
                    totalWeight += 0.08;
                }

                // 中圈（12 點，半徑 0.67）
                for (int k = 0; k < 12; k++)
                {
                    float angle = PI2 * k / 12.0;
                    float2 offset = float2(cos(angle), sin(angle)) * texel * 0.67;
                    col += tex2D(_ScreenTex, uv + offset).rgb * 0.05;
                    totalWeight += 0.05;
                }

                // 外圈（18 點，半徑 1.0）
                for (int m = 0; m < 18; m++)
                {
                    float angle = PI2 * m / 18.0;
                    float2 offset = float2(cos(angle), sin(angle)) * texel;
                    col += tex2D(_ScreenTex, uv + offset).rgb * 0.03;
                    totalWeight += 0.03;
                }

                return col / totalWeight;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 前 75%：模糊逐漸加重
                // 後 25%：畫面淡出為純色
                float blurPhase = saturate(_Progress / 0.75);
                float fadePhase = saturate((_Progress - 0.75) / 0.25);

                // 模糊半徑（平方曲線讓前段變化快）
                float radius = _MaxBlur * blurPhase * blurPhase;

                float2 uv = i.uv;

                // 用 texel 大小乘以 radius 控制取樣範圍
                float2 texel = _ScreenTex_TexelSize.xy * radius;

                fixed3 blurred;
                if (radius < 0.5)
                {
                    // 幾乎無模糊，直接取樣避免浪費
                    blurred = tex2D(_ScreenTex, uv).rgb;
                }
                else
                {
                    // 疊兩層不同尺度的圓盤模糊
                    blurred = DiscBlur(uv, texel) * 0.6
                            + DiscBlur(uv, texel * 0.5) * 0.4;
                }

                // 混合：模糊畫面 → 純色
                fixed3 col = lerp(blurred, _Color.rgb, fadePhase);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
