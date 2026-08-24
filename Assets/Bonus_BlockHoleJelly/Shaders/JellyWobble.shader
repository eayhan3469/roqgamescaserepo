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
        _Alpha ("Opacity (real transparency, not just tint)", Range(0,1)) = 0.82
        _EdgeOpacityBoost ("Extra Opacity At Edges (fresnel)", Range(0,1)) = 0.16
        _Smoothness ("Smoothness", Range(0,1)) = 0.8
        _SpecColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularIntensity ("Specular Intensity", Range(0,6)) = 0.7
        _SheenStrength ("Broad Sheen Strength", Range(0,2)) = 0.15

        [Header(Toy Candy Toon Shading)]
        _ShadowTone ("Shadow Band Brightness", Range(0,1)) = 0.8
        _MidTone ("Mid Band Brightness", Range(0.5,1.5)) = 0.95
        _HighTone ("Highlight Band Brightness", Range(1,2)) = 1.15
        _TopHighlightColor ("Top Highlight Color", Color) = (1, 1, 1, 1)
        _TopHighlightStrength ("Top Highlight Strength", Range(0,1)) = 0.45

        [Header(Per Corner Candy Glint)]
        _CornerGlintColor ("Corner Glint Color", Color) = (1, 1, 1, 1)
        _CornerGlintStrength ("Corner Glint Strength", Range(0,2)) = 0.8
        _CornerGlintSize ("Corner Glint Size", Range(0.05, 0.6)) = 0.22

        [Header(Suspended Flecks)]
        _FleckColor ("Fleck Color", Color) = (1, 1, 1, 1)
        _FleckStrength ("Fleck Strength", Range(0,2)) = 0.5
        _FleckDensity ("Flecks Per Unit", Range(2, 20)) = 8
        _FleckSize ("Fleck Size", Range(0.05, 0.6)) = 0.22
        _FleckCoverage ("Fleck Coverage (0=rare, 1=everywhere)", Range(0,1)) = 0.16

        [Header(Gummy Translucency)]
        _WrapAmount ("Diffuse Wrap (soft falloff)", Range(0,1)) = 0.4
        _TranslucencyStrength ("Backlight Translucency", Range(0,2)) = 0.25
        _TranslucencyColor ("Translucency Tint", Color) = (1, 0.95, 0.7, 1)
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 1.6
        _RimStrength ("Rim Strength", Range(0,2)) = 1.3

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
        // Real alpha transparency (not just a tinted-but-opaque look) — a jelly block
        // should let some light/background through, especially toward the edges where
        // you are looking through more of a curved-feeling surface at a grazing angle.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // This project's URP renderer uses Forward+ (PC_Renderer.asset m_RenderingMode: 2),
            // which culls lights into a tiled/clustered structure. GetAdditionalLightsCount()/
            // GetAdditionalLight() branch internally on this keyword to read that structure —
            // without it they silently behave as if 0 additional lights exist, even with
            // _ADDITIONAL_LIGHTS compiled. This was why the scene's second (brighter, unshadowed)
            // directional light never reached this shader.
            #pragma multi_compile _ _FORWARD_PLUS

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
                float3 localPosOS : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Alpha;
                float _EdgeOpacityBoost;
                float _Smoothness;
                float4 _SpecColor;
                float _SpecularIntensity;
                float _SheenStrength;
                float _ShadowTone;
                float _MidTone;
                float _HighTone;
                float4 _TopHighlightColor;
                float _TopHighlightStrength;
                float4 _CornerGlintColor;
                float _CornerGlintStrength;
                float _CornerGlintSize;
                float4 _FleckColor;
                float _FleckStrength;
                float _FleckDensity;
                float _FleckSize;
                float _FleckCoverage;
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
                // Un-deformed object-space position, used in frag() for the corner glint and
                // suspended flecks so those patterns stay fixed "inside" the material and don't
                // swim around during jelly wobble the way a world-space pattern would.
                OUT.localPosOS = posOS;

                return OUT;
            }

            // Cheap deterministic hash (IQ-style), used for both the per-cell corner glint
            // jitter and the scattered suspended flecks below — no texture lookup needed.
            float3 Hash3(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            // Toon-banded diffuse + two-lobe specular for ONE light, reused for both the
            // main light and every additional light in the loop below (see frag()).
            void ShadeOneLight(Light light, half3 albedo, float3 normalWS, float3 viewDir, float fresnel,
                                out half3 diffuseOut, out half3 specularOut)
            {
                float3 lightDir = light.direction;
                half3 atten = light.color * light.distanceAttenuation * light.shadowAttenuation;

                float NdotL = dot(normalWS, lightDir);
                float wrappedNdotL = saturate((NdotL + _WrapAmount) / (1.0 + _WrapAmount));
                float shadowToMid = smoothstep(0.15, 0.45, wrappedNdotL);
                float midToHigh = smoothstep(0.55, 0.85, wrappedNdotL);
                float toonTone = lerp(_ShadowTone, _MidTone, shadowToMid);
                toonTone = lerp(toonTone, _HighTone, midToHigh);
                diffuseOut = albedo * atten * toonTone;

                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
                half sharpPower = exp2(_Smoothness * 11.0 + 1.0);
                half sharpSpec = pow(NdotH, sharpPower);
                half broadSpec = pow(NdotH, 4.0) * _SheenStrength;
                half specFresnelBoost = lerp(1.0, 1.8, fresnel);
                specularOut = _SpecColor.rgb * atten * (sharpSpec + broadSpec) * _SpecularIntensity * specFresnelBoost;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;

                float3 normalWS = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                float3 lightDir = mainLight.direction;
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float NdotV = saturate(dot(normalWS, viewDir));
                float fresnel = pow(1.0 - NdotV, _RimPower);

                // Wrapped + BANDED diffuse: real-time smooth NdotL shading reads as plastic;
                // reference toy/jelly-game shaders use a toon-style ramp instead (a dark
                // band, a mid band, a bright band, soft-blended rather than hard-stepped) —
                // this is the main thing that makes the material look like jelly at rest,
                // not just "shiny plastic that jiggles".
                half3 diffuseBase, mainSpecular;
                ShadeOneLight(mainLight, albedo, normalWS, viewDir, fresnel, diffuseBase, mainSpecular);
                half3 totalSpecular = mainSpecular;

                // This shader only sampled the URP "main" light (GetMainLight, the dim
                // Sun-tagged directional at intensity 0.35) — but the scene actually authors
                // a SECOND, much brighter (0.85) directional light too, used by every stock
                // URP-Lit object in the level as an edge/corner fill light. Stock Lit shaders
                // pick up additional lights automatically; this custom shader did not, so any
                // face angled toward that second light rendered dark here while every normal
                // block looked fine — exactly the "edges/corners are not catching light"
                // symptom. Loop over and add every additional light's contribution too.
                #if defined(_ADDITIONAL_LIGHTS)
                int additionalLightsCount = GetAdditionalLightsCount();
                for (int i = 0; i < additionalLightsCount; i++)
                {
                    Light addLight = GetAdditionalLight(i, IN.positionWS);
                    half3 addDiffuse, addSpecular;
                    ShadeOneLight(addLight, albedo, normalWS, viewDir, fresnel, addDiffuse, addSpecular);
                    diffuseBase += addDiffuse;
                    totalSpecular += addSpecular;
                }
                #endif

                // Big soft "always-on" top highlight — upward-facing surfaces blend toward
                // a bright tone regardless of the actual light direction, the way reference
                // jelly-cube toy shaders fake a consistent studio key light on top of every
                // piece. Blended (lerp) rather than added on top, so it brightens the toon
                // shading without washing the base color out to flat white — squared falloff
                // for a softer, rounder patch than a raw dot product.
                float topFacing = saturate(normalWS.y);
                float topShape = topFacing * topFacing * topFacing * topFacing;
                float topFactor = saturate(topShape * _TopHighlightStrength);
                half3 diffuse = lerp(diffuseBase, _TopHighlightColor.rgb * mainLight.color, topFactor);

                half3 specular = totalSpecular;

                // Per-corner candy glint: reference toy/jelly shaders fake a single small
                // fixed studio-light dot near one corner of every top face rather than a
                // smooth streak that slides with the camera. Each unit cell of the (possibly
                // multi-cell) mesh gets its own jittered corner point (random per cell via
                // the hash, so a multi-cell L-piece doesn't look like it is stamped from one
                // repeating tile) — additive, only on top-facing geometry, so it reads as a
                // small bright dot sitting on the surface, not a lighting change.
                float2 cellXZ = frac(IN.localPosOS.xz);
                float2 cellId = floor(IN.localPosOS.xz);
                float2 glintPoint = Hash3(float3(cellId, 3.7)).xy * 0.5 + 0.15;
                float glintDist = distance(cellXZ, glintPoint);
                float glintMask = smoothstep(_CornerGlintSize, _CornerGlintSize * 0.15, glintDist);
                half3 cornerGlint = _CornerGlintColor.rgb * glintMask * topShape * _CornerGlintStrength;

                // Suspended flecks: small round dots scattered through the material (fruit-jelly
                // "bits" look), one candidate point per fine grid cell, most cells skipped via
                // _FleckCoverage so they read as sparse specks rather than an all-over pattern.
                // Uses full 3D local position (not just XZ) so flecks differ between the top
                // face and side walls instead of repeating the same 2D pattern on every face.
                float3 fleckSpace = IN.localPosOS * _FleckDensity;
                float3 fleckCellId = floor(fleckSpace);
                float3 fleckCellFrac = frac(fleckSpace);
                float3 fleckRand = Hash3(fleckCellId);
                float3 fleckJitter = fleckRand * 0.6 + 0.2;
                float fleckPresence = step(1.0 - _FleckCoverage, fleckRand.z);
                float fleckDist = distance(fleckCellFrac, fleckJitter);
                float fleckMask = smoothstep(_FleckSize, _FleckSize * 0.2, fleckDist) * fleckPresence;
                half3 flecks = _FleckColor.rgb * fleckMask * _FleckStrength;

                // Cheap fake-SSS: light "bleeding through" from behind the surface,
                // strongest when the view is looking roughly along the light direction
                // through the block — the classic backlit-gummy-bear look.
                float backLight = saturate(dot(-normalWS, lightDir)) * saturate(dot(viewDir, -lightDir));
                half3 translucency = _TranslucencyColor.rgb * albedo * mainLight.color * backLight * _TranslucencyStrength;

                // Fresnel rim for a soft gummy sheen at grazing angles.
                half3 rim = _RimColor.rgb * fresnel * _RimStrength;

                // Ambient probe light (bright sky/backdrop) was the biggest single cause of
                // the "washed out" look on top faces — full-strength SH on top of an already
                // bright toon high-band and top highlight blew the saturated color out toward
                // white. Dialed way down; it is just a faint fill now, not a driver of tone.
                half3 ambient = albedo * SampleSH(normalWS) * 0.4;

                half3 color = diffuse + specular + translucency + rim + ambient + cornerGlint + flecks;

                // Real transparency: base opacity plus extra opacity at grazing angles
                // (like looking through the curved edge of a gummy block vs. straight
                // through its flat face) — reads as a proper see-through jelly instead of
                // an opaque surface with a tinted color.
                float alpha = saturate(_Alpha + fresnel * _EdgeOpacityBoost) * tex.a * _BaseColor.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
