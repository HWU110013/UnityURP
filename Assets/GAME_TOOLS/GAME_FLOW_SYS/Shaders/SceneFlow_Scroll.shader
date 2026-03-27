// 捲軸轉場：畫面從中間割開，上下捲走，露出黑底，捲曲背面顯示遮罩色
Shader "CatzTools/SceneFlow/Scroll"
{
    Properties
    {
        _Color ("捲軸背面顏色", Color) = (0.82, 0.75, 0.58, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _CurlRadius ("捲曲半徑", Range(0.03, 0.2)) = 0.08
        _ShadowWidth ("陰影寬度", Range(0, 0.15)) = 0.05
        _BackShade ("背面暗度", Range(0, 0.6)) = 0.3
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

            #define PI 3.14159265

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _Color;
            float _Progress;
            float _CurlRadius;
            float _ShadowWidth;
            float _BackShade;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float R = max(_CurlRadius, 0.01);
                float curlW = PI * R;

                // 折線從中心往外推
                float topFold = lerp(0.5, 1.0 + curlW, _Progress);
                float botFold = lerp(0.5, -curlW, _Progress);

                float dy;
                float foldY;

                if (i.uv.y >= 0.5)
                {
                    // 上半：折線往上推
                    foldY = topFold;
                    dy = i.uv.y - topFold; // 正=外側(上方)，負=內側(中心方向)
                }
                else
                {
                    // 下半：折線往下推
                    foldY = botFold;
                    dy = botFold - i.uv.y; // 正=外側(下方)，負=內側(中心方向)
                }

                // ── 外側（未捲）：透明，場景直接看到 ──
                if (dy > 0.0)
                {
                    float shadow = exp(-dy * dy / (_ShadowWidth * _ShadowWidth + 0.0001)) * 0.3;
                    return float4(0, 0, 0, shadow);
                }

                float absDy = -dy;

                // ── 捲曲帶：捲軸背面（遮罩色 + 圓柱光影）──
                if (absDy < curlW)
                {
                    float theta = absDy / R;
                    float shade = 1.0 - _BackShade * (1.0 - sin(theta));
                    return float4(_Color.rgb * shade, 1.0);
                }

                // ── 內側（已捲走）：全黑底 ──
                float behind = absDy - curlW;
                float bShadow = 1.0 - (1.0 - smoothstep(0.0, _ShadowWidth, behind)) * 0.3;
                return float4(0, 0, 0, bShadow);
            }
            ENDCG
        }
    }
}
