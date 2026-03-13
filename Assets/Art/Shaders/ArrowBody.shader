Shader "ArrowThing/ArrowBody"
{
    Properties
    {
        _Color          ("Color",            Color)  = (0.816, 0.847, 0.910, 1)
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.5

        // Flash driven by ArrowView via MaterialPropertyBlock.
        _FlashColor     ("Flash Color",      Color)  = (1, 0.267, 0.267, 1)
        _FlashT         ("Flash T",          Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _HighlightStrength;
                float4 _FlashColor;
                float  _FlashT;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // UV.y runs 0..1 across the width.
                // Map to -1..1, square it, subtract from 1 → dome profile peaking at centre.
                float v        = IN.uv.y;
                float dome     = 1.0 - (2.0 * v - 1.0) * (2.0 * v - 1.0);   // 0..1
                float highlight = smoothstep(0.0, 1.0, dome) * _HighlightStrength;

                float3 base    = _Color.rgb + highlight;
                float3 flashed = lerp(base, _FlashColor.rgb, _FlashT);

                return half4(flashed, _Color.a);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
