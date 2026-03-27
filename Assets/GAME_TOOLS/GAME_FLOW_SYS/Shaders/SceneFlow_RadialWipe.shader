// 時鐘擦除：像時鐘指針從起始角度掃一圈覆蓋畫面
Shader "CatzTools/SceneFlow/RadialWipe"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Smoothness ("邊緣柔化", Range(0, 0.1)) = 0.02
        _StartAngle ("起始角度", Range(0, 360)) = 90
        [Enum(Clockwise,0,CounterClockwise,1)] _Direction ("方向", Float) = 0
        _CenterX ("中心 X", Range(0, 1)) = 0.5
        _CenterY ("中心 Y", Range(0, 1)) = 0.5
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
            float _StartAngle;
            float _Direction;
            float _CenterX;
            float _CenterY;

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
                const float TWO_PI = 6.28318530;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 從指定中心計算角度
                float2 delta = i.uv - float2(_CenterX, _CenterY);
                delta.x *= aspect;

                // atan2 回傳 -PI ~ PI，轉成 0 ~ 1
                float angle = atan2(delta.y, delta.x); // -PI ~ PI
                angle = angle / TWO_PI + 0.5; // 0 ~ 1

                // 套用起始角度偏移
                float startOffset = _StartAngle / 360.0;
                angle = frac(angle - startOffset + 1.0);

                // 方向：1=逆時針，翻轉角度
                if (_Direction > 0.5)
                    angle = 1.0 - angle;

                // Progress 補償 Smoothness 確保完整覆蓋
                float smooth = _Smoothness;
                float p = _Progress * (1.0 + smooth * 2.0) - smooth;

                // 扇形遮罩
                float alpha = 1.0 - smoothstep(p - smooth, p + smooth, angle);

                return fixed4(_Color.rgb, saturate(alpha));
            }
            ENDCG
        }
    }
}
