// 燃燒轉場：多起火點，domain warping 模擬洪水填充式不規則蔓延
Shader "CatzTools/SceneFlow/Burn"
{
    Properties
    {
        _Color ("灰燼顏色", Color) = (0.05, 0.02, 0.01, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _FireColor1 ("火焰外緣色", Color) = (1, 0.3, 0, 1)
        _FireColor2 ("火焰內焰色", Color) = (1, 0.85, 0.2, 1)
        _FireWidth ("火焰邊寬度", Range(0.01, 0.2)) = 0.06
        _IgnitionCount ("起火點數量", Range(1, 10)) = 3
        _SpeedVariation ("速度差異", Range(0, 1)) = 0.4
        _NoiseScale ("噪點縮放", Range(2, 20)) = 6
        _Distortion ("蔓延不規則度", Range(0, 2)) = 1.2
        _WarpStrength ("路徑扭曲強度", Range(0, 1.5)) = 0.8
        _CharEdge ("焦化邊寬度", Range(0, 0.1)) = 0.03
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

            float4 _Color;
            float4 _FireColor1;
            float4 _FireColor2;
            float _Progress;
            float _FireWidth;
            float _IgnitionCount;
            float _SpeedVariation;
            float _NoiseScale;
            float _Distortion;
            float _WarpStrength;
            float _CharEdge;

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

            float2 hash2V(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
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

            // FBM 帶種子偏移（每個起火點獨立噪點圖）
            float fbmSeeded(float2 p, float2 seed)
            {
                p += seed;
                float v = 0.0;
                float amp = 0.5;
                float2 shift = float2(100.0, 100.0);
                v += amp * valueNoise(p); p = p * 2.0 + shift; amp *= 0.5;
                v += amp * valueNoise(p); p = p * 2.0 + shift; amp *= 0.5;
                v += amp * valueNoise(p); p = p * 2.0 + shift; amp *= 0.5;
                v += amp * valueNoise(p);
                return v;
            }

            float fbm(float2 p)
            {
                return fbmSeeded(p, float2(0, 0));
            }

            // Domain warping：用噪點扭曲座標，模擬不規則蔓延路徑
            float2 domainWarp(float2 p, float2 seed, float strength)
            {
                float nx = fbmSeeded(p * _NoiseScale, seed);
                float ny = fbmSeeded(p * _NoiseScale + float2(50, 80), seed);
                return p + float2(nx - 0.5, ny - 0.5) * strength;
            }

            float4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;

                int count = max(1, (int)_IgnitionCount);
                float minBurnDist = 100.0;

                // 對每個起火點計算扭曲後的距離
                for (int idx = 0; idx < 10; idx++)
                {
                    if (idx >= count) break;

                    float2 seed = float2((float)idx * 7.31, (float)idx * 13.17);

                    // 隨機起火位置
                    float2 ignPos = hash2V(seed);

                    // 隨機擴散速度
                    float speed = 1.0 + (hash2D(seed + 50.0) - 0.5) * 2.0 * _SpeedVariation;
                    speed = max(speed, 0.2);

                    // Domain warp：用這個起火點專屬的噪點扭曲座標
                    // 模擬火焰沿隨機路徑蔓延（洪水填充感）
                    float2 warpSeed = seed * 3.14;
                    float2 warpedUV = domainWarp(i.uv, warpSeed, _WarpStrength / speed);
                    float2 warpedIgn = domainWarp(ignPos, warpSeed, _WarpStrength / speed);

                    // 扭曲後的距離（修正長寬比）
                    float2 delta = (warpedUV - warpedIgn) * float2(aspect, 1.0);
                    float dist = length(delta) / speed;

                    minBurnDist = min(minBurnDist, dist);
                }

                // 正規化
                float maxDist = length(float2(aspect, 1.0));
                float normDist = minBurnDist / maxDist;

                // 額外噪點擾動（細節層）
                float detailNoise = fbm(i.uv * _NoiseScale * 1.5 + _Time.y * 0.15);
                float distorted = normDist + (detailNoise - 0.5) * _Distortion * 0.3;

                // Progress 映射（放慢蔓延：用 sqrt 讓前期慢後期快）
                float fw = _FireWidth;
                float totalExpand = fw + _Distortion * 0.3 + _WarpStrength * 0.3;
                float p = _Progress * (1.0 + totalExpand) - fw * 0.5;

                // 燃燒遮罩
                float burnMask = 1.0 - smoothstep(p - fw, p, distorted);

                // 火焰帶
                float fireZone = smoothstep(p - fw, p - fw * 0.3, distorted)
                               * (1.0 - smoothstep(p - fw * 0.1, p + fw * 0.2, distorted));

                // 火焰顏色
                float fireGrad = smoothstep(p - fw, p, distorted);
                float3 fireCol = lerp(_FireColor1.rgb, _FireColor2.rgb, fireGrad);

                // 焦化邊
                float charZone = smoothstep(p - fw - _CharEdge, p - fw, distorted)
                               * (1.0 - smoothstep(p - fw, p - fw * 0.3, distorted));
                float3 charCol = float3(0.15, 0.05, 0.01);

                // 灰燼紋理
                float ashNoise = fbm(i.uv * _NoiseScale * 3.0 + 50.0);
                float3 ashCol = _Color.rgb * (0.7 + ashNoise * 0.6);

                // 合成
                float3 col = ashCol * burnMask;
                col = lerp(col, charCol, charZone);
                col = lerp(col, fireCol, fireZone);

                float alpha = saturate(burnMask + fireZone * 0.5);

                return float4(saturate(col), alpha);
            }
            ENDCG
        }
    }
}
