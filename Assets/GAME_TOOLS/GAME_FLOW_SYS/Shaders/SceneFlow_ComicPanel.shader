// 漫畫格轉場：畫面分割成漫畫格，逐格縮小消失
Shader "CatzTools/SceneFlow/ComicPanel"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (1, 1, 1, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _GridX ("水平格數", Range(2, 8)) = 3
        _GridY ("垂直格數", Range(2, 6)) = 2
        _BorderWidth ("格線寬度", Range(0.005, 0.05)) = 0.015
        _BorderColor ("格線顏色", Color) = (0, 0, 0, 1)
        _Randomness ("隨機順序", Range(0, 1)) = 0.5
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
            float _GridX;
            float _GridY;
            float _BorderWidth;
            float4 _BorderColor;
            float _Randomness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash1(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 grid = float2(_GridX, _GridY);
                float2 cellID = floor(i.uv * grid);
                float2 cellUV = frac(i.uv * grid); // 0~1 in cell

                // 格線
                float2 borderDist = min(cellUV, 1.0 - cellUV);
                float2 borderNorm = borderDist * grid; // 正規化到螢幕空間
                float borderMask = step(_BorderWidth, min(borderNorm.x, borderNorm.y));
                // borderMask = 0 在格線上, 1 在格子內

                // 每格隨機延遲
                float rndDelay = hash1(cellID);
                float delay = rndDelay * _Randomness;
                float localP = saturate((_Progress - delay) / max(1.0 - _Randomness, 0.01));

                // 格線隨 Progress 出現
                float borderAppear = smoothstep(0.0, 0.15, _Progress);

                // ── 還沒開始消失的格子 ──
                if (localP <= 0.001)
                {
                    // 只顯示格線
                    if (borderAppear > 0.01 && borderMask < 0.5)
                        return float4(_BorderColor.rgb, borderAppear);
                    return float4(0, 0, 0, 0);
                }

                // ── 格子消失動畫：縮小 + 淡出 ──
                float shrinkT = localP * localP;

                // 從格子中心縮小
                float scale = 1.0 - shrinkT * 0.95;
                scale = max(scale, 0.01);
                float2 shrunkUV = (cellUV - 0.5) / scale + 0.5;

                // 超出格子範圍 → 遮罩色
                if (shrunkUV.x < 0.0 || shrunkUV.x > 1.0 ||
                    shrunkUV.y < 0.0 || shrunkUV.y > 1.0)
                {
                    return float4(_Color.rgb, _Opacity);
                }

                // 縮小後的格線
                float2 shrunkBorder = min(shrunkUV, 1.0 - shrunkUV) * grid;
                float shrunkBorderMask = step(_BorderWidth, min(shrunkBorder.x, shrunkBorder.y));

                // 格線
                if (shrunkBorderMask < 0.5)
                    return float4(_BorderColor.rgb, 1.0);

                // 取樣截圖
                float2 sampleUV = (cellID + shrunkUV) / grid;
                sampleUV = clamp(sampleUV, 0.001, 0.999);
                float3 col = tex2D(_ScreenTex, sampleUV).rgb;

                // 漫畫風格：稍微提高對比
                col = (col - 0.5) * (1.0 + localP * 0.3) + 0.5;

                // 淡出
                float fadeOut = 1.0 - shrinkT;

                return float4(saturate(col), fadeOut);
            }
            ENDCG
        }
    }
}
