Shader "Custom/StickerPeel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _BackSideColor ("Backside Adhesive Color", Color) = (0.93, 0.93, 0.94, 1.0)
        _PeelProgress ("Peel Progress", Range(0.0, 1.2)) = 0.0
        _PeelAngle ("Peel Angle (Degrees)", Range(0, 360)) = 45.0
        _RollRadius ("Roll / Crease Width", Range(0.01, 0.4)) = 0.10
        _ShadowIntensity ("Crease & Drop Shadow", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "StickerPeelPass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _BackSideColor;
                float _PeelProgress;
                float _PeelAngle;
                float _RollRadius;
                float _ShadowIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // 1. If no peel, render normal sprite
                if (_PeelProgress <= 0.001)
                {
                    float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
                    if (col.a < 0.05) discard;
                    return col;
                }

                // 2. Compute Peel Direction Vector & Corner Origin
                float rad = radians(_PeelAngle);
                float2 dir = float2(cos(rad), sin(rad));

                // Find the starting corner (the corner furthest in the negative peel direction)
                float2 origin = float2(
                    dir.x > 0.0 ? 0.0 : 1.0,
                    dir.y > 0.0 ? 0.0 : 1.0
                );

                // Projection distance from origin along peel direction
                float dist = dot(uv - origin, dir);

                // Max distance across the [0,1] quad in this direction
                float2 oppositeCorner = float2(1.0 - origin.x, 1.0 - origin.y);
                float maxDist = max(dot(oppositeCorner - origin, dir), 0.001);

                // Crease fold line advances with _PeelProgress (0 to 1.1)
                float foldLine = _PeelProgress * maxDist;

                // 3. CHECK A: Pixels lying ahead of the fold line (dist >= foldLine)
                if (dist >= foldLine)
                {
                    // Check if the curled flap from behind the fold line has folded over onto this pixel!
                    float flapDist = dist - foldLine;
                    float2 uvOrig = uv - 2.0 * flapDist * dir;

                    // If uvOrig is within sprite bounds and has alpha:
                    if (uvOrig.x >= 0.0 && uvOrig.x <= 1.0 && uvOrig.y >= 0.0 && uvOrig.y <= 1.0)
                    {
                        float4 flapSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvOrig);

                        // If flap sample is solid sticker pixel:
                        if (flapSample.a >= 0.05)
                        {
                            // Render Curled Adhesive Backside
                            // Subtle cylinder lighting highlight near crease ridge
                            float curlHighlight = saturate(1.0 - flapDist / max(_RollRadius, 0.03));
                            float3 backRgb = _BackSideColor.rgb * lerp(0.85, 1.08, curlHighlight);

                            // Subtle metallic/paper sheen along the curl edge
                            backRgb += float3(0.18, 0.18, 0.20) * (curlHighlight * curlHighlight);

                            return float4(backRgb, flapSample.a * _BackSideColor.a * input.color.a);
                        }
                    }

                    // If no curled flap covers this pixel, render the flat Front Side
                    float4 frontCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
                    if (frontCol.a < 0.05) discard;

                    // Contact drop shadow cast by the approaching fold crease
                    float shadowDist = dist - foldLine;
                    if (shadowDist < _RollRadius * 1.8)
                    {
                        float shadowFactor = 1.0 - (shadowDist / (_RollRadius * 1.8));
                        frontCol.rgb *= lerp(1.0, 1.0 - _ShadowIntensity * 0.7, shadowFactor * shadowFactor);
                    }

                    return frontCol;
                }
                else
                {
                    // 4. CHECK B: Pixels behind the fold line (dist < foldLine)
                    // The sticker has been lifted and peeled away from this spot!
                    // Discard so the background / notebook page underneath is visible
                    discard;
                    return float4(0, 0, 0, 0);
                }
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
