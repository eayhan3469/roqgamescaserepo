Shader "BlockHole/SmoothLaserOutline"
{
    Properties
    {
        _Color ("Glow Color", Color) = (0.2, 0.9, 1.0, 1.0)
        _CoreColor ("Core Brightness Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Intensity ("Glow Intensity", Float) = 1.6
        _FlowSpeed ("Flow Speed", Float) = 3.0
        _FlowTiling ("Flow Wave Tiling", Float) = 8.0
        _Alpha ("Master Alpha", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+120" 
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SmoothGlowPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _CoreColor;
                float _Intensity;
                float _FlowSpeed;
                float _FlowTiling;
                float _Alpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Calculate smooth bell curve across the width of the ribbon (uv.y from 0 to 1)
                float yDist = sin(input.uv.y * 3.14159265);
                float softGlow = pow(yDist, 1.8);
                float hotCore = pow(yDist, 6.0);

                // Smooth organic energy ripple traveling along the contour length (uv.x)
                float wave = 0.82 + 0.18 * sin(input.uv.x * _FlowTiling - _Time.y * _FlowSpeed);

                half3 finalColor = (_Color.rgb * softGlow * _Intensity + _CoreColor.rgb * hotCore * 1.5) * wave * input.color.rgb;
                float finalAlpha = (softGlow * 0.95 + hotCore * 0.4) * _Alpha * input.color.a * _Color.a;

                return half4(finalColor * finalAlpha, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
