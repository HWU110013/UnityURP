// 果凍轉場：畫面像果凍晃動扭曲後消失
Shader "CatzTools/SceneFlow/Jelly"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _WobbleX ("水平晃動", Range(0, 0.15)) = 0.06
        _WobbleY ("垂直晃動", Range(0, 0.15)) = 0.04
        _Frequency ("晃動頻率", Range(1, 15)) = 5
        _Speed ("晃動速度", Range(1, 20)) = 8
        _Squash ("壓扁強度", Range(0, 0.5)) = 0.2
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
            float _WobbleX;
            float _WobbleY;
            float _Frequency;
            float _Speed;
            float _Squash;

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

                // 強度曲線：先增後減，讓果凍在中段晃最大
                float intensity = sin(_Progress * PI);
                // 衰減彈跳：越接近結尾晃動越小
                float decay = exp(-_Progress * 3.0) * intensity;

                float t = _Time.y * _Speed;

                // ── 果凍晃動位移 ──
                float2 uv = i.uv;
                float2 centered = uv - 0.5;

                // 正弦波晃動（水平和垂直獨立頻率）
                float wobX = sin(centered.y * _Frequency * PI + t) * _WobbleX * decay;
                float wobY = sin(centered.x * _Frequency * PI + t * 1.3) * _WobbleY * decay;

                // 壓扁/拉伸效果（中心不動，邊緣變形大）
                float squashAmount = sin(t * 1.7) * _Squash * decay;
                float scaleX = 1.0 + squashAmount;
                float scaleY = 1.0 - squashAmount;

                // 套用變形
                float2 distortedUV;
                distortedUV.x = centered.x * scaleX + 0.5 + wobX;
                distortedUV.y = centered.y * scaleY + 0.5 + wobY;
                distortedUV = clamp(distortedUV, 0.001, 0.999);

                // 取樣
                fixed4 screenCol = tex2D(_ScreenTex, distortedUV);

                // ── 邊緣檢測：變形後超出原始畫面的部分顯示遮罩色 ──
                float2 edge = abs(distortedUV - 0.5);
                float edgeMask = smoothstep(0.48, 0.5, max(edge.x, edge.y));

                // ── 遮罩色混合（Progress 越大越多） ──
                float colorBlend = _Progress * _Progress * _Opacity;
                colorBlend = max(colorBlend, edgeMask * intensity * _Opacity);

                fixed4 result;
                result.rgb = lerp(screenCol.rgb, _Color.rgb, colorBlend);

                // Alpha
                float alpha = saturate(max(intensity * 0.5, colorBlend));
                alpha *= step(0.001, _Progress);

                return fixed4(result.rgb, alpha);
            }
            ENDCG
        }
    }
}
