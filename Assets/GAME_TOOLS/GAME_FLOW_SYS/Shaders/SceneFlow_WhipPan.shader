// 甩鏡轉場：快速方向性動態模糊，像攝影機甩過去
Shader "CatzTools/SceneFlow/WhipPan"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _BlurStrength ("模糊強度", Range(0, 0.15)) = 0.08
        _Angle ("甩鏡角度", Range(0, 360)) = 0
        _SampleCount ("取樣數（品質）", Range(4, 24)) = 12
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
            float _BlurStrength;
            float _Angle;
            float _SampleCount;

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

                // 模糊強度曲線：中段最強（sin 曲線）
                float intensity = sin(_Progress * PI);
                float strength = _BlurStrength * intensity;

                // 甩鏡方向
                float rad = _Angle * 0.01745329;
                float2 dir = float2(cos(rad), sin(rad));

                // 方向性動態模糊（沿方向多次取樣平均）
                int samples = max(4, (int)_SampleCount);
                fixed3 col = fixed3(0, 0, 0);

                for (int s = 0; s < samples; s++)
                {
                    float t = ((float)s / (float)(samples - 1)) - 0.5; // -0.5 ~ 0.5
                    float2 offset = dir * t * strength;
                    float2 sampleUV = clamp(i.uv + offset, 0.001, 0.999);
                    col += tex2D(_ScreenTex, sampleUV).rgb;
                }
                col /= (float)samples;

                // 位移效果：畫面隨進度往甩鏡方向偏移
                float slideAmount = intensity * 0.3;
                float2 slideUV = clamp(i.uv - dir * slideAmount, 0.001, 0.999);
                fixed3 slideCol = fixed3(0, 0, 0);
                for (int s2 = 0; s2 < samples; s2++)
                {
                    float t2 = ((float)s2 / (float)(samples - 1)) - 0.5;
                    float2 offset2 = dir * t2 * strength;
                    float2 sampleUV2 = clamp(slideUV + offset2, 0.001, 0.999);
                    slideCol += tex2D(_ScreenTex, sampleUV2).rgb;
                }
                slideCol /= (float)samples;

                // 混合：前半段模糊+位移，後半段淡入遮罩色
                fixed3 blurred = lerp(col, slideCol, _Progress);
                float colorBlend = _Progress * _Progress * _Opacity;

                fixed4 result;
                result.rgb = lerp(blurred, _Color.rgb, colorBlend);

                // Alpha
                float blurAlpha = intensity;
                result.a = saturate(max(blurAlpha, colorBlend));
                result.a *= step(0.001, _Progress);

                return result;
            }
            ENDCG
        }
    }
}
