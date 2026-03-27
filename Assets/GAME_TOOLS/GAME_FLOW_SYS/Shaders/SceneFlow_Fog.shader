// 霧氣轉場：濃霧從邊緣湧入覆蓋，帶流動感
Shader "CatzTools/SceneFlow/Fog"
{
    Properties
    {
        _Color ("霧色", Color) = (0.85, 0.88, 0.9, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Smoothness ("邊緣柔化", Range(0, 0.3)) = 0.12
        _NoiseScale ("噪點縮放", Range(1, 15)) = 4
        _FlowSpeed ("流動速度", Range(0, 3)) = 0.8
        _Density ("霧濃度", Range(0.5, 3)) = 1.2
        _LayerCount ("霧層數", Range(1, 4)) = 3
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

            float4 _Color;
            float _Progress;
            float _Smoothness;
            float _NoiseScale;
            float _FlowSpeed;
            float _Density;
            float _LayerCount;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash2D(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 p)
            {
                float2 iv = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash2D(iv);
                float b = hash2D(iv + float2(1, 0));
                float c = hash2D(iv + float2(0, 1));
                float d = hash2D(iv + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                float2 shift = float2(100.0, 100.0);
                v += amp * valueNoise(p); p = p * 2.0 + shift; amp *= 0.5;
                v += amp * valueNoise(p); p = p * 2.0 + shift; amp *= 0.5;
                v += amp * valueNoise(p); p = p * 2.0 + shift; amp *= 0.5;
                v += amp * valueNoise(p);
                return v;
            }

            float4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float t = _Time.y * _FlowSpeed;
                int layers = max(1, (int)_LayerCount);

                // 多層霧疊加（每層速度和偏移不同）
                float fogAlpha = 0.0;

                for (int layer = 0; layer < 4; layer++)
                {
                    if (layer >= layers) break;

                    float layerSeed = (float)layer * 37.7;
                    float layerSpeed = 1.0 + (float)layer * 0.3;

                    // 流動方向（每層不同）
                    float2 flow = float2(
                        sin(layerSeed) * t * layerSpeed * 0.1,
                        cos(layerSeed) * t * layerSpeed * 0.07
                    );

                    float2 noiseUV = i.uv * _NoiseScale * (0.8 + (float)layer * 0.3) + flow;
                    float noise = fbm(noiseUV + layerSeed);

                    // 從邊緣往中心的距離基底
                    float2 edge = min(i.uv, 1.0 - i.uv);
                    edge.x *= aspect;
                    float edgeDist = min(edge.x, edge.y);
                    float maxEdge = min(0.5 * aspect, 0.5);
                    float normDist = edgeDist / maxEdge;

                    // 霧前緣：Progress 決定穿透深度
                    float expand = _Smoothness + 0.3;
                    float p = _Progress * (1.0 + expand) * _Density;
                    float fogMask = noise * p - normDist * (1.0 - _Progress * 0.5);
                    fogMask = smoothstep(-_Smoothness, _Smoothness, fogMask);

                    fogAlpha = max(fogAlpha, fogMask);
                }

                // 霧色微變化
                float colorNoise = fbm(i.uv * 3.0 + t * 0.05);
                float3 col = _Color.rgb * (0.9 + colorNoise * 0.2);

                return float4(saturate(col), saturate(fogAlpha));
            }
            ENDCG
        }
    }
}
