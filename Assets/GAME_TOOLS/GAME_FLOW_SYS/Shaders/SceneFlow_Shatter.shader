// 碎裂轉場：Voronoi 不規則碎片，裂縫浮現 → 爆發四散 + 重力下墜
Shader "CatzTools/SceneFlow/Shatter"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _PieceCount ("碎片密度", Range(3, 40)) = 12
        _CrackWidth ("裂縫寬度", Range(0.005, 0.08)) = 0.025
        _FlyStrength ("爆發力", Range(0, 2)) = 0.8
        _Gravity ("重力", Range(0, 3)) = 1.5
        _ShrinkAmount ("碎片縮小量", Range(0, 0.8)) = 0.3
        _RotateStrength ("旋轉強度", Range(0, 5)) = 2.0
        _Randomness ("隨機延遲", Range(0, 1)) = 0.3
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
            float _PieceCount;
            float _CrackWidth;
            float _FlyStrength;
            float _Gravity;
            float _ShrinkAmount;
            float _RotateStrength;
            float _Randomness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 hash2(float2 p)
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

            // 手動展開 Voronoi 鄰居檢查，避免迴圈編譯問題
            #define CHECK_NEIGHBOR(ox, oy) \
            { \
                float2 nb = float2(ox, oy); \
                float2 pt = hash2(bc + nb); \
                float2 df = nb + pt - lp; \
                float dd = dot(df, df); \
                if (dd < d1) { d2 = d1; d1 = dd; np = (bc + nb + pt) / dens; nid = bc + nb; } \
                else if (dd < d2) { d2 = dd; } \
            }

            float4 frag(v2f i) : SV_Target
            {
                float PI = 3.14159265;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                float2 uv = float2(i.uv.x * aspect, i.uv.y);
                float dens = _PieceCount;
                float2 sc = uv * dens;
                float2 bc = floor(sc);
                float2 lp = frac(sc);

                // Voronoi（用距離平方比較，最後再開根號）
                float d1 = 100.0;
                float d2 = 100.0;
                float2 np = float2(0, 0);
                float2 nid = float2(0, 0);

                CHECK_NEIGHBOR(-1, -1)
                CHECK_NEIGHBOR( 0, -1)
                CHECK_NEIGHBOR( 1, -1)
                CHECK_NEIGHBOR(-1,  0)
                CHECK_NEIGHBOR( 0,  0)
                CHECK_NEIGHBOR( 1,  0)
                CHECK_NEIGHBOR(-1,  1)
                CHECK_NEIGHBOR( 0,  1)
                CHECK_NEIGHBOR( 1,  1)

                d1 = sqrt(d1);
                d2 = sqrt(d2);

                float edgeDist = (d2 - d1) / dens;
                float2 cellCenter = float2(np.x / aspect, np.y);

                float2 rnd = hash2(nid);
                float rndDelay = hash1(nid + 0.5);

                // ── 階段 1：裂縫浮現 ──
                float crackPhase = saturate(_Progress * 4.0);
                float crackMask = smoothstep(0.0, _CrackWidth * crackPhase, edgeDist);

                // ── 階段 2：碎片飛散 ──
                float flyPhase = saturate((_Progress - 0.15) / 0.85);
                float dl = rndDelay * _Randomness;
                float localFly = saturate((flyPhase - dl) / max(1.0 - _Randomness, 0.01));

                // 還沒飛 → 截圖 + 裂縫
                if (localFly <= 0.001)
                {
                    float4 c = tex2D(_ScreenTex, i.uv);
                    c.rgb = lerp(_Color.rgb, c.rgb, crackMask);
                    return float4(c.rgb, 1.0);
                }

                float tt = localFly;

                // ── 爆發初速 ──
                float2 burstDir = cellCenter - 0.5;
                burstDir += (rnd - 0.5) * 0.8;
                float speedMul = 0.5 + rnd.x;
                float2 vel = burstDir * _FlyStrength * speedMul;
                vel.y += (rnd.y - 0.3) * _FlyStrength * 0.5;

                // ── 物理位移 ──
                float2 flyOff;
                flyOff.x = vel.x * tt;
                flyOff.y = vel.y * tt - 0.5 * _Gravity * tt * tt;

                // ── 旋轉 ──
                float ang = (rnd.x - 0.5) * PI * 2.0 * _RotateStrength * tt;
                float ca = cos(ang);
                float sa = sin(ang);

                // ── 縮小 ──
                float scl = max(1.0 - tt * _ShrinkAmount, 0.05);

                float2 fc = i.uv - cellCenter;
                float2 sc2 = fc * scl;
                float2 rot;
                rot.x = sc2.x * ca - sc2.y * sa;
                rot.y = sc2.x * sa + sc2.y * ca;
                float2 sampleUV = clamp(cellCenter + rot - flyOff, 0.001, 0.999);

                float4 screenCol = tex2D(_ScreenTex, sampleUV);

                float scaledCrack = smoothstep(0.0, _CrackWidth / scl, edgeDist);
                screenCol.rgb = lerp(_Color.rgb, screenCol.rgb, scaledCrack);

                float fadeOut = (1.0 - tt * tt) * scaledCrack;
                float colorBlend = tt * _Opacity * 0.2;
                float3 col = lerp(screenCol.rgb, _Color.rgb, colorBlend);

                return float4(col, saturate(fadeOut));
            }
            ENDCG
        }
    }
}
