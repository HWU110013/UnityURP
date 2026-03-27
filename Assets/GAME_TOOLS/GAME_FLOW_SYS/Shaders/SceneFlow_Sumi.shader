// 水墨畫轉場：毛筆筆觸從邊緣刷過覆蓋畫面
Shader "CatzTools/SceneFlow/Sumi"
{
    Properties
    {
        _Color ("墨色", Color) = (0.02, 0.02, 0.03, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Smoothness ("邊緣柔化", Range(0, 0.15)) = 0.04
        _BrushScale ("筆觸大小", Range(2, 15)) = 5
        _BrushDetail ("筆觸細節", Range(1, 10)) = 4
        _InkSpread ("暈染擴散", Range(0, 1)) = 0.5
        _BrushAngle ("筆刷角度", Range(0, 360)) = 30
        _EdgeWet ("邊緣濕潤", Range(0, 1)) = 0.6
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
            float _BrushScale;
            float _BrushDetail;
            float _InkSpread;
            float _BrushAngle;
            float _EdgeWet;

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

            // 毛筆筆觸 FBM：用 domain warping 模擬纖維方向性
            float brushFBM(float2 p, float angle)
            {
                float rad = angle * 0.01745329;
                float ca = cos(rad);
                float sa = sin(rad);

                // 筆觸方向拉伸（模擬毛筆纖維）
                float2 stretched = float2(
                    p.x * ca + p.y * sa,
                    (-p.x * sa + p.y * ca) * 0.4
                );

                float v = 0.0;
                float amp = 0.5;
                float2 shift = float2(100.0, 100.0);
                float detail = max(1.0, _BrushDetail);

                for (int i = 0; i < 5; i++)
                {
                    if ((float)i >= detail) break;
                    v += amp * valueNoise(stretched);
                    stretched = stretched * 2.1 + shift;
                    amp *= 0.45;
                }
                return v;
            }

            // 暈染 domain warp
            float2 inkWarp(float2 p, float strength)
            {
                float nx = valueNoise(p * 3.0 + float2(50, 0));
                float ny = valueNoise(p * 3.0 + float2(0, 80));
                return p + float2(nx - 0.5, ny - 0.5) * strength;
            }

            float4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 筆刷方向座標
                float rad = _BrushAngle * 0.01745329;
                float2 centered = i.uv - 0.5;
                centered.x *= aspect;
                float brushCoord = dot(centered, float2(cos(rad), sin(rad)));
                float halfSpan = (abs(cos(rad)) * aspect + abs(sin(rad))) * 0.5;
                float normCoord = brushCoord / halfSpan * 0.5 + 0.5;

                // 暈染扭曲
                float2 warpedUV = inkWarp(i.uv, _InkSpread * 0.15);

                // 筆觸噪點
                float brush = brushFBM(warpedUV * _BrushScale, _BrushAngle);

                // 筆觸邊緣不規則度（毛邊）
                float edgeNoise = brushFBM(warpedUV * _BrushScale * 2.0 + 30.0, _BrushAngle + 45.0);

                // 合併：筆觸噪點擾動推進前緣
                float distorted = normCoord + (brush - 0.5) * 0.4 + (edgeNoise - 0.5) * 0.15;

                // Progress 補償
                float sm = _Smoothness;
                float totalExpand = sm + 0.4 + 0.15;
                float p = _Progress * (1.0 + totalExpand) - sm;

                // 主遮罩
                float inkMask = 1.0 - smoothstep(p - sm, p + sm, distorted);

                // 邊緣濕潤效果（墨水邊緣微暈開的半透明帶）
                float wetZone = smoothstep(p - sm * 3.0, p - sm, distorted)
                              * (1.0 - smoothstep(p - sm, p + sm * 2.0, distorted));
                float wetAlpha = wetZone * _EdgeWet * 0.5;

                // 墨色微變化（模擬墨水濃淡不均）
                float inkVariation = brushFBM(i.uv * _BrushScale * 1.5 + 50.0, 0.0);
                float3 col = _Color.rgb * (0.85 + inkVariation * 0.3);

                // 邊緣略淡（墨水邊緣比中心淡）
                float3 wetCol = _Color.rgb * 1.3;

                float3 finalCol = lerp(col, wetCol, wetZone * _EdgeWet);
                float alpha = saturate(inkMask + wetAlpha);

                return float4(saturate(finalCol), alpha);
            }
            ENDCG
        }
    }
}
