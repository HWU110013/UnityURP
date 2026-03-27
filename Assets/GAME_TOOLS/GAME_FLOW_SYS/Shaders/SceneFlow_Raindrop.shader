// 雨滴轉場：水滴落在鏡頭上，擴散模糊覆蓋畫面
Shader "CatzTools/SceneFlow/Raindrop"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _DropCount ("水滴密度", Range(3, 30)) = 12
        _DistortStrength ("扭曲強度", Range(0, 0.1)) = 0.03
        _BlurAmount ("模糊量", Range(0, 0.05)) = 0.015
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _ScreenTex;
            float4 _Color;
            float _Progress;
            float _Opacity;
            float _DropCount;
            float _DistortStrength;
            float _BlurAmount;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 hash2V(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float hash1(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float PI = 3.14159265;

                // 累積水滴扭曲和模糊
                float2 totalDistort = float2(0, 0);
                float totalWet = 0.0;
                int count = max(3, (int)_DropCount);

                for (int idx = 0; idx < 30; idx++)
                {
                    if (idx >= count) break;

                    float2 seed = float2((float)idx * 7.31, (float)idx * 13.17);
                    float2 dropPos = hash2V(seed);
                    float dropDelay = hash1(seed + 50.0) * 0.7;
                    float dropSize = 0.05 + hash1(seed + 100.0) * 0.1;

                    // 水滴何時出現
                    float dropProgress = saturate((_Progress - dropDelay) / max(1.0 - dropDelay * 0.5, 0.01));
                    if (dropProgress <= 0.0) continue;

                    // 水滴擴散半徑
                    float radius = dropSize * dropProgress;

                    // 到水滴中心的距離
                    float2 delta = (i.uv - dropPos) * float2(aspect, 1.0);
                    float dist = length(delta);

                    if (dist > radius * 2.0) continue;

                    // 水滴內的扭曲（折射效果）
                    float dropMask = 1.0 - smoothstep(radius * 0.7, radius, dist);
                    float2 dir = normalize(delta + 0.0001);
                    totalDistort += dir * dropMask * _DistortStrength * dropProgress;

                    // 濕潤區域
                    totalWet = max(totalWet, dropMask * dropProgress);
                }

                // 套用扭曲
                float2 distortedUV = clamp(i.uv + totalDistort, 0.001, 0.999);

                // 模糊取樣
                float blur = _BlurAmount * _Progress;
                float3 col = float3(0, 0, 0);
                float2 offsets[8];
                offsets[0] = float2(1, 0); offsets[1] = float2(-1, 0);
                offsets[2] = float2(0, 1); offsets[3] = float2(0, -1);
                offsets[4] = float2(0.7, 0.7); offsets[5] = float2(-0.7, 0.7);
                offsets[6] = float2(0.7, -0.7); offsets[7] = float2(-0.7, -0.7);

                col += tex2D(_ScreenTex, distortedUV).rgb;
                for (int s = 0; s < 8; s++)
                {
                    float2 sUV = clamp(distortedUV + offsets[s] * blur, 0.001, 0.999);
                    col += tex2D(_ScreenTex, sUV).rgb;
                }
                col /= 9.0;

                // 遮罩色混合
                float colorBlend = _Progress * _Progress * _Opacity;
                col = lerp(col, _Color.rgb, colorBlend);

                float alpha = saturate(max(totalWet * 0.5 + _Progress * 0.3, colorBlend));
                alpha *= step(0.001, _Progress);

                return float4(col, alpha);
            }
            ENDCG
        }
    }
}
