Shader "Bonus/JellyWobble"
{
    // Vertex-displacement "jelly" shader. A driving script (JellySpringDriver) feeds
    // _JellyDir / _JellyAmount per-instance every frame (via MaterialPropertyBlock so
    // many blocks can share one Material instance without allocating a Material each).
    //
    // _JellyDir    : object-space squash/stretch axis, magnitude ignored (normalized in-shader).
    //                Treated as object-space since blocks don't rotate arbitrarily; good
    //                enough for axis-aligned block cubes.
    // _JellyAmount : signed stretch factor driven by a damped spring in script.
    //                >0 stretches along _JellyDir and squashes the two perpendicular axes,
    //                <0 does the opposite (a squash bounce). Should oscillate through 0 and
    //                decay back to 0 at rest.
    //
    // Normals are re-derived analytically for the primary squash/stretch (it's a constant
    // linear map per-vertex, so its inverse-transpose is cheap to apply directly) instead of
    // reusing the rest-pose normal — fixes the dark seam/crack that showed up at high stretch
    // when normals were left un-transformed. The secondary ripple is a small perturbation and
    // is not accounted for in the normal, which is an accepted approximation.
    //
    // _JellyPivotOffset: the squash/stretch decomposition below is relative to OBJECT-SPACE
    // ORIGIN, not the mesh's actual centroid — fine for a single block cube (naturally
    // centered on its own pivot), but multi-cell BlockHole shapes (e.g. the L-tetromino)
    // render through a combined mesh whose pivot sits at one corner of the footprint, not
    // its center. Deforming relative to origin there meant vertices far from that off-center
    // pivot got scaled by a hugely disproportionate absolute amount, which looked like the
    // block exploding/jumping in size. JellySpringDriver feeds this once (mesh.bounds.center
    // in local space) so the decomposition can happen relative to the actual mesh center
    // instead, regardless of where the pivot happens to sit.
    //
    // NOTE (see roq-case-project memory / commit 02030bf): a custom shader that is only ever
    // instantiated at runtime via Shader.Find + new Material() gets STRIPPED from mobile
    // builds. This shader is intentionally referenced by a real saved Material asset
    // (Mat_JellyWobble.mat) instead, so it survives stripping without needing an
    // Always-Included-Shaders entry. Keep it wired that way if this ever ships.
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.08, 0.68, 0.60, 1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.45
        _SpecColor ("Specular Color", Color) = (1,1,1,1)

        [Header(Gummy Translucency)]
        _WrapAmount ("Diffuse Wrap (soft falloff)", Range(0,1)) = 0.4
        _TranslucencyStrength ("Backlight Translucency", Range(0,2)) = 0.6
        _TranslucencyColor ("Translucency Tint", Color) = (1, 0.95, 0.7, 1)
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.0
        _RimStrength ("Rim Strength", Range(0,2)) = 0.5

        [Header(Jelly Drive Runtime)]
        _JellyDir ("Jelly Direction", Vector) = (0, 1, 0, 0)
        _JellyAmount ("Jelly Amount", Float) = 0
        _JellyPivotOffset ("Jelly Pivot Offset (local mesh center)", Vector) = (0, 0, 0, 0)

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
                float _WrapAmount;
                float _TranslucencyStrength;
                float4 _TranslucencyColor;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
                float4 _JellyDir;
                float _JellyAmount;
                float4 _JellyPivotOffset;
                float _JellyRippleFreq;
                float _JellyRippleSpeed;
                float _JellyRippleStrength;
            CBUFFER_END

            // Squash/stretch along `dir` by `stretch`, inverse-sqrt squash perpendicular —
            // shared by position (forward) and normal (inverse-transpose) transforms below.
            float3 ApplyJellyBasis(float3 v, float3 dir, float alongScale, float perpScale)
            {
                float alongComp = dot(v, dir);
                float3 alongVec = dir * alongComp;
                float3 perpVec = v - alongVec;
                return alongVec * alongScale + perpVec * perpScale;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 posOS = IN.positionOS.xyz;
                float3 pivot = _JellyPivotOffset.xyz;
                float3 posRelPivot = posOS - pivot;

                float3 dir = _JellyDir.xyz;
                float dirLenSq = dot(dir, dir);
                dir = (dirLenSq > 1e-8) ? normalize(dir) : float3(0, 1, 0);

                // Volume-preserving squash/stretch: stretch along the drive axis,
                // inverse-sqrt squash on the two perpendicular axes. Relative to the
                // mesh's actual center (posRelPivot), not raw object-space origin — see
                // _JellyPivotOffset note up top.
                float stretch = 1.0 + _JellyAmount;
                float squash = 1.0 / sqrt(max(abs(stretch), 0.0001));

                float3 deformedRelPivot = ApplyJellyBasis(posRelPivot, dir, stretch, squash);
                float3 deformed = deformedRelPivot + pivot;

                // Secondary traveling ripple along the normal for a wetter/gooey look,
                // strongest at the stretch extremities and fading as the spring settles.
                float axisPos = dot(posRelPivot, dir);
                float ripple = sin(axisPos * _JellyRippleFreq - _Time.y * _JellyRippleSpeed)
                             * _JellyRippleStrength * _JellyAmount;
                deformed += IN.normalOS * ripple;

                float3 positionWS = TransformObjectToWorld(deformed);
                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);

                // Normal under the same linear map transforms by the inverse-transpose;
                // since the map is symmetric (pure scale along an axis + perpendicular
                // scale), that's just the same decomposition with reciprocal scales.
                float3 deformedNormalOS = ApplyJellyBasis(IN.normalOS, dir, 1.0 / max(stretch, 0.0001), 1.0 / max(squash, 0.0001));
                OUT.normalWS = TransformObjectToWorldNormal(normalize(deformedNormalOS));
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
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Wrapped diffuse: softens the terminator so the gummy surface doesn't
                // look hard-shaded like plastic.
                float NdotL = dot(normalWS, lightDir);
                float wrappedNdotL = saturate((NdotL + _WrapAmount) / (1.0 + _WrapAmount));
                half3 diffuse = albedo * mainLight.color * wrappedNdotL;

                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
                half specPower = exp2(_Smoothness * 10.0 + 1.0);
                half3 specular = _SpecColor.rgb * mainLight.color * pow(NdotH, specPower) * _Smoothness;

                // Cheap fake-SSS: light "bleeding through" from behind the surface,
                // strongest when the view is looking roughly along the light direction
                // through the block — the classic backlit-gummy-bear look.
                float backLight = saturate(dot(-normalWS, lightDir)) * saturate(dot(viewDir, -lightDir));
                half3 translucency = _TranslucencyColor.rgb * albedo * mainLight.color * backLight * _TranslucencyStrength;

                // Fresnel rim for a soft gummy sheen at grazing angles.
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), _RimPower);
                half3 rim = _RimColor.rgb * fresnel * _RimStrength;

                half3 ambient = albedo * SampleSH(normalWS);

                half3 color = diffuse + specular + translucency + rim + ambient;
                return half4(color, tex.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
