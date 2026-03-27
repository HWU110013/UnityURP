// 稜鏡轉場：RGB 三色分離偏移，各自往不同方向飄散後淡出
Shader "CatzTools/SceneFlow/Prism"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _SplitStrength ("分離強度", Range(0, 0.2)) = 0.08
        _SplitAngle ("分離角度", Range(0, 360)) = 0
        _Aberration ("色差擴散", Range(0, 0.1)) = 0.03
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
            float _SplitStrength;
            float _SplitAngle;
            float _Aberration;

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

                // 強度曲線：前段分離加速，後段淡出
                float splitT = pow(_Progress, 0.7);
                float fadeT = _Progress * _Progress;

                // 三色各自偏移方向（相隔 120 度）
                float baseRad = _SplitAngle * 0.01745329;
                float2 dirR = float2(cos(baseRad), sin(baseRad));
                float2 dirG = float2(cos(baseRad + PI * 0.667), sin(baseRad + PI * 0.667));
                float2 dirB = float2(cos(baseRad + PI * 1.333), sin(baseRad + PI * 1.333));

                float strength = splitT * _SplitStrength;

                // 從中心往外的色差擴散
                float2 centered = i.uv - 0.5;
                float distFromCenter = length(centered);
                float aberration = distFromCenter * splitT * _Aberration;

                // 三色各自偏移取樣
                float2 uvR = clamp(i.uv + dirR * strength + normalize(centered + 0.0001) * aberration, 0.001, 0.999);
                float2 uvG = clamp(i.uv + dirG * strength, 0.001, 0.999);
                float2 uvB = clamp(i.uv + dirB * strength - normalize(centered + 0.0001) * aberration, 0.001, 0.999);

                float r = tex2D(_ScreenTex, uvR).r;
                float g = tex2D(_ScreenTex, uvG).g;
                float b = tex2D(_ScreenTex, uvB).b;

                float3 col = float3(r, g, b);

                // 亮度增強（稜鏡折射的過曝感）
                float brightBoost = 1.0 + splitT * 0.5;
                col *= brightBoost;

                // 淡出到遮罩色
                col = lerp(col, _Color.rgb, fadeT * _Opacity);

                // Alpha
                float alpha = saturate(max(splitT * 0.4, fadeT * _Opacity));
                alpha *= step(0.001, _Progress);

                return float4(saturate(col), alpha);
            }
            ENDCG
        }
    }
}
