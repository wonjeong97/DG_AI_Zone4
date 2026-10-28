// 모서리를 깎은 패널 테두리 바깥으로 번지는 빛(아우라)을 그리는 UI 셰이더.
// 이미지 사각형(_RectSize, 픽셀) 안쪽으로 _GlowWidth만큼 들어간 곳을 패널 테두리로 보고, 테두리 밖으로 갈수록 옅어지는 빛을
// 왼쪽 위 _ColorA에서 오른쪽 아래 _ColorB로 넘어가는 색으로 칠함. 그림(텍스처)은 쓰지 않으며, 깜빡임은 CanvasGroup 알파로 줌.
Shader "UI/PanelAura"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ColorA ("Glow Color (Top Left)", Color) = (1, 0.25, 0.95, 1)
        _ColorB ("Glow Color (Bottom Right)", Color) = (0.25, 0.45, 1, 1)
        _RectSize ("Rect Size (px)", Vector) = (800, 560, 0, 0)
        _GlowWidth ("Glow Width (px)", Float) = 40
        _Chamfer ("Corner Chamfer (px)", Float) = 20
        _InnerWidth ("Inner Fade (px)", Float) = 8

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _ColorA;
            fixed4 _ColorB;
            float4 _RectSize;
            float _GlowWidth;
            float _Chamfer;
            float _InnerWidth;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord; // 그림 없는 Image라 0~1
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 이미지 가운데를 원점으로 한 픽셀 좌표와 패널(이미지에서 _GlowWidth만큼 안쪽) 반 크기
                float2 p = (IN.texcoord - 0.5) * _RectSize.xy;
                float2 halfSize = _RectSize.xy * 0.5 - _GlowWidth;

                // 모서리를 _Chamfer만큼 비스듬히 깎은 사각형까지의 부호 있는 거리(밖 +, 안 -)
                float2 q = abs(p) - halfSize;
                float box = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
                float chamfer = (q.x + q.y + _Chamfer) * 0.70710678;
                float d = max(box, chamfer);

                // 바깥은 테두리에서 _GlowWidth까지 부드럽게 옅어지고, 안쪽은 _InnerWidth 안에서 금방 사라짐(패널 그림이 덮음)
                float outer = saturate(1.0 - d / max(_GlowWidth, 0.001));
                outer *= outer;
                float inner = saturate(1.0 + d / max(_InnerWidth, 0.001));
                float glow = d > 0.0 ? outer : inner;

                float t = saturate((IN.texcoord.x + (1.0 - IN.texcoord.y)) * 0.5);
                half4 color = lerp(_ColorA, _ColorB, t) * IN.color;
                color.a *= glow;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
