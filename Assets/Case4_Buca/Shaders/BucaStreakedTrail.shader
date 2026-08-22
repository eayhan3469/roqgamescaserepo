Shader "Buca/StreakedTrail"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (1.0, 0.85, 0.45, 1.0)
        _BaseAlpha ("Overall Transparency", Range(0.1, 1.0)) = 0.65
        _HeadGlowStrength ("Head Glow Boost", Range(0.0, 2.0)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float  _BaseAlpha;
                float  _HeadGlowStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float u = input.uv.x; // 0 at head (near disc) -> 1 at tail
                float v = input.uv.y; // 0 at left edge -> 1 at right edge

                // 1. Soft lateral boundary (keeps edges clean and soft)
                float edgeMask = sin(saturate(v) * 3.14159265);
                edgeMask = pow(edgeMask, 0.65);

                // 2. Three soft internal light streaks / rays across the ribbon width
                float leftRay   = exp(-pow((v - 0.20) * 7.0, 2.0));
                float centerRay = exp(-pow((v - 0.50) * 8.0, 2.0));
                float rightRay  = exp(-pow((v - 0.80) * 7.0, 2.0));
                float streakPattern = 0.32 + 0.68 * (leftRay + centerRay * 0.75 + rightRay);

                // 3. Length fade (bright at disc, fading gracefully towards tail)
                float lengthFade = pow(saturate(1.0 - u), 1.25);

                // 4. Front head glow bar right behind the disc
                float headBar = smoothstep(0.20, 0.0, u) * _HeadGlowStrength;

                // 5. Warm Golden Color Gradient
                float3 colHead = float3(1.0, 0.98, 0.85); // Bright gold-white
                float3 colMid  = float3(1.0, 0.72, 0.22); // Warm rich amber
                float3 colTail = float3(1.0, 0.45, 0.05); // Deep golden amber

                float3 rgb = lerp(colHead, colMid, smoothstep(0.0, 0.50, u));
                rgb = lerp(rgb, colTail, smoothstep(0.50, 1.0, u));
                rgb = rgb * _TintColor.rgb + headBar * colHead;

                // 6. Translucent alpha calculation
                float alpha = edgeMask * streakPattern * lengthFade * _BaseAlpha * input.color.a;

                return float4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
