// 雙向百葉窗：涵蓋拉簾、百葉窗、交叉線三種效果
// A條數=1 B關閉=拉簾 / A多條 B關閉=百葉窗 / AB都開=交叉線
Shader "CatzTools/SceneFlow/DualBlinds"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Smoothness ("邊緣柔化", Range(0, 0.5)) = 0.02
        _StripsA ("A 條紋數量", Range(1, 50)) = 8
        _AngleA ("A 角度", Range(0, 360)) = 0
        _StripsB ("B 條紋數量（0=關閉）", Range(0, 50)) = 0
        _AngleB ("B 角度", Range(0, 360)) = 90
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

            fixed4 _Color;
            float _Progress;
            float _Smoothness;
            float _StripsA;
            float _AngleA;
            float _StripsB;
            float _AngleB;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 單方向百葉窗遮罩計算
            float blindsMask(float2 uv, float angle, float strips, float progress, float smooth)
            {
                float rad = angle * 0.01745329;
                float2 dir = float2(cos(rad), sin(rad));
                float2 centered = uv - 0.5;
                float coord = dot(centered, dir) + 0.5;

                // 擴展 Progress 補償 Smoothness
                float p = progress * (1.0 + smooth * 2.0) - smooth;

                if (strips <= 1.01)
                {
                    // 條數=1：整面拉簾，coord 0~1 直接當進度軸
                    return 1.0 - smoothstep(p - smooth, p + smooth, coord);
                }

                // 多條：條紋內局部座標
                float stripLocal = frac(coord * strips);
                float alpha = smoothstep(p - smooth, p + smooth, stripLocal);
                return 1.0 - alpha;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // A 方向遮罩
                float maskA = blindsMask(i.uv, _AngleA, _StripsA, _Progress, _Smoothness);

                // B 方向遮罩（strips <= 0 視為關閉）
                float maskB = 0.0;
                if (_StripsB > 0.5)
                    maskB = blindsMask(i.uv, _AngleB, _StripsB, _Progress, _Smoothness);

                // 聯集：任一方向覆蓋就覆蓋
                float alpha = max(maskA, maskB);

                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
