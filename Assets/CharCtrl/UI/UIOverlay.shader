Shader "Custom/UIOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Overlay"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always // 關鍵：強制置頂
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;    // 接收 UI Text 元件上的顏色
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            float4 _Color;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                // 將 Inspector 的 Color 欄位與 UI 頂點顏色相乘
                output.color = input.color * _Color; 
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 關鍵修正：UI Text 的文字形狀主要存在於貼圖的 Alpha 通道
                // 我們只取其 Alpha 值作為字體形狀遮罩
                half textAlpha = _MainTex.Sample(sampler_MainTex, input.uv).a;
                
                // 顏色的 RGB 使用設定好的顏色，Alpha 則乘上字體貼圖的遮罩
                half4 finalColor = input.color;
                finalColor.a *= textAlpha;
                
                // 如果計算後完全透明，就丟棄像素（防止透明區域出錯）
                clip(finalColor.a - 0.001);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}