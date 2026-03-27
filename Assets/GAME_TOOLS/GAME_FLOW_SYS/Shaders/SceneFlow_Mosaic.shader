// 馬賽克拼圖轉場：畫面碎成磚塊，逐塊翻轉消失
Shader "CatzTools/SceneFlow/Mosaic"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _GridX ("水平磚數", Range(2, 30)) = 10
        _GridY ("垂直磚數", Range(2, 20)) = 8
        _GapWidth ("磚縫寬度", Range(0, 0.05)) = 0.008
        _Randomness ("隨機延遲", Range(0, 1)) = 0.6
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
            float _GridX;
            float _GridY;
            float _GapWidth;
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
                float PI = 3.14159265;
                float2 grid = float2(_GridX, _GridY);
                float2 cellID = floor(i.uv * grid);
                float2 cellUV = frac(i.uv * grid);

                // 磚縫
                float2 gapMask = smoothstep(0.0, _GapWidth * grid, min(cellUV, 1.0 - cellUV));
                float gap = gapMask.x * gapMask.y;

                // 每塊隨機延遲
                float rndDelay = hash1(cellID);
                float delay = rndDelay * _Randomness;
                float localP = saturate((_Progress - delay) / max(1.0 - _Randomness, 0.01));

                // 還沒開始翻 → 透明
                if (localP <= 0.001)
                    return float4(0, 0, 0, 0);

                // 磚縫：在翻轉中的磚塊邊緣顯示遮罩色
                if (gap < 0.99)
                    return float4(_Color.rgb, 1.0);

                // ── 翻轉動畫 ──
                float flipAngle = localP * PI;

                if (localP < 0.5)
                {
                    float scaleX = cos(flipAngle);
                    scaleX = max(abs(scaleX), 0.01);

                    float squashedX = (cellUV.x - 0.5) / scaleX + 0.5;
                    if (squashedX < 0.0 || squashedX > 1.0)
                        return float4(_Color.rgb, 1.0);

                    float2 sampleUV = (cellID + float2(squashedX, cellUV.y)) / grid;
                    sampleUV = clamp(sampleUV, 0.001, 0.999);

                    float3 col = tex2D(_ScreenTex, sampleUV).rgb;
                    col *= scaleX;

                    return float4(col, 1.0);
                }
                else
                {
                    // 後半：遮罩色展開
                    float scaleX = -cos(flipAngle); // 0 → 1
                    scaleX = max(scaleX, 0.01);

                    float squashedX = (cellUV.x - 0.5) / scaleX + 0.5;

                    if (squashedX < 0.0 || squashedX > 1.0)
                        return float4(_Color.rgb, 1.0);

                    // 遮罩色 + 輕微陰影
                    float shade = 0.7 + 0.3 * scaleX;
                    return float4(_Color.rgb * shade, 1.0);
                }
            }
            ENDCG
        }
    }
}
