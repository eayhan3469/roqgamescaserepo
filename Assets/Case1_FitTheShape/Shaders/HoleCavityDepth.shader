Shader "FitTheShape/HoleCavityDepth"
{
    Properties
    {
        [Header(Cavity Depth Colors)]
        _BaseColor ("Upper Wall Tint", Color) = (0.05, 0.25, 0.40, 1.0)
        _BottomColor ("Deep Floor Color", Color) = (0.02, 0.02, 0.03, 1.0)
        _RimColor ("Rim Edge Highlight", Color) = (0.35, 0.70, 0.95, 1.0)

        [Header(Depth and Occlusion Tuning)]
        _DepthMinY ("Mesh Min Y (Bottom)", Float) = -0.45
        _DepthMaxY ("Mesh Max Y (Rim)", Float) = 0.05
        _DepthPower ("Depth Falloff Curve", Range(0.5, 4.0)) = 1.8
        _WallLighting ("Wall Light Factor", Range(0.0, 1.0)) = 0.55
        _FresnelStrength ("Angle Reactivity", Range(0.0, 1.0)) = 0.45
        _RimWidth ("Rim Lip Width", Range(0.5, 0.99)) = 0.88
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

            Cull Off
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
                float  localPosY    : TEXCOORD3;
                float3 viewDirWS    : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BottomColor;
                float4 _RimColor;
                float  _DepthMinY;
                float  _DepthMaxY;
                float  _DepthPower;
                float  _WallLighting;
                float  _FresnelStrength;
                float  _RimWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS   = normalInputs.normalWS;
                output.localPosY  = input.positionOS.y;
                output.viewDirWS  = GetWorldSpaceViewDir(vertexInputs.positionWS);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);

                // 1. Vertical Depth Ratio (0 = bottom floor, 1 = top rim)
                float depthRange = max(0.001, _DepthMaxY - _DepthMinY);
                float depth01 = saturate((input.localPosY - _DepthMinY) / depthRange);
                float depthCurve = pow(depth01, _DepthPower);

                // 2. Wall vs Floor distinction (floor is flatter, walls are inclined)
                float wallFactor = saturate(1.0 - abs(input.normalWS.y));

                // 3. Main Directional Light interaction on inner walls
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(N, mainLight.direction));
                float wallLight = lerp(1.0, NdotL * 0.7 + 0.3, _WallLighting);

                // 4. View-angle Fresnel (makes inner walls pop when viewed from top/bottom angles)
                float NdotV = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, 2.5) * _FresnelStrength;

                // 5. Interpolate Color from Deep Floor to Upper Walls
                float3 cavityCol = lerp(_BottomColor.rgb, _BaseColor.rgb * wallLight, depthCurve);

                // Add subtle wall contrast
                cavityCol = lerp(cavityCol, cavityCol * (1.0 + wallFactor * 0.35), depthCurve);

                // 6. Crisp Rim Lip Highlight near top edge
                float rimMask = smoothstep(_RimWidth, 1.0, depth01);
                float3 finalColor = lerp(cavityCol, _RimColor.rgb, rimMask * 0.65) + fresnel * _BaseColor.rgb * depthCurve;

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
