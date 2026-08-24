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
        _Smoothness ("Smoothness", Range(0,1)) = 0.93
        _SpecColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularIntensity ("Specular Intensity", Range(0,6)) = 1.8
        _SheenStrength ("Broad Sheen Strength", Range(0,2)) = 0.08

        [Header(Toy Candy Toon Shading)]
        _ShadowTone ("Shadow Band Brightness", Range(0,1)) = 0.8
        _MidTone ("Mid Band Brightness", Range(0.5,1.5)) = 0.95
        _HighTone ("Highlight Band Brightness", Range(1,2)) = 1.15
        _TopHighlightColor ("Top Highlight Color", Color) = (1, 1, 1, 1)
        _TopHighlightStrength ("Top Highlight Strength", Range(0,1)) = 0.15

        [Header(Beveled Corner Sparkle)]
        _CornerBevelColor ("Corner Bevel Highlight Color", Color) = (1, 1, 1, 1)
        _CornerBevelStrength ("Corner Bevel Strength", Range(0,4)) = 1.6
        _CornerBevelSize ("Corner Bevel Radius", Range(0.05, 0.5)) = 0.22
        _CornerBevelSharpness ("Corner Bevel Sharpness", Range(1, 64)) = 12

        [Header(Suspended Chunks)]
        _FleckColor ("Chunk Color", Color) = (1, 0.93, 0.82, 1)
        _FleckStrength ("Chunk Blend Strength", Range(0,1)) = 0.35
        _FleckDensity ("Chunks Per Unit", Range(1, 12)) = 3.5
        _FleckSize ("Chunk Size", Range(0.05, 0.6)) = 0.32
        _FleckCoverage ("Chunk Coverage (0=rare, 1=everywhere)", Range(0,1)) = 0.1

        [Header(Gummy Translucency)]
        _WrapAmount ("Diffuse Wrap (soft falloff)", Range(0,1)) = 0.4
        _TranslucencyStrength ("Backlight Translucency", Range(0,2)) = 0.25
        _TranslucencyColor ("Translucency Tint", Color) = (1, 0.95, 0.7, 1)
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.8
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
                float4 _CornerBevelColor;
                float _CornerBevelStrength;
                float _CornerBevelSize;
                float _CornerBevelSharpness;
                // Real silhouette corners of the mesh's top face, in raw local (object) space,
                // computed once in JellySpringDriver.Awake() from the actual mesh geometry (see
                // its ComputeExteriorTopCorners for why: a plain periodic grid can't tell a real
                // exterior/concave corner apart from the flat interior seam between two occupied
                // cells sitting flush together, which showed up as an obviously fake floating
                // sparkle in the middle of a flat run on multi-cell pieces). Unused slots are a
                // sentinel far outside any block's local space so they're never the "nearest".
                // xz used (corner position), yw unused padding — array size MUST match
                // JellySpringDriver.MaxExteriorCorners.
                float4 _ExteriorCorners[16];
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

            // Toon-banded diffuse + two-lobe specular + beveled-corner sparkle for ONE light,
            // reused for both the main light and every additional light in the loop below
            // (see frag()). `bevelNormal` is a FAKE normal that leans outward/upward near a
            // top-face corner (see frag() for how it is built) — under this project's
            // orthographic camera + directional lights, the real `normalWS` is perfectly
            // uniform across an entire flat cube face, so real specular can only ever light
            // (or not light) the WHOLE face at once, never just its corners. `bevelNormal`
            // fakes the rounded-edge normal a real beveled/curved corner would have, so the
            // corner sparkle responds to light direction like a real highlight (brighter on
            // whichever corner actually faces the light) instead of glowing uniformly.
            void ShadeOneLight(Light light, half3 albedo, float3 normalWS, float3 viewDir, float fresnel,
                                float3 bevelNormal, float bevelSharpness,
                                out half3 diffuseOut, out half3 specularOut, out half cornerOut)
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

                float bevelNdotL = saturate(dot(bevelNormal, lightDir));
                half lightBrightness = dot(atten, half3(0.333, 0.334, 0.333));
                cornerOut = pow(bevelNdotL, bevelSharpness) * lightBrightness;
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

                // Beveled-corner fake normal: find the NEAREST REAL exterior/concave corner of
                // the whole piece's top silhouette (from _ExteriorCorners, computed on the CPU
                // from actual mesh geometry — see JellySpringDriver.ComputeExteriorTopCorners),
                // then lean the normal outward from that corner and upward — approximating what
                // a real small rounded bevel there would look like. Deliberately NOT a plain
                // periodic per-cell grid corner: that version could not tell a real silhouette
                // corner apart from the flat interior seam between two occupied cells sitting
                // flush together, and lit up seams with no real edge there at all. Only
                // meaningful near an actual corner (bevelAmount fades to 0 elsewhere) and only
                // on top-facing geometry (topShape, computed further below reused here via its
                // own local copy since it is needed before the diffuse falls out of the loop).
                float topFacingForBevel = saturate(normalWS.y);
                float topShapeForBevel = topFacingForBevel * topFacingForBevel * topFacingForBevel * topFacingForBevel;
                float2 localXZ = IN.localPosOS.xz;
                float bestCornerDistSq = 1e9;
                float2 bestOffsetXZ = float2(0, 0);
                UNITY_UNROLL
                for (int c = 0; c < 16; c++)
                {
                    float2 offsetXZ = localXZ - _ExteriorCorners[c].xz;
                    float distSq = dot(offsetXZ, offsetXZ);
                    if (distSq < bestCornerDistSq)
                    {
                        bestCornerDistSq = distSq;
                        bestOffsetXZ = offsetXZ;
                    }
                }
                float cornerDistXZ = sqrt(bestCornerDistSq);
                float bevelAmount = smoothstep(_CornerBevelSize, 0.0, cornerDistXZ) * topShapeForBevel;
                float2 outwardDirXZ = bestOffsetXZ / max(cornerDistXZ, 1e-4);
                float3 bevelNormal = normalize(float3(outwardDirXZ.x, 1.4, outwardDirXZ.y));

                // Wrapped + BANDED diffuse: real-time smooth NdotL shading reads as plastic;
                // reference toy/jelly-game shaders use a toon-style ramp instead (a dark
                // band, a mid band, a bright band, soft-blended rather than hard-stepped) —
                // this is the main thing that makes the material look like jelly at rest,
                // not just "shiny plastic that jiggles".
                half3 diffuseBase, mainSpecular;
                half mainCorner;
                ShadeOneLight(mainLight, albedo, normalWS, viewDir, fresnel, bevelNormal, _CornerBevelSharpness, diffuseBase, mainSpecular, mainCorner);
                half3 totalSpecular = mainSpecular;
                half totalCorner = mainCorner;

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
                    half addCorner;
                    ShadeOneLight(addLight, albedo, normalWS, viewDir, fresnel, bevelNormal, _CornerBevelSharpness, addDiffuse, addSpecular, addCorner);
                    diffuseBase += addDiffuse;
                    totalSpecular += addSpecular;
                    totalCorner += addCorner;
                }
                #endif

                // Small, tight "always-on" top highlight — upward-facing surfaces get a
                // compact bright patch regardless of the actual light direction, the way a
                // glass/gel dome catches a consistent studio key light. Blended (lerp) rather
                // than added, and kept LOW strength with a steep falloff (^8, not ^4) so it
                // stays a small compact highlight instead of flattening the whole top face
                // toward pale/matte — the real "glossy" read comes from the sharp specular
                // lobe below, this is just a soft assist.
                float topFacing = saturate(normalWS.y);
                float topShape = topFacing * topFacing * topFacing * topFacing;
                float topShapeTight = topShape * topShape;
                float topFactor = saturate(topShapeTight * _TopHighlightStrength);
                half3 diffuse = lerp(diffuseBase, _TopHighlightColor.rgb * mainLight.color, topFactor);

                half3 specular = totalSpecular;

                // Beveled-corner sparkle: additive, masked to the bevelAmount computed above
                // (near an actual top-face corner) and scaled by how directly a light hits the
                // fake bevel normal there — this is what makes specific corners catch a bright
                // point while others stay dim, matching how a real glossy object's corners
                // catch light unevenly depending on their angle to the light source.
                half3 cornerHighlight = _CornerBevelColor.rgb * totalCorner * bevelAmount * _CornerBevelStrength;

                // Suspended chunks (fruit-jelly "pieces visible through the gel" look): a
                // sparse scatter of soft round blobs computed from a 3D hash grid over the
                // un-deformed object-space position, most cells skipped via _FleckCoverage.
                // Blended INTO the diffuse (lerp) rather than added on top like a sticker —
                // reads as color variation embedded in the material, not glowing dots sitting
                // on the surface. Tinted a warm cream by default, not stark white, and kept
                // subtle (_FleckStrength) so it stays a hint of texture, not a pattern.
                float3 fleckSpace = IN.localPosOS * _FleckDensity;
                float3 fleckCellId = floor(fleckSpace);
                float3 fleckCellFrac = frac(fleckSpace);
                float3 fleckRand = Hash3(fleckCellId);
                float3 fleckJitter = fleckRand * 0.6 + 0.2;
                float fleckPresence = step(1.0 - _FleckCoverage, fleckRand.z);
                float fleckDist = distance(fleckCellFrac, fleckJitter);
                float fleckMask = smoothstep(_FleckSize, _FleckSize * 0.2, fleckDist) * fleckPresence;
                diffuse = lerp(diffuse, _FleckColor.rgb * mainLight.color, fleckMask * _FleckStrength);

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

                half3 color = diffuse + specular + translucency + rim + ambient + cornerHighlight;

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
