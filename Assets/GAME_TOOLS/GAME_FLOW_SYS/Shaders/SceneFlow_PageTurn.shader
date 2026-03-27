// 翻頁轉場：折線從右向左推進，左側顯示翻過的紙張背面
Shader "CatzTools/SceneFlow/PageTurn"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _CurlRadius ("捲曲半徑", Range(0.03, 0.2)) = 0.08
        _Angle ("翻頁角度", Range(0, 360)) = 0
        _ShadowWidth ("陰影寬度", Range(0, 0.15)) = 0.05
        _BackShade ("背面暗度", Range(0, 0.6)) = 0.3
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _ScreenTex ("Screen Capture", 2D) = "white" {}
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

            #define PI 3.14159265

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _Color;
            float _Progress;
            float _CurlRadius;
            float _Angle;
            float _ShadowWidth;
            float _BackShade;
            sampler2D _ScreenTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 旋轉 UV
            float2 rotateUV(float2 uv, float ca, float sa)
            {
                float2 c = uv - 0.5;
                return float2(c.x * ca + c.y * sa, -c.x * sa + c.y * ca) + 0.5;
            }

            // 反旋轉 UV
            float2 unrotateUV(float2 uv, float ca, float sa)
            {
                float2 c = uv - 0.5;
                return float2(c.x * ca - c.y * sa, c.x * sa + c.y * ca) + 0.5;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float R = max(_CurlRadius, 0.01);
                float curlW = PI * R; // 捲曲帶寬度（半圓弧長）

                // 旋轉
                float rad = _Angle * 0.01745329;
                float ca = cos(rad);
                float sa = sin(rad);
                float2 ruv = rotateUV(i.uv, ca, sa);

                // 折線從右往左推進（補償捲曲寬度，避免邊界外取樣）
                float foldX = lerp(1.0 + curlW, -curlW, _Progress);

                // 像素相對於折線的距離（正=右邊未翻，負=左邊已翻）
                float dx = ruv.x - foldX;

                if (dx > 0.0)
                {
                    // ── 右側：未翻的平面紙張 ──
                    float2 origUV = unrotateUV(ruv, ca, sa);
                    float valid = step(0.001, origUV.x) * step(origUV.x, 0.999)
                                * step(0.001, origUV.y) * step(origUV.y, 0.999);

                    fixed4 col = tex2D(_ScreenTex, clamp(origUV, 0.001, 0.999));

                    // 折線旁投射陰影
                    float shadow = smoothstep(0.0, _ShadowWidth, dx);
                    col.rgb *= 0.75 + 0.25 * shadow;

                    return fixed4(lerp(_Color.rgb, col.rgb, valid), 1.0);
                }

                float absDx = -dx; // 距折線多遠（正值）

                if (absDx < curlW)
                {
                    // ── 捲曲帶：紙張捲在圓柱面上，顯示背面 ──
                    float theta = absDx / R; // 0（折線）~ PI（捲曲底部）

                    // 背面取樣：鏡射 X，原本在折線右邊 absDx 處的紙翻過來
                    float2 sampleRUV = float2(foldX + absDx, ruv.y);
                    float2 origUV = unrotateUV(sampleRUV, ca, sa);
                    float valid = step(0.001, origUV.x) * step(origUV.x, 0.999)
                                * step(0.001, origUV.y) * step(origUV.y, 0.999);

                    fixed4 col = tex2D(_ScreenTex, clamp(origUV, 0.001, 0.999));

                    // 圓柱面光影：頂部（theta=PI/2）最亮，兩端暗
                    float cylinderShade = 1.0 - _BackShade * (1.0 - sin(theta));
                    col.rgb *= cylinderShade;

                    return fixed4(lerp(_Color.rgb, col.rgb, valid), 1.0);
                }

                // ── 左側：完全翻過，露出遮罩色 ──
                // 捲曲帶底部投射的陰影
                float behindDist = absDx - curlW;
                float behindShadow = 1.0 - (1.0 - smoothstep(0.0, _ShadowWidth, behindDist)) * 0.2;

                return fixed4(_Color.rgb * behindShadow, 1.0);
            }
            ENDCG
        }
    }
}
