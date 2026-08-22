Shader "FitTheShape/StylizedBlock"
{
    Properties
    {
        [Header(Base Color)]
        _BaseColor ("Base Color", Color) = (0.0, 0.67, 1.0, 1.0)
        _Color ("Main Color (Fallback)", Color) = (0.0, 0.67, 1.0, 1.0)

        [Header(Edge Bevel Highlight)]
        _RimPower ("Edge Rim Sharpness", Range(1.0, 6.0)) = 2.6
        _RimIntensity ("Edge Rim Brightness", Range(0.0, 1.5)) = 0.65
        _RimTint ("Edge Rim Tint", Color) = (1.0, 1.0, 1.0, 1.0)

        [Header(Flat Surface Settings)]
        _SpecularIntensity ("Flat Surface Specular", Range(0.0, 0.5)) = 0.04
        _LightWrap ("Shadow Wrap Softness", Range(0.0, 0.6)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 viewDirWS    : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _Color;
                float4 _RimTint;
                float  _RimPower;
                float  _RimIntensity;
                float  _SpecularIntensity;
                float  _LightWrap;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS   = normalInputs.normalWS;
                output.viewDirWS  = GetWorldSpaceViewDir(vertexInputs.positionWS);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);

                Light mainLight = GetMainLight();

                // 1. Soft Wrapped Diffuse on flat surfaces (Vibrant & Matte)
                float NdotL = dot(N, mainLight.direction);
                float wrappedDiff = saturate((NdotL + _LightWrap) / (1.0 + _LightWrap));
                
                float3 ambient = float3(0.35, 0.35, 0.38) * _BaseColor.rgb;
                float3 diffuse = _BaseColor.rgb * (mainLight.color * wrappedDiff * 0.85) + ambient;

                // 2. Bevel Edge Rim Highlight (Active on rounded edges, 0 on flat front face)
                float NdotV = saturate(dot(N, V));
                float edgeFactor = pow(1.0 - NdotV, _RimPower);
                
                float edgeFacingLight = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);
                float3 edgeTint = lerp(_RimTint.rgb, _BaseColor.rgb * 1.4 + 0.2, 0.45);
                float3 edgeHighlight = edgeTint * (edgeFactor * _RimIntensity * (edgeFacingLight * 0.8 + 0.35));

                // 3. Minimal surface specular (flat face remains matte)
                float3 H = normalize(mainLight.direction + V);
                float NdotH = saturate(dot(N, H));
                float surfaceSpec = pow(NdotH, 24.0) * _SpecularIntensity;
                float3 flatSpec = float3(1.0, 1.0, 1.0) * surfaceSpec;

                float3 finalColor = diffuse + edgeHighlight + flatSpec;

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
