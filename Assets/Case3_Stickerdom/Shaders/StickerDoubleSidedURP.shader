Shader "Custom/StickerDoubleSidedURP"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _BackSideColor ("Backside Adhesive Color", Color) = (0.90, 0.91, 0.93, 1.0)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest+50"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite On
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "DoubleSidedPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _BackSideColor;
                float _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                float4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (texCol.a < _Cutoff) discard;

                if (isFrontFace)
                {
                    // Front side: colorful sticker graphic
                    return float4(texCol.rgb * input.color.rgb, texCol.a * input.color.a);
                }
                else
                {
                    // Back side: realistic metallic/paper adhesive backside
                    float sheen = 0.94 + 0.10 * sin((input.uv.x + input.uv.y) * 20.0);
                    float3 backRgb = _BackSideColor.rgb * sheen;
                    return float4(backRgb * input.color.rgb, texCol.a * _BackSideColor.a * input.color.a);
                }
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
