// 雙重曝光轉場：新舊場景疊影漸變，帶亮度增強的攝影感
Shader "CatzTools/SceneFlow/DoubleExposure"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _BlendMode ("混合模式", Range(0, 1)) = 0.5
        _Brightness ("曝光亮度", Range(0.5, 3)) = 1.3
        _Contrast ("對比度", Range(0.5, 2)) = 1.1
        _Desaturate ("去飽和度", Range(0, 1)) = 0.3
        _ScreenTex ("擷取畫面（舊場景）", 2D) = "white" {}
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
            float _BlendMode;
            float _Brightness;
            float _Contrast;
            float _Desaturate;

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

                float3 oldScene = tex2D(_ScreenTex, i.uv).rgb;

                // 曝光強度曲線：中段最亮
                float intensity = sin(_Progress * PI);

                // 去飽和（模擬過曝的褪色感）
                float luma = dot(oldScene, float3(0.299, 0.587, 0.114));
                float3 gray = float3(luma, luma, luma);
                float desat = _Desaturate * intensity;
                float3 desaturated = lerp(oldScene, gray, desat);

                // 曝光亮度
                float bright = 1.0 + ((_Brightness - 1.0) * intensity);
                desaturated *= bright;

                // 對比度
                float contrast = lerp(1.0, _Contrast, intensity);
                desaturated = (desaturated - 0.5) * contrast + 0.5;

                // Screen blend with mask color（模擬底片疊影）
                float3 screenBlend = 1.0 - (1.0 - desaturated) * (1.0 - _Color.rgb * _Progress);

                // Multiply blend
                float3 multiplyBlend = desaturated * lerp(float3(1,1,1), _Color.rgb, _Progress);

                // 混合兩種模式
                float3 blended = lerp(multiplyBlend, screenBlend, _BlendMode);

                // 最終淡出到遮罩色
                float fadeOut = _Progress * _Progress;
                float3 col = lerp(blended, _Color.rgb, fadeOut);

                // Alpha
                float alpha = saturate(_Progress * 0.5 + intensity * 0.3 + fadeOut);
                alpha *= step(0.001, _Progress);

                return float4(saturate(col), alpha);
            }
            ENDCG
        }
    }
}
