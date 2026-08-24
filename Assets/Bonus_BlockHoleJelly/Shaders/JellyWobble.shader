Shader "Bonus/JellyWobble"
{
    // Vertex-displacement "jelly" shader. A driving script (JellySpringDriver) feeds
    // _JellyDir / _JellyAmount per-instance every frame (via MaterialPropertyBlock so
    // many blocks can share one Material instance without allocating a Material each).
    //
    // _JellyDir    : world-space* squash/stretch axis, magnitude ignored (normalized in-shader).
    //                (*treated as object-space here since blocks don't rotate arbitrarily;
    //                good enough for axis-aligned block cubes.)
    // _JellyAmount : signed stretch factor driven by a damped spring in script.
    //                >0 stretches along _JellyDir and squashes the two perpendicular axes,
    //                <0 does the opposite (a squash bounce). Should oscillate through 0 and
    //                decay back to 0 at rest.
    //
    // NOTE (see roq-case-project memory / commit 02030bf): a custom shader that is only ever
    // instantiated at runtime via Shader.Find + new Material() gets STRIPPED from mobile
    // builds. This shader is intentionally referenced by a real saved Material asset
    // (Mat_JellyWobble.mat) instead, so it survives stripping without needing an
    // Always-Included-Shaders entry. Keep it wired that way if this ever ships.
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.35, 0.85, 0.55, 1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
        _SpecColor ("Specular Color", Color) = (1,1,1,1)

        [Header(Jelly Drive - set at runtime by JellySpringDriver)]
        _JellyDir ("Jelly Direction", Vector) = (0, 1, 0, 0)
        _JellyAmount ("Jelly Amount", Float) = 0

        [Header(Secondary Ripple)]
        _JellyRippleFreq ("Ripple Frequency", Float) = 6.0
        _JellyRippleSpeed ("Ripple Speed", Float) = 10.0
        _JellyRippleStrength ("Ripple Strength", Float) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Smoothness;
                float4 _SpecColor;
                float4 _JellyDir;
                float _JellyAmount;
                float _JellyRippleFreq;
                float _JellyRippleSpeed;
                float _JellyRippleStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 posOS = IN.positionOS.xyz;
                float3 dir = _JellyDir.xyz;
                float dirLenSq = dot(dir, dir);
                dir = (dirLenSq > 1e-8) ? normalize(dir) : float3(0, 1, 0);

                // Decompose into along-axis and perpendicular components (object-space,
                // pivot assumed near mesh center — true for the block cubes this targets).
                float axisPos = dot(posOS, dir);
                float3 alongVec = dir * axisPos;
                float3 perpVec = posOS - alongVec;

                // Volume-preserving squash/stretch: stretch along the drive axis,
                // inverse-sqrt squash on the two perpendicular axes.
                float stretch = 1.0 + _JellyAmount;
                float squash = 1.0 / sqrt(max(abs(stretch), 0.0001));

                float3 deformed = alongVec * stretch + perpVec * squash;

                // Secondary traveling ripple along the normal for a wetter/gooey look,
                // strongest at the stretch extremities and fading as the spring settles.
                float ripple = sin(axisPos * _JellyRippleFreq - _Time.y * _JellyRippleSpeed)
                             * _JellyRippleStrength * _JellyAmount;
                deformed += IN.normalOS * ripple;

                float3 positionWS = TransformObjectToWorld(deformed);
                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;

                float3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;

                float NdotL = saturate(dot(normalWS, lightDir));
                half3 diffuse = albedo * mainLight.color * NdotL;

                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
                half specPower = exp2(_Smoothness * 10.0 + 1.0);
                half3 specular = _SpecColor.rgb * mainLight.color * pow(NdotH, specPower) * _Smoothness;

                half3 ambient = albedo * SampleSH(normalWS);

                half3 color = diffuse + specular + ambient;
                return half4(color, tex.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
