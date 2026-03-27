// 縮放轉場：畫面放大衝進遮罩色，可帶模糊與透明度
Shader "CatzTools/SceneFlow/Zoom"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _ZoomPower ("放大倍率", Range(1.5, 20)) = 5
        _Smoothness ("邊緣柔化", Range(0, 0.5)) = 0.1
        _BlurAmount ("模糊強度", Range(0, 1)) = 0.3
        _Opacity ("不透明度", Range(0, 1)) = 1
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _ScreenTex ("Screen Capture", 2D) = "white" {}
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

            fixed4 _Color;
            float _Progress;
            float _ZoomPower;
            float _Smoothness;
            float _BlurAmount;
            float _Opacity;
            sampler2D _ScreenTex;
            float4 _ScreenTex_TexelSize;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 放射狀模糊取樣：沿中心→像素方向取多點平均
            fixed4 SampleWithBlur(float2 uv, float blurRadius)
            {
                if (blurRadius < 0.001)
                    return tex2D(_ScreenTex, uv);

                float2 dir = uv - 0.5;
                fixed4 col = fixed4(0, 0, 0, 0);
                const int SAMPLES = 8;

                for (int i = 0; i < SAMPLES; i++)
                {
                    float t = (float(i) / float(SAMPLES - 1) - 0.5) * 2.0;
                    float2 offset = dir * t * blurRadius;
                    col += tex2D(_ScreenTex, uv + offset);
                }

                return col / float(SAMPLES);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 放大倍率隨 Progress 指數成長
                float zoom = lerp(1.0, _ZoomPower, _Progress * _Progress);

                // 以畫面中心為放大基準
                float2 zoomedUV = (i.uv - 0.5) / zoom + 0.5;

                // UV 超出範圍 = 放大到看不到的區域
                float inBounds = step(0.0, zoomedUV.x) * step(zoomedUV.x, 1.0)
                               * step(0.0, zoomedUV.y) * step(zoomedUV.y, 1.0);

                // 模糊隨 Progress 增強（放射狀）
                float blur = _BlurAmount * _Progress * 0.15;
                fixed4 screenCol = SampleWithBlur(zoomedUV, blur);

                // 遮罩：邊緣先覆蓋，中心最後
                float2 fromCenter = abs(zoomedUV - 0.5) * 2.0;
                float edgeDist = max(fromCenter.x, fromCenter.y);
                float p = _Progress * (1.0 + _Smoothness * 2.0) - _Smoothness;
                float mask = smoothstep(1.0 - p - _Smoothness, 1.0 - p + _Smoothness, edgeDist);

                // 超出範圍直接遮罩色
                mask = max(mask, 1.0 - inBounds);

                // 遮罩色覆蓋程度由不透明度控制
                float colorMask = mask * _Opacity;

                fixed4 result;
                result.rgb = lerp(screenCol.rgb, _Color.rgb, colorMask);

                // 截圖區域（inBounds）永遠可見
                // 出界區域由不透明度決定是遮罩色還是透明
                // Opacity=1：出界=黑（正常轉場）
                // Opacity=0：出界=透明（看到底下場景，放大效果浮在上面）
                result.a = saturate(inBounds + colorMask);

                return result;
            }
            ENDCG
        }
    }
}
