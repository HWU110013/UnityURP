// 風吹轉場：畫面像紙被風吹散，像素往風向飄散消失
Shader "CatzTools/SceneFlow/WindBlow"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _WindAngle ("風向角度", Range(0, 360)) = 0
        _WindStrength ("風力", Range(0, 0.3)) = 0.12
        _NoiseScale ("粒子大小", Range(5, 40)) = 15
        _Turbulence ("亂流", Range(0, 1)) = 0.5
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
            float _WindAngle;
            float _WindStrength;
            float _NoiseScale;
            float _Turbulence;

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
                float PI = 3.14159265;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 風向
                float rad = _WindAngle * 0.01745329;
                float2 windDir = float2(cos(rad), sin(rad));

                // 風向軸座標（像素沿風向的位置，0~1）
                float2 centered = i.uv - 0.5;
                centered.x *= aspect;
                float windCoord = dot(centered, windDir);
                float halfSpan = (abs(windDir.x) * aspect + abs(windDir.y)) * 0.5;
                float normWind = windCoord / halfSpan * 0.5 + 0.5; // 0=逆風端, 1=順風端

                // 粒子噪點（決定每個粒子何時開始飄散）
                float particleNoise = fbm(i.uv * _NoiseScale);

                // 亂流偏移（讓飄散邊緣不規則）
                float turbNoise = fbm(i.uv * _NoiseScale * 0.7 + 30.0);
                float turbOffset = (turbNoise - 0.5) * _Turbulence;

                // 飄散門檻：逆風端先飄（normWind 小的先走）
                // 加入噪點和亂流讓邊緣不規則
                float threshold = normWind + particleNoise * 0.3 + turbOffset * 0.2;
                threshold = threshold / 1.5; // 正規化到大致 0~1

                // Progress 映射
                float p = _Progress * 1.4; // 稍微超過確保全部飄完

                // 每個像素的局部飄散進度
                float localP = saturate((p - threshold) / 0.3);

                // 還沒開始飄 → 顯示遮罩色底（先鋪底防穿幫）
                if (localP <= 0.001)
                {
                    // 即將被吹到的區域先顯示遮罩色底
                    float preP = saturate((p - threshold + 0.05) / 0.1);
                    if (preP > 0.001)
                        return float4(_Color.rgb, preP * _Opacity);
                    return float4(0, 0, 0, 0);
                }

                // ── 風吹位移 ──
                float flyT = localP * localP;

                // 主風向位移
                float2 mainOffset = windDir * flyT * _WindStrength;
                // 亂流橫向擺動
                float perpAngle = rad + PI * 0.5;
                float2 perpDir = float2(cos(perpAngle), sin(perpAngle));
                float sway = sin(localP * PI * 3.0 + particleNoise * 10.0) * _Turbulence * 0.3;
                float2 turbOffset2 = perpDir * sway * flyT * _WindStrength;

                float2 totalOffset = mainOffset + turbOffset2;

                // 取樣（反向偏移）
                float2 sampleUV = i.uv - totalOffset;
                sampleUV = clamp(sampleUV, 0.001, 0.999);
                float3 col = tex2D(_ScreenTex, sampleUV).rgb;

                // ── 淡出：飄遠後露出遮罩色 ──
                float fadeOut = 1.0 - flyT;

                // 粒子邊緣碎片化
                float dissolve = step(particleNoise, 1.0 - localP * 0.8);

                // 已飄走的區域顯示遮罩色
                float isBehind = 1.0 - fadeOut * dissolve;

                // 混合：飄散中的粒子 + 背後的遮罩色
                float3 finalCol = lerp(col, _Color.rgb, isBehind);
                float alpha = saturate(max(fadeOut * dissolve, isBehind * _Opacity));

                return float4(finalCol, alpha);
            }
            ENDCG
        }
    }
}
