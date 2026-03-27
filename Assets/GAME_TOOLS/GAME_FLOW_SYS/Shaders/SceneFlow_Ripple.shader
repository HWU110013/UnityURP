// 水波紋轉場：從中心擴散的同心波紋扭曲畫面，逐漸淡入遮罩色
Shader "CatzTools/SceneFlow/Ripple"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _WaveCount ("波紋數量", Range(1, 30)) = 8
        _WaveStrength ("扭曲強度", Range(0, 0.15)) = 0.04
        _WaveSpeed ("波紋速度", Range(0, 10)) = 3
        _CenterX ("中心 X", Range(0, 1)) = 0.5
        _CenterY ("中心 Y", Range(0, 1)) = 0.5
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
            fixed4 _Color;
            float _Progress;
            float _Opacity;
            float _WaveCount;
            float _WaveStrength;
            float _WaveSpeed;
            float _CenterX;
            float _CenterY;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                const float PI = 3.14159265;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 從指定中心計算距離（修正長寬比）
                float2 center = float2(_CenterX, _CenterY);
                float2 delta = i.uv - center;
                delta.x *= aspect;
                float dist = length(delta);
                float2 dir = normalize(delta + 0.0001); // 防止 (0,0) 除零

                // 波紋隨 Progress 從中心往外擴散
                // waveRadius：波紋前緣位置，隨 Progress 擴大
                float maxRadius = length(float2(max(_CenterX, 1.0 - _CenterX) * aspect,
                                                max(_CenterY, 1.0 - _CenterY)));
                float waveRadius = _Progress * maxRadius * 1.5;

                // 只在波紋已到達的區域產生扭曲（波紋前緣往外擴散）
                float waveMask = smoothstep(waveRadius, waveRadius - 0.15, dist);

                // 扭曲強度隨 Progress 先增後減（中段最強）
                float progressCurve = sin(_Progress * PI);
                float strength = _WaveStrength * progressCurve;

                // 正弦波：距離越遠波紋越密
                float wave = sin(dist * _WaveCount * PI * 2.0 - _Time.y * _WaveSpeed);
                float displacement = wave * strength * waveMask;

                // 扭曲 UV（沿徑向位移）
                float2 distortedUV = i.uv + dir * displacement / float2(aspect, 1.0);
                distortedUV = clamp(distortedUV, 0.001, 0.999);

                // 取樣擷取畫面
                fixed4 screenCol = tex2D(_ScreenTex, distortedUV);

                // 遮罩：從中心向外逐漸覆蓋
                float coverRadius = _Progress * maxRadius * 1.3;
                float coverMask = smoothstep(coverRadius - 0.2, coverRadius, dist);
                float colorBlend = (1.0 - coverMask) * _Progress * _Progress;

                // 混合截圖與遮罩色
                fixed4 result;
                result.rgb = lerp(screenCol.rgb, _Color.rgb, colorBlend * _Opacity);

                // Alpha：有扭曲或有遮罩色的區域都要顯示
                float distortAlpha = (1.0 - waveMask) < 0.99 ? 1.0 : 0.0;
                float colorAlpha = colorBlend * _Opacity;
                result.a = saturate(max(distortAlpha, colorAlpha));

                // Progress=0 時完全透明
                result.a *= step(0.001, _Progress);

                return result;
            }
            ENDCG
        }
    }
}
