Shader "Custom/StickerDoubleSidedURP"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseMap ("Base Map", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _BackSideColor ("Backside Adhesive Color", Color) = (0.90, 0.91, 0.93, 1.0)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.05
        _ShineProgress ("Shine Ray Progress", Range(-0.5, 1.5)) = -0.5
        _ShineColor ("Shine Ray Color", Color) = (1.0, 1.0, 1.0, 0.85)
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
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

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
                float4 _ShineColor;
                float _Cutoff;
                float _ShineProgress;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 frag(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (texCol.a < _Cutoff) discard;

                bool isFront = IS_FRONT_VFACE(isFrontFace, true, false);

                if (isFront)
                {
                    // Front side: colorful sticker graphic
                    float3 frontRgb = texCol.rgb * _Color.rgb;

                    // Diagonal Shine Ray / Gloss Light Sweep across sticker
                    if (_ShineProgress > -0.4 && _ShineProgress < 1.4)
                    {
                        float rayPos = (input.uv.x + input.uv.y) * 0.5;
                        float rayDist = abs(rayPos - _ShineProgress);
                        float rayIntensity = saturate(1.0 - rayDist / 0.12);
                        rayIntensity = pow(rayIntensity, 2.0);

                        frontRgb += _ShineColor.rgb * (rayIntensity * _ShineColor.a);
                    }

                    return float4(frontRgb, texCol.a * _Color.a);
                }
                else
                {
                    // Back side: Geometric Apex Curvature Highlight passed via vertex color (input.color.r)
                    float apexHighlight = saturate(input.color.r);

                    // Smooth ambient base (0.78 in crease/shadow) to bright luminous peak (1.35 at apex)
                    float lightIntensity = lerp(0.78, 1.35, apexHighlight);
                    float3 backRgb = _BackSideColor.rgb * lightIntensity;

                    // Brilliant specular metallic sheen streak along the apex crest
                    float glossStreak = pow(apexHighlight, 2.5) * 0.40;
                    backRgb += float3(glossStreak, glossStreak, glossStreak);

                    return float4(backRgb * _Color.rgb, texCol.a * _BackSideColor.a * _Color.a);
                }
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
