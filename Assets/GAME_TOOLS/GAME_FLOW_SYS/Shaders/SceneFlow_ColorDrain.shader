// 色彩抽離轉場：畫面逐漸褪色→灰階→淡入遮罩色
Shader "CatzTools/SceneFlow/ColorDrain"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Opacity ("不透明度", Range(0, 1)) = 1
        _DrainSpeed ("褪色速度", Range(0.5, 3)) = 1.2
        _Contrast ("灰階對比度", Range(0, 2)) = 1
        _Brightness ("灰階亮度偏移", Range(-0.5, 0.5)) = 0
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
            float _DrainSpeed;
            float _Contrast;
            float _Brightness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 screenCol = tex2D(_ScreenTex, i.uv);

                // 灰階（人眼加權）
                float luma = dot(screenCol.rgb, float3(0.299, 0.587, 0.114));

                // 對比度 + 亮度調整
                luma = (luma - 0.5) * _Contrast + 0.5 + _Brightness;
                luma = saturate(luma);
                fixed3 gray = fixed3(luma, luma, luma);

                // 階段 1（0~0.5）：彩色→灰階
                // 階段 2（0.5~1）：灰階→遮罩色
                float drainPhase = saturate(_Progress * _DrainSpeed * 2.0);
                float fadePhase = saturate((_Progress - 0.4) * 2.5);

                // 彩色→灰階
                fixed3 drained = lerp(screenCol.rgb, gray, drainPhase);

                // 灰階→遮罩色
                fixed3 result = lerp(drained, _Color.rgb, fadePhase * _Opacity);

                // Alpha：有任何效果就顯示
                float alpha = saturate(max(drainPhase * 0.3, fadePhase * _Opacity));
                alpha = max(alpha, _Progress * 0.5);
                alpha *= step(0.001, _Progress);

                return fixed4(result, alpha);
            }
            ENDCG
        }
    }
}
