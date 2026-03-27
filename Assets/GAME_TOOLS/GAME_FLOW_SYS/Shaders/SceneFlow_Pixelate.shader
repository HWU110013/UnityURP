// 馬賽克轉場：擷取畫面後以畫面中心為基準逐漸增大像素格，最終覆蓋為純色
Shader "CatzTools/SceneFlow/Pixelate"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _MaxPixelSize ("最大像素格", Range(8, 256)) = 96
        _ScreenTex ("擷取畫面（自動設定）", 2D) = "black" {}
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
            float4 _ScreenTex_TexelSize;
            fixed4 _Color;
            float _Progress;
            float _MaxPixelSize;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 前 80%：像素化逐漸加粗
                // 後 20%：畫面淡出為純色
                float pixelPhase = saturate(_Progress / 0.8);
                float fadePhase = saturate((_Progress - 0.8) / 0.2);

                // 像素格大小：1（原始）→ _MaxPixelSize（平方曲線讓前段變化自然）
                float pixelSize = lerp(1.0, _MaxPixelSize, pixelPhase * pixelPhase);

                // 取螢幕解析度
                float2 texSize = _ScreenTex_TexelSize.zw;

                // 以畫面中心為基準的像素化
                // 將 UV 偏移到以中心為原點 → 量化 → 偏移回來
                float2 centered = (i.uv - 0.5) * texSize;
                float2 quantized = floor(centered / pixelSize + 0.5) * pixelSize;
                float2 pixelUV = quantized / texSize + 0.5;

                fixed4 screen = tex2D(_ScreenTex, pixelUV);

                // 混合：馬賽克畫面 → 純色
                fixed3 col = lerp(screen.rgb, _Color.rgb, fadePhase);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
