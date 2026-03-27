// 拉絲轉場：沿任意角度的方向性擦除，帶細絲拖尾效果
Shader "CatzTools/SceneFlow/Streak"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Angle ("角度", Range(0, 360)) = 0
        _Smoothness ("邊緣柔化", Range(0, 0.5)) = 0.1
        _StreakDensity ("絲線密度", Range(1, 100)) = 30
        _StreakStrength ("絲線強度", Range(0, 1)) = 0.5
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
            float _Angle;
            float _Smoothness;
            float _StreakDensity;
            float _StreakStrength;

            // 簡易 hash
            float hash(float n) { return frac(sin(n) * 43758.5453); }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float rad = _Angle * 0.01745329;
                float2 dir = float2(cos(rad), sin(rad));
                // 垂直於擦除方向（用於絲線排列）
                float2 perp = float2(-dir.y, dir.x);

                float2 centered = i.uv - 0.5;

                // 沿擦除方向的進度座標（-0.7 ~ 0.7 左右）
                float along = dot(centered, dir);
                // 垂直方向的座標（用於產生每條絲的偏移）
                float across = dot(centered, perp);

                // 歸一化到 0~1
                float maxDist = abs(dir.x) * 0.5 + abs(dir.y) * 0.5 + 0.01;
                float normalizedAlong = (along / maxDist) * 0.5 + 0.5;

                // 每條絲線有不同的長度偏移
                float streakId = floor(across * _StreakDensity);
                float streakOffset = hash(streakId) * _StreakStrength;

                // 擴展 Progress 確保 0=全開 1=全閉（補償柔化+絲線偏移）
                float expand = _Smoothness + _StreakStrength;
                float threshold = _Progress * (1.0 + expand) - streakOffset;

                float alpha = smoothstep(
                    threshold - _Smoothness,
                    threshold + _Smoothness,
                    normalizedAlong);

                // 反轉：0 = 透明，1 = 遮罩
                alpha = 1.0 - alpha;

                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
