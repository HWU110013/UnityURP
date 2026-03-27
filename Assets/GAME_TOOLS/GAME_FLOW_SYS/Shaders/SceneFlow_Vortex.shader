// 漩渦轉場：螺旋臂從外向中心旋轉覆蓋
Shader "CatzTools/SceneFlow/Vortex"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Smoothness ("邊緣柔化", Range(0, 0.5)) = 0.05
        _Arms ("螺旋臂數", Range(1, 12)) = 3
        _Twist ("旋轉圈數", Range(0.5, 10)) = 2
        [KeywordEnum(CW, CCW)] _DIR ("旋轉方向", Float) = 0
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
            #define TAU 6.28318530

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            #pragma multi_compile _DIR_CW _DIR_CCW

            fixed4 _Color;
            float _Progress;
            float _Smoothness;
            float _Arms;
            float _Twist;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 中心座標（修正長寬比）
                float2 center = i.uv - 0.5;
                center.x *= aspect;

                // 極座標
                float dist = length(center);
                float maxDist = length(float2(aspect * 0.5, 0.5));
                float normDist = dist / maxDist; // 0=中心 1=角落
                float angle = atan2(center.y, center.x); // -PI ~ PI

                // 方向
                #ifdef _DIR_CCW
                float dir = -1.0;
                #else
                float dir = 1.0;
                #endif

                // 螺旋：角度隨距離旋轉
                float arms = max(round(_Arms), 1.0);
                float spiralAngle = angle * dir + normDist * _Twist * TAU;

                // 將角度映射到 0~1 的鋸齒波（每個臂一個週期）
                float wave = frac(spiralAngle * arms / TAU);

                // 擴展 Progress 範圍，補償 Smoothness 確保 0=全開 1=全閉
                float p = _Progress * (1.0 + _Smoothness * 2.0) - _Smoothness;

                float alpha = smoothstep(p - _Smoothness, p + _Smoothness, wave);
                // 反轉：wave 小的先被覆蓋
                alpha = 1.0 - alpha;

                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
