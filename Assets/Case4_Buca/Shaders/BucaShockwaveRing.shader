Shader "Buca/ShockwaveRing"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.25, 1.0, 0.75, 1.0)
        _Intensity ("Glow Intensity", Range(0.5, 8.0)) = 4.2
        _RingRadius ("Ring Center Radius", Range(0.1, 0.95)) = 0.78
        _RingWidth ("Ring Soft Width", Range(0.01, 0.40)) = 0.065
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+100" 
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        ZTest LEqual

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

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

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float _Intensity;
                float _RingRadius;
                float _RingWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.color = input.color * _TintColor;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Distance from center (0.0 to 1.0)
                float2 centeredUV = (input.uv - 0.5) * 2.0;
                float dist = length(centeredUV);

                // Smooth Gaussian bell-curve ring profile
                float ring = exp(-pow((dist - _RingRadius) / max(0.001, _RingWidth), 2.0));

                // Anti-aliased outer edge cutoff to guarantee zero boundary clipping
                float boundaryCut = smoothstep(1.0, 0.82, dist);
                float alpha = ring * boundaryCut * input.color.a;

                // Glowing additive emission with Bloom pop
                half3 rgb = input.color.rgb * _Intensity * alpha;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
