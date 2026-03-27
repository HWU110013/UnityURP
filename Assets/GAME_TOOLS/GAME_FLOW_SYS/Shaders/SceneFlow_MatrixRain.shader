// 矩陣雨轉場：數位綠字從上往下流，逐漸覆蓋畫面
Shader "CatzTools/SceneFlow/MatrixRain"
{
    Properties
    {
        _Color ("底色", Color) = (0, 0, 0, 1)
        _RainColor ("雨色", Color) = (0, 0.8, 0.2, 1)
        _GlowColor ("高光色", Color) = (0.5, 1, 0.6, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _ColumnCount ("列數", Range(10, 100)) = 40
        _Speed ("落下速度", Range(1, 10)) = 4
        _TrailLength ("尾跡長度", Range(0.1, 0.8)) = 0.35
        _Randomness ("隨機延遲", Range(0, 1)) = 0.5
        _CharFlicker ("字元閃爍", Range(0, 1)) = 0.6
        [Toggle] _Reverse ("反向（開場模式）", Float) = 0
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
            float4 _RainColor;
            float4 _GlowColor;
            float _Progress;
            float _ColumnCount;
            float _Speed;
            float _TrailLength;
            float _Randomness;
            float _CharFlicker;
            float _Reverse;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash1(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float hash2(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // 偽字元圖案（用格子噪點模擬）
            float charPattern(float2 cellUV, float seed)
            {
                // 5x7 格子模擬字元
                float2 charGrid = floor(cellUV * float2(5, 7));
                float pattern = hash2(charGrid + seed * 100.0);
                // 只顯示部分格子（模擬筆畫）
                return step(0.4, pattern);
            }

            float4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 列和行的格子
                float cols = max(10.0, _ColumnCount);
                float charH = 1.0 / cols * aspect; // 等寬字元高度
                float rows = 1.0 / charH;

                float2 gridUV = float2(i.uv.x * cols, i.uv.y * rows);
                float2 cellID = floor(gridUV);
                float2 cellUV = frac(gridUV);

                float colID = cellID.x;
                float rowID = cellID.y;

                // 每列隨機延遲
                float colDelay = hash1(colID * 7.31) * _Randomness;

                // 這列的局部 Progress（確保 Progress=1 時所有列都完成）
                float localP = saturate((_Progress - colDelay) / max(1.0 - colDelay, 0.01));

                // 前緣必須跑過所有行 + 尾跡長度
                float trailLen = _TrailLength * rows;
                float totalDist = rows + trailLen;
                float headY = localP * totalDist;
                // 轉成 row 座標（0=頂部, rows=底部）
                float rowFromTop = rows - 1.0 - rowID;

                // 這格距離前緣多遠
                float distFromHead = headY - rowFromTop;

                bool isReverse = _Reverse > 0.5;

                if (distFromHead < 0.0)
                {
                    // 雨還沒到：正常=透明，反向=底色覆蓋（還沒被洗掉）
                    if (isReverse)
                        return float4(_Color.rgb, 1.0);
                    return float4(0, 0, 0, 0);
                }

                if (distFromHead > trailLen)
                {
                    // 雨已過去：正常=底色覆蓋，反向=透明（已露出場景）
                    if (isReverse)
                        return float4(0, 0, 0, 0);
                    return float4(_Color.rgb, 1.0);
                }

                // ── 在尾跡範圍內：顯示雨滴 ──
                float trailPos = distFromHead / trailLen;

                float flickerSeed = hash2(cellID + floor(_Time.y * _Speed * 3.0));
                float flicker = lerp(1.0, flickerSeed, _CharFlicker);

                float charSeed = hash2(cellID + floor(_Time.y * _Speed * 0.5));
                float charMask = charPattern(cellUV, charSeed);

                float brightness = (1.0 - trailPos);
                brightness *= brightness;
                brightness *= flicker;
                brightness *= charMask;

                float headGlow = exp(-trailPos * 8.0);

                float3 rainCol = lerp(_RainColor.rgb, _GlowColor.rgb, headGlow);
                rainCol *= brightness;

                float bgBlend = trailPos * trailPos;

                if (isReverse)
                {
                    // 反向：尾跡前方=底色，後方=透明，中間=雨
                    float3 col = lerp(rainCol, _Color.rgb, 1.0 - bgBlend);
                    col = lerp(float3(0,0,0), col, charMask * brightness + (1.0 - bgBlend));
                    float alpha = saturate(max(brightness, 1.0 - bgBlend));
                    return float4(saturate(col), alpha);
                }
                else
                {
                    // 正常：尾跡前方=雨，後方=底色
                    float3 col = lerp(rainCol, _Color.rgb, bgBlend);
                    col = lerp(_Color.rgb * bgBlend, col, charMask * brightness + bgBlend);
                    float alpha = saturate(max(brightness, bgBlend));
                    return float4(saturate(col), alpha);
                }
            }
            ENDCG
        }
    }
}
