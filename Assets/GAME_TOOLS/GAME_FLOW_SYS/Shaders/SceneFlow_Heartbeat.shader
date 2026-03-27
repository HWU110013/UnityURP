// 心跳轉場：畫面隨心跳節奏桶狀變形+放大，逐漸變暗至全黑
Shader "CatzTools/SceneFlow/Heartbeat"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _BeatCount ("心跳次數", Range(1, 8)) = 4
        _DistortStrength ("變形強度", Range(0, 0.5)) = 0.2
        _ZoomStrength ("放大強度", Range(0, 0.3)) = 0.1
        _RedShift ("紅色偏移", Range(0, 0.5)) = 0.2
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
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _ScreenTex;
            float4 _Color;
            float _Progress;
            float _Opacity;
            float _BeatCount;
            float _DistortStrength;
            float _ZoomStrength;
            float _RedShift;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float PI = 3.14159265;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // ── 心跳波形：雙峰 lub-dub ──
                float beats = max(1.0, _BeatCount);
                float beatPhase = _Progress * beats;
                float localBeat = frac(beatPhase);

                // 雙峰脈衝
                float peak1 = exp(-pow((localBeat - 0.1) * 12.0, 2.0));
                float peak2 = exp(-pow((localBeat - 0.3) * 12.0, 2.0)) * 0.6;
                float beat = peak1 + peak2;

                // 心跳強度隨次數遞增（越跳越猛）
                float beatIndex = floor(beatPhase);
                float beatIntensity = (beatIndex + 1.0) / beats;
                beat *= beatIntensity;

                // ── 中心座標 ──
                float2 centered = i.uv - 0.5;
                centered.x *= aspect;
                float dist = length(centered);
                float2 dir = centered / max(dist, 0.0001);

                // ── 桶狀變形：心跳時畫面往外膨脹 ──
                float distort = beat * _DistortStrength;
                float2 barrelUV = i.uv - 0.5;
                float r2 = dot(barrelUV * float2(aspect, 1.0), barrelUV * float2(aspect, 1.0));
                barrelUV *= 1.0 + distort * r2;
                barrelUV += 0.5;

                // ── 放大：每次心跳累積放大 ──
                float totalZoom = 1.0 + _Progress * _ZoomStrength * beats;
                float beatZoom = beat * _ZoomStrength;
                float zoom = totalZoom + beatZoom;
                float2 zoomedUV = (barrelUV - 0.5) / zoom + 0.5;
                zoomedUV = clamp(zoomedUV, 0.001, 0.999);

                // ── 取樣 ──
                float3 col = tex2D(_ScreenTex, zoomedUV).rgb;

                // ── 紅色偏移：心跳時偏紅 ──
                float redPulse = beat * _RedShift;
                col.r = min(col.r + redPulse, 1.0);
                col.g *= 1.0 - redPulse * 0.6;
                col.b *= 1.0 - redPulse * 0.8;

                // ── 暗角：隨 Progress 收窄加深 ──
                float vigRadius = lerp(2.0, 0.0, _Progress);
                float vignette = smoothstep(vigRadius, vigRadius + 0.4, dist);
                // 心跳時暗角脈動
                vignette += beat * 0.1;
                vignette = saturate(vignette);

                // ── 整體亮度：逐漸變暗 ──
                float darkness = 1.0 - _Progress * _Progress;
                // 心跳時微亮閃
                darkness += beat * 0.1 * (1.0 - _Progress);
                col *= saturate(darkness);

                // ── 混合遮罩色 ──
                float colorMix = max(vignette, _Progress * _Progress);
                col = lerp(col, _Color.rgb, colorMix * _Opacity);

                // Alpha：確保 Progress=1 時完全遮蔽
                float alpha = saturate(colorMix * _Opacity + _Progress * _Progress);
                alpha *= step(0.001, _Progress);

                return float4(saturate(col), alpha);
            }
            ENDCG
        }
    }
}
