// 圖片拉簾轉場：兩張自訂圖片從兩側滑入合攏（高美術需求用）
// 預設純位移（圖片不變形），可選壓縮模式
Shader "CatzTools/SceneFlow/CurtainImage"
{
    Properties
    {
        _Progress ("轉場進度", Range(0, 1)) = 0
        _CurtainTexA ("拉簾 A（左/上）", 2D) = "black" {}
        _CurtainTexB ("拉簾 B（右/下）", 2D) = "black" {}
        _Angle ("開合角度", Range(0, 360)) = 0
        _Gap ("中縫間距", Range(0, 0.05)) = 0
        _Overlap ("合攏重疊量", Range(0, 0.15)) = 0
        _ShadowWidth ("邊緣陰影寬度", Range(0, 0.1)) = 0.03
        _ShadowStrength ("邊緣陰影強度", Range(0, 1)) = 0.5
        [Toggle] _Compress ("壓縮模式（圖片隨簾寬縮放）", Float) = 0
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

            sampler2D _CurtainTexA;
            sampler2D _CurtainTexB;
            float _Progress;
            float _Angle;
            float _Gap;
            float _Overlap;
            float _ShadowWidth;
            float _ShadowStrength;
            float _Compress;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 計算前緣陰影
            float edgeShadow(float coord, float edge, float shadowW, float strength)
            {
                float dist = abs(coord - edge) / max(shadowW, 0.001);
                float shadow = saturate(dist);
                return 1.0 - (1.0 - shadow) * strength;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 旋轉 UV 到指定角度
                float rad = _Angle * 0.01745329;
                float cosA = cos(rad);
                float sinA = sin(rad);

                float2 centered = i.uv - 0.5;
                centered.x *= aspect;
                float rotated = dot(centered, float2(cosA, sinA));

                // 半幅（旋轉後的最大投影距離）
                float halfSpan = (abs(cosA) * aspect + abs(sinA)) * 0.5;

                // 正規化到 -1 ~ 1
                float coord = rotated / halfSpan;

                // 合攏進度：0=全開，1=全合
                float halfGap = _Gap / halfSpan;
                float slideTarget = halfGap + _Overlap / halfSpan;
                float slidePos = lerp(1.0, slideTarget, _Progress);

                // ── 拉簾 A（負方向，從 -1 側滑入） ──
                float edgeA = -slidePos;
                float inA = step(coord, edgeA);

                // ── 拉簾 B（正方向，從 +1 側滑入） ──
                float edgeB = slidePos;
                float inB = step(edgeB, coord);

                // ── UV 計算 ──
                float2 uvA, uvB;

                if (_Compress > 0.5)
                {
                    // 壓縮模式：圖片隨可見寬度縮放（UV 0~1 映射到可見區域）
                    float spanA = 1.0 - slidePos;
                    uvA = float2(1.0 - saturate((edgeA - coord) / max(spanA, 0.001)), i.uv.y);

                    float spanB = 1.0 - slidePos;
                    uvB = float2(saturate((coord - edgeB) / max(spanB, 0.001)), i.uv.y);
                }
                else
                {
                    // 位移模式（預設）：圖片固定大小，純滑入裁切
                    // A 佔左半 [-1, 0]，圖片 UV.x 0~1 對應完整左半
                    // 圖片跟著簾子一起滑，前緣 = edgeA，尾端 = edgeA - 1
                    float uvAx = saturate((coord - (edgeA - 1.0)));
                    uvA = float2(uvAx, i.uv.y);

                    // B 佔右半 [0, 1]，圖片 UV.x 0~1 對應完整右半
                    float uvBx = saturate(coord - edgeB);
                    uvB = float2(uvBx, i.uv.y);
                }

                // ── 合成 ──
                fixed4 result = fixed4(0, 0, 0, 0);

                if (inA > 0.5)
                {
                    fixed4 colA = tex2D(_CurtainTexA, uvA);
                    colA.rgb *= edgeShadow(coord, edgeA, _ShadowWidth, _ShadowStrength);
                    result = colA;
                }

                if (inB > 0.5)
                {
                    fixed4 colB = tex2D(_CurtainTexB, uvB);
                    colB.rgb *= edgeShadow(coord, edgeB, _ShadowWidth, _ShadowStrength);

                    // B 覆蓋 A
                    result.rgb = lerp(result.rgb, colB.rgb, colB.a);
                    result.a = max(result.a, colB.a);
                }

                return result;
            }
            ENDCG
        }
    }
}
