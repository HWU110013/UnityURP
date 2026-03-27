// 百葉窗：任意角度條紋逐漸覆蓋
Shader "CatzTools/SceneFlow/Blinds"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Strips ("條紋數量", Range(2, 50)) = 8
        _Smoothness ("邊緣柔化", Range(0, 0.5)) = 0.02
        _Angle ("角度", Range(0, 360)) = 0
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

            fixed4 _Color;
            float _Progress;
            float _Strips;
            float _Smoothness;
            float _Angle;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 角度轉弧度，以畫面中心為旋轉基準
                float rad = _Angle * 0.01745329; // PI / 180
                float2 dir = float2(cos(rad), sin(rad));

                // 把 UV 投影到旋轉方向上
                float2 centered = i.uv - 0.5;
                float coord = dot(centered, dir) + 0.5;

                // 條紋內的局部座標 0~1
                float stripLocal = frac(coord * _Strips);
                // 擴展 Progress 範圍，補償 Smoothness 確保 0=全開 1=全閉
                float p = _Progress * (1.0 + _Smoothness * 2.0) - _Smoothness;
                float alpha = smoothstep(p - _Smoothness, p + _Smoothness, stripLocal);
                alpha = 1.0 - alpha;

                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
