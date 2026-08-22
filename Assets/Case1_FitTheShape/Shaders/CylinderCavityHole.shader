Shader "FitTheShape/CylinderCavityHole"
{
    Properties
    {
        [Header(Cavity Colors)]
        _WallColor ("Inner Wall Color", Color) = (0.08, 0.26, 0.45, 1.0)
        _FloorColor ("Bottom Floor Color", Color) = (0.02, 0.03, 0.05, 1.0)
        _RimHighlight ("Rim Lip Color", Color) = (0.25, 0.55, 0.85, 1.0)

        [Header(Cavity Depth and Wall Lighting)]
        _DepthMinY ("Bottom Floor Y", Float) = -0.40
        _DepthMaxY ("Top Rim Y", Float) = 0.02
        _WallBrightness ("Inner Wall Visibility", Range(0.2, 1.5)) = 0.88
        _FloorOcclusion ("Floor Darkness", Range(0.0, 1.0)) = 0.80
        _CylinderCurveLight ("Cylinder Normal Wrap", Range(0.0, 0.6)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"
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
                float  localPosY    : TEXCOORD2;
                float3 viewDirWS    : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _WallColor;
                float4 _FloorColor;
                float4 _RimHighlight;
                float  _DepthMinY;
                float  _DepthMaxY;
                float  _WallBrightness;
                float  _FloorOcclusion;
                float  _CylinderCurveLight;
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

                Light mainLight = GetMainLight();

                // 1. Vertical depth ratio (0 = bottom floor, 1 = top rim mouth)
                float depthSpan = max(0.001, _DepthMaxY - _DepthMinY);
                float depthFactor = saturate((input.localPosY - _DepthMinY) / depthSpan);

                // 2. Wall vs Floor separation (walls are inclined, floor is flat)
                float isWall = saturate(1.2 - abs(input.normalWS.y));

                // 3. Cylinder-aware light wrap (inner wall is softly lit with block hue, not pitch black, not glaring)
                float NdotL = saturate((dot(N, mainLight.direction) + _CylinderCurveLight) / (1.0 + _CylinderCurveLight));
                float3 wallLit = _WallColor.rgb * (mainLight.color * NdotL * 0.75 + float3(0.28, 0.28, 0.32)) * _WallBrightness;

                // 4. Soft floor occlusion (bottom is deep shadow)
                float3 floorLit = lerp(_FloorColor.rgb, wallLit * 0.35, 1.0 - _FloorOcclusion);

                // 5. Blend from floor to walls based on depth & normal
                float3 cavityColor = lerp(floorLit, wallLit, depthFactor * 0.8 + isWall * 0.35);

                // 6. Subtle view-angle wall shading so walls shift visibly as cylinder rotates
                float NdotV = saturate(dot(N, V));
                float wallAngleFactor = pow(1.0 - NdotV, 2.0) * 0.25;
                cavityColor += _WallColor.rgb * wallAngleFactor * isWall;

                return float4(cavityColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
