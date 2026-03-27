// 萬花筒轉場：畫面鏡射成萬花筒圖案，旋轉縮小後消失
Shader "CatzTools/SceneFlow/Kaleidoscope"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _Segments ("鏡射分割數", Range(2, 16)) = 6
        _RotateSpeed ("旋轉速度", Range(0, 5)) = 1.5
        _ZoomSpeed ("縮放速度", Range(0, 3)) = 1.0
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
            float _Segments;
            float _RotateSpeed;
            float _ZoomSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float PI = 3.14159265;
                float TWO_PI = 6.28318530;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 萬花筒強度隨 Progress 增加
                float intensity = sin(_Progress * PI); // 中段最強
                float kStrength = _Progress;

                // 中心座標
                float2 centered = i.uv - 0.5;
                centered.x *= aspect;

                // 旋轉（隨 Progress 和時間）
                float angle = kStrength * _RotateSpeed * TWO_PI;
                float ca = cos(angle);
                float sa = sin(angle);
                float2 rotated = float2(
                    centered.x * ca - centered.y * sa,
                    centered.x * sa + centered.y * ca
                );

                // 縮放（越來越小，萬花筒效果越密集）
                float zoom = 1.0 + kStrength * _ZoomSpeed;
                rotated *= zoom;

                // 極座標
                float r = length(rotated);
                float theta = atan2(rotated.y, rotated.x);

                // 萬花筒鏡射：角度限制在一個扇區內並鏡射
                float segments = max(2.0, floor(_Segments));
                float segAngle = TWO_PI / segments;

                // 把角度折入第一個扇區
                theta = theta + PI; // 0 ~ TWO_PI
                float segID = floor(theta / segAngle);
                float localAngle = theta - segID * segAngle;

                // 鏡射：奇數扇區翻轉
                float odd = fmod(segID, 2.0);
                if (odd > 0.5)
                    localAngle = segAngle - localAngle;

                // 混合：原始 UV 和萬花筒 UV
                float2 kaleidUV;
                float2 kaleidCart = float2(cos(localAngle - PI), sin(localAngle - PI)) * r;
                kaleidCart.x /= aspect;
                kaleidUV = kaleidCart + 0.5;

                // 原始和萬花筒之間插值
                float2 sampleUV = lerp(i.uv, kaleidUV, kStrength);
                sampleUV = frac(sampleUV); // wrap
                sampleUV = clamp(sampleUV, 0.001, 0.999);

                float3 screenCol = tex2D(_ScreenTex, sampleUV).rgb;

                // 遮罩色混合
                float colorBlend = _Progress * _Progress * _Opacity;
                float3 col = lerp(screenCol, _Color.rgb, colorBlend);

                // Alpha
                float alpha = saturate(max(kStrength * 0.5, colorBlend));
                alpha *= step(0.001, _Progress);

                return float4(col, alpha);
            }
            ENDCG
        }
    }
}
