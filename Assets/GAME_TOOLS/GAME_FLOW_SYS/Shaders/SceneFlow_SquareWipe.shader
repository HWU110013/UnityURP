// 方格擦除：密度控制大小，支援波紋擴散
Shader "CatzTools/SceneFlow/SquareWipe"
{
    Properties
    {
        _Color ("遮罩顏色", Color) = (0, 0, 0, 1)
        _Progress ("轉場進度", Range(0, 1)) = 0
        _Smoothness ("邊緣柔化", Range(0, 1)) = 0.05
        _Density ("密度（長邊分割數）", Range(1, 50)) = 1
        _OffsetX ("水平偏移", Range(-0.5, 0.5)) = 0
        _OffsetY ("垂直偏移", Range(-0.5, 0.5)) = 0
        _RippleStrength ("波紋強度（0=關閉）", Range(0, 1)) = 0
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
            float _Smoothness;
            float _Density;
            float _OffsetX;
            float _OffsetY;
            float _RippleStrength;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;

                float tileX, tileY;
                if (aspect >= 1.0)
                {
                    tileX = _Density;
                    tileY = ceil(_Density / aspect);
                }
                else
                {
                    tileY = _Density;
                    tileX = ceil(_Density * aspect);
                }

                float2 uv = i.uv + float2(_OffsetX, _OffsetY);
                float2 scaled = uv * float2(tileX, tileY);
                float2 tileId = floor(scaled);
                float2 tiled = frac(scaled);

                // 切比雪夫距離 = 方形
                float2 center = abs(tiled - 0.5);
                float dist = max(center.x, center.y);
                float maxDist = 0.5 + _Smoothness;

                float localProgress = _Progress;
                if (_RippleStrength > 0.001)
                {
                    // 波紋用切比雪夫距離，擴散波也是方形
                    float2 screenCenter = float2(tileX, tileY) * 0.5;
                    float2 diff = abs(tileId + 0.5 - screenCenter);
                    float tileDist = max(diff.x, diff.y);
                    float maxTileDist = max(screenCenter.x, screenCenter.y);
                    float spread = _RippleStrength * 0.9;
                    float delay = (tileDist / max(maxTileDist, 0.001)) * spread;
                    localProgress = saturate((_Progress - delay) / (1.0 - spread));
                }

                float radius = (1.0 - localProgress) * maxDist;
                float alpha = smoothstep(radius - _Smoothness, radius, dist);

                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
