// 墨水暈染：從中心不規則擴散覆蓋，適合水墨/和風
Shader "CatzTools/SceneFlow/InkSpread"
{
    Properties
    {
        _Color ("墨色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Smoothness ("邊緣柔化", Range(0, 0.15)) = 0.04
        _Distortion ("暈染不規則度", Range(0, 1)) = 0.5
        _NoiseScale ("噪點縮放", Range(1, 20)) = 6
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
            float _Distortion;
            float _NoiseScale;
            float _CenterX;
            float _CenterY;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 2D Simplex-like noise（Value noise + smooth interpolation）
            float hash2D(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash2D(i);
                float b = hash2D(i + float2(1, 0));
                float c = hash2D(i + float2(0, 1));
                float d = hash2D(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // 多層噪點（FBM）模擬墨水暈染的不規則邊緣
            float inkNoise(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                float2 shift = float2(100.0, 100.0);
                for (int i = 0; i < 4; i++)
                {
                    v += amp * valueNoise(p);
                    p = p * 2.0 + shift;
                    amp *= 0.5;
                }
                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 從指定中心計算距離
                float2 delta = i.uv - float2(_CenterX, _CenterY);
                delta.x *= aspect;
                float dist = length(delta);

                // 最大距離（到角落）
                float maxDist = length(float2(
                    max(_CenterX, 1.0 - _CenterX) * aspect,
                    max(_CenterY, 1.0 - _CenterY)));

                // 正規化距離 0~1
                float normDist = dist / max(maxDist, 0.001);

                // 噪點擾動距離（模擬墨水不規則邊緣）
                float noise = inkNoise(i.uv * _NoiseScale);
                float distorted = normDist + (noise - 0.5) * _Distortion;

                // Progress 補償
                float smooth = _Smoothness;
                float p = _Progress * (1.0 + smooth * 2.0 + _Distortion) - smooth;

                // 遮罩：從中心往外擴散
                float alpha = 1.0 - smoothstep(p - smooth, p + smooth, distorted);

                return fixed4(_Color.rgb, saturate(alpha));
            }
            ENDCG
        }
    }
}
