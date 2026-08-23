Shader "Custom/StickerPeel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _BackSideColor ("Backside Base Tint", Color) = (0.95, 0.95, 0.95, 1.0)
        _PeelProgress ("Peel Progress", Range(0.0, 1.2)) = 0.0
        _PeelAngle ("Peel Angle (Degrees)", Range(0, 360)) = 45.0
        _RollRadius ("Roll / Crease Width", Range(0.01, 0.4)) = 0.10
        _ShadowWidth ("Crease Shadow Width", Range(0.01, 0.2)) = 0.08
        _ShadowStrength ("Crease Shadow Strength", Range(0.0, 1.0)) = 0.45
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
            #pragma target 3.0

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
                float _ShadowWidth;
                float _ShadowStrength;
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
                
                if (_PeelProgress <= 0.001)
                {
                    float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
                    if (col.a < 0.05) discard;
                    return col;
                }

                float rad = radians(_PeelAngle);
                float2 dir = float2(cos(rad), sin(rad));
                float2 origin = float2(dir.x > 0.0 ? 0.0 : 1.0, dir.y > 0.0 ? 0.0 : 1.0);
                
                float d = dot(uv - origin, dir);
                float r = max(_RollRadius, 0.01);
                float foldLine = _PeelProgress * 1.5;

                // 1. Unpeeled Flat Region (With Crease Under-Shadow / AO)
                if (d < foldLine)
                {
                    float4 frontCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
                    if (frontCol.a < 0.05) discard;

                    // Apply drop shadow beneath the lifted paper near the fold line
                    float shadowDist = foldLine - d;
                    if (shadowDist < _ShadowWidth)
                    {
                        float shadowFactor = smoothstep(0.0, _ShadowWidth, shadowDist);
                        float shadowMultiplier = lerp(1.0 - _ShadowStrength, 1.0, shadowFactor);
                        frontCol.rgb *= shadowMultiplier;
                    }

                    return frontCol;
                }
                
                // 2. Curled Region (Cylindrical Roll with Depth Gradient)
                float delta = d - foldLine;
                if (delta <= PI * r)
                {
                    float theta = delta / r;
                    float2 curlUV = uv - dir * (delta - sin(theta) * r);

                    if (curlUV.x < 0.0 || curlUV.x > 1.0 || curlUV.y < 0.0 || curlUV.y > 1.0)
                        discard;

                    float4 sampleCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, curlUV);
                    if (sampleCol.a < 0.05) discard;

                    // Cylindrical light gradient:
                    // - Base of crease (theta ~ 0) is darker ambient occlusion (0.58)
                    // - Top crest (theta ~ PI/2 to PI) gets full bright specular highlight
                    float aoGradient = smoothstep(0.0, 1.2, theta);
                    float shade = lerp(0.58, 1.0, aoGradient);
                    float crestHighlight = pow(saturate(sin(theta)), 3.0) * 0.25;

                    float4 outCol = _BackSideColor;
                    outCol.rgb = (outCol.rgb * shade) + crestHighlight;
                    outCol.a = sampleCol.a;

                    return outCol * input.color;
                }
                else
                {
                    // 3. Flipped Backside Region (Past cylinder)
                    float2 backUV = uv - dir * (2.0 * delta - PI * r);
                    if (backUV.x < 0.0 || backUV.x > 1.0 || backUV.y < 0.0 || backUV.y > 1.0)
                        discard;

                    float4 backSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, backUV);
                    if (backSample.a < 0.05) discard;

                    float4 flatBack = _BackSideColor;
                    flatBack.rgb *= 0.95;
                    flatBack.a = backSample.a;
                    return flatBack * input.color;
                }
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
