// 噪點溶解：使用程序化噪點產生溶解效果，無需外部貼圖
Shader "CatzTools/SceneFlow/Dissolve"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _NoiseScale ("噪點密度", Range(1, 50)) = 10
        _Smoothness ("邊緣柔化", Range(0, 0.5)) = 0.05
        _EdgeColor ("溶解邊緣色", Color) = (1, 0.5, 0, 1)
        _EdgeWidth ("邊緣寬度", Range(0, 0.1)) = 0.03
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
            fixed4 _EdgeColor;
            float _Progress;
            float _NoiseScale;
            float _Smoothness;
            float _EdgeWidth;

            // 簡易 2D Hash
            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Value Noise
            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = dot(hash22(i + float2(0, 0)), float2(1, 1)) * 0.5;
                float b = dot(hash22(i + float2(1, 0)), float2(1, 1)) * 0.5;
                float c = dot(hash22(i + float2(0, 1)), float2(1, 1)) * 0.5;
                float d = dot(hash22(i + float2(1, 1)), float2(1, 1)) * 0.5;

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float noise = valueNoise(i.uv * _NoiseScale);

                // Progress 0→1：從無到全覆蓋
                float threshold = _Progress * (1.0 + _Smoothness * 2.0) - _Smoothness;
                float alpha = smoothstep(threshold - _Smoothness, threshold + _Smoothness, noise);
                alpha = 1.0 - alpha;

                // 溶解邊緣高亮
                float edge = smoothstep(threshold - _EdgeWidth, threshold, noise)
                           - smoothstep(threshold, threshold + _EdgeWidth, noise);
                edge = abs(edge);

                fixed3 col = lerp(_Color.rgb, _EdgeColor.rgb, edge * step(0.01, _EdgeWidth));

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
