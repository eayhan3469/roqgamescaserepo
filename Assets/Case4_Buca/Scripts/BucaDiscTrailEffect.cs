using System;
using UnityEngine;

namespace Buca
{
    public class BucaDiscTrailEffect : MonoBehaviour
    {
        [Header("Single Unified Streaked Trail Settings")]
        [Tooltip("Full width of the unified trail at the disc base.")]
        [SerializeField] private float trailWidth = 1.16f;

        [Tooltip("Lifetime / length of the trail.")]
        [SerializeField] private float trailLifetime = 0.22f;

        [Header("Warm Translucent Golden Embers")]
        [SerializeField] private float particlesPerMeter = 10f;
        [SerializeField] private float particleLifetime = 0.32f;
        [SerializeField] private float particleStartSize = 0.15f;
        [SerializeField] private Color particleColorStart = new Color(1.0f, 0.90f, 0.45f, 0.85f);
        [SerializeField] private Color particleColorEnd = new Color(1.0f, 0.50f, 0.05f, 0.00f);

        [Header("Material Customization (Optional)")]
        [SerializeField] private Material customTrailMaterial;
        [SerializeField] private Material customParticleMaterial;

        private Transform trailAnchor;
        private TrailRenderer mainTrail;
        private ParticleSystem slipstreamPs;
        private ParticleSystem wallSparkPs;
        private ParticleSystem wallEmberPs;
        private Rigidbody rb;

        private static Material sharedStreakedTrailMat;
        private static Material sharedSoftCircleMat;
        private static Material sharedSoftStarMat;
        private static Material sharedSparkMat;

        public static Material GetOrCreateStreakedTrailMaterial()
        {
            if (sharedStreakedTrailMat == null || !sharedStreakedTrailMat)
            {
                sharedStreakedTrailMat = Resources.Load<Material>("PFX_BucaStreakedTrail");

                if (sharedStreakedTrailMat == null || !sharedStreakedTrailMat)
                {
                    Shader shader = Shader.Find("Buca/StreakedTrail")
                                 ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                 ?? Shader.Find("Sprites/Default");
                    sharedStreakedTrailMat = new Material(shader) { name = "PFX_ProceduralStreakedTrail" };
                }
            }
            return sharedStreakedTrailMat;
        }

        public static Material GetOrCreateSoftCircleMaterial()
        {
            if (sharedSoftCircleMat == null || !sharedSoftCircleMat)
            {
                sharedSoftCircleMat = Resources.Load<Material>("PFX_SoftCircleAdditive")
                                   ?? Resources.Load<Material>("PFX_BucaSoft");

                if (sharedSoftCircleMat == null || !sharedSoftCircleMat)
                {
                    Shader shader = Shader.Find("Buca/SoftParticleAdditive")
                                 ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                 ?? Shader.Find("Sprites/Default");
                    sharedSoftCircleMat = new Material(shader) { name = "PFX_ProceduralSoftCircle" };
                }
            }
            return sharedSoftCircleMat;
        }

        public static Material GetOrCreateSoftStarMaterial()
        {
            if (sharedSoftStarMat == null || !sharedSoftStarMat)
            {
                sharedSoftStarMat = Resources.Load<Material>("PFX_SoftStarAdditive")
                                 ?? Resources.Load<Material>("PFX_BucaStar");

                if (sharedSoftStarMat == null || !sharedSoftStarMat)
                {
                    Shader shader = Shader.Find("Buca/SoftParticleAdditive")
                                 ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                 ?? Shader.Find("Sprites/Default");
                    sharedSoftStarMat = new Material(shader) { name = "PFX_ProceduralSoftStar" };
                }
            }
            return sharedSoftStarMat;
        }

        public static Material GetOrCreateSparkMaterial()
        {
            if (sharedSparkMat == null || !sharedSparkMat)
            {
                Shader shader = Shader.Find("Buca/SoftParticleAdditive")
                             ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("Sprites/Default");
                sharedSparkMat = new Material(shader) { name = "PFX_ProceduralWallSparks" };
                sharedSparkMat.SetColor("_TintColor", Color.white);
                if (sharedSparkMat.HasProperty("_Intensity")) sharedSparkMat.SetFloat("_Intensity", 1.35f);
            }
            return sharedSparkMat;
        }

        private void Awake()
        {
            InitializeAllVfx();
        }

        private void InitializeAllVfx()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();

            if (trailAnchor == null || !trailAnchor)
            {
                GameObject anchorGo = new GameObject("VFX_DiscTrailAnchor");
                trailAnchor = anchorGo.transform;
                trailAnchor.position = new Vector3(transform.position.x, 0.08f, transform.position.z);
                trailAnchor.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            if (mainTrail == null || !mainTrail) SetupMainTrail();
            if (slipstreamPs == null || !slipstreamPs) SetupSlipstreamParticles();
            if (wallSparkPs == null || !wallSparkPs || wallEmberPs == null || !wallEmberPs) SetupWallSparkParticles();

            SetEmitting(false);
        }

        private void SetupMainTrail()
        {
            GameObject trailGo = new GameObject("VFX_UnifiedStreakedTrail");
            trailGo.transform.SetParent(trailAnchor);
            trailGo.transform.localPosition = Vector3.zero;
            trailGo.transform.localRotation = Quaternion.identity;

            mainTrail = trailGo.AddComponent<TrailRenderer>();
            mainTrail.time = trailLifetime;
            mainTrail.minVertexDistance = 0.03f;
            mainTrail.autodestruct = false;
            mainTrail.emitting = false;
            mainTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mainTrail.receiveShadows = false;
            mainTrail.alignment = LineAlignment.TransformZ;

            // Single unified width curve (wide at puck, gently tapering at tail)
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, trailWidth);
            curve.AddKey(0.30f, trailWidth * 0.92f);
            curve.AddKey(0.65f, trailWidth * 0.72f);
            curve.AddKey(1f, trailWidth * 0.38f);
            mainTrail.widthCurve = curve;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1.0f, 0.85f, 0.40f), 0.5f),
                    new GradientColorKey(new Color(1.0f, 0.45f, 0.05f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0f),
                    new GradientAlphaKey(0.85f, 0.5f),
                    new GradientAlphaKey(0.0f, 1f)
                }
            );
            mainTrail.colorGradient = grad;

            mainTrail.material = customTrailMaterial != null ? customTrailMaterial : GetOrCreateStreakedTrailMaterial();
        }

        private void SetupSlipstreamParticles()
        {
            GameObject psGo = new GameObject("VFX_SlipstreamParticles");
            psGo.transform.SetParent(trailAnchor);
            psGo.transform.localPosition = Vector3.zero;
            psGo.transform.localRotation = Quaternion.identity;

            slipstreamPs = psGo.AddComponent<ParticleSystem>();
            slipstreamPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = slipstreamPs.main;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = particleLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(particleStartSize * 0.6f, particleStartSize * 1.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.maxParticles = 120;

            var emission = slipstreamPs.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = particlesPerMeter;

            var shape = slipstreamPs.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.45f;

            var colorOverLifetime = slipstreamPs.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(particleColorStart, 0f), new GradientColorKey(particleColorEnd, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = slipstreamPs.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, 0.1f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.material = customParticleMaterial != null ? customParticleMaterial : GetOrCreateSoftStarMaterial();
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.4f;
        }

        private void SetupWallSparkParticles()
        {
            // Primary: Translucent delicate needle streaks
            GameObject sparkGo = new GameObject("VFX_WallSparks_World");
            wallSparkPs = sparkGo.AddComponent<ParticleSystem>();
            wallSparkPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = wallSparkPs.main;
            main.duration = 1.0f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.09f, 0.22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7.5f, 20.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.065f, 0.135f);
            main.gravityModifier = 1.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.maxParticles = 1000;

            var emission = wallSparkPs.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            var shape = wallSparkPs.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.04f;

            // Soft translucent warm yellow -> light orange gradient (turuncuya yakın açık sarı)
            var colorOverLifetime = wallSparkPs.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.0f, 0.88f, 0.40f), 0f),       // Açık tatlı sarı
                    new GradientColorKey(new Color(1.0f, 0.60f, 0.15f), 0.45f),    // Turuncuya çalan amber
                    new GradientColorKey(new Color(0.95f, 0.35f, 0.06f), 1f)       // Sıcak hafif turuncu
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.90f, 0f),    // Canlı ama transparan başlangıç
                    new GradientAlphaKey(0.70f, 0.5f),  // Transparan akış
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = wallSparkPs.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1.0f);
            sizeCurve.AddKey(0.5f, 0.80f);
            sizeCurve.AddKey(1f, 0.0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = sparkGo.GetComponent<ParticleSystemRenderer>();
            renderer.material = customParticleMaterial != null ? customParticleMaterial : GetOrCreateSparkMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.055f;
            renderer.lengthScale = 3.6f;
            renderer.sortingOrder = 5;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Secondary: Micro-Specks / Embers scattering randomly
            GameObject emberGo = new GameObject("VFX_WallEmbers_World");
            wallEmberPs = emberGo.AddComponent<ParticleSystem>();
            wallEmberPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var emberMain = wallEmberPs.main;
            emberMain.duration = 1.0f;
            emberMain.loop = false;
            emberMain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.30f);
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 10.0f);
            emberMain.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.090f);
            emberMain.gravityModifier = 1.4f;
            emberMain.simulationSpace = ParticleSystemSimulationSpace.World;
            emberMain.playOnAwake = false;
            emberMain.maxParticles = 800;

            var emberShape = wallEmberPs.shape;
            emberShape.shapeType = ParticleSystemShapeType.Cone;
            emberShape.angle = 55f;
            emberShape.radius = 0.05f;

            var emberColor = wallEmberPs.colorOverLifetime;
            emberColor.enabled = true;
            emberColor.color = grad;

            var emberSize = wallEmberPs.sizeOverLifetime;
            emberSize.enabled = true;
            emberSize.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var emberRenderer = emberGo.GetComponent<ParticleSystemRenderer>();
            emberRenderer.material = customParticleMaterial != null ? customParticleMaterial : GetOrCreateSparkMaterial();
            emberRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            emberRenderer.sortingOrder = 5;
            emberRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            emberRenderer.receiveShadows = false;
        }

        public void SetEmitting(bool active)
        {
            if (mainTrail != null) mainTrail.emitting = active;

            if (slipstreamPs != null)
            {
                var em = slipstreamPs.emission;
                em.enabled = active;

                if (active)
                {
                    slipstreamPs.Play();
                }
                else
                {
                    slipstreamPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        public void ClearTrails()
        {
            SetEmitting(false);
            if (mainTrail != null && mainTrail) mainTrail.Clear();
            if (slipstreamPs != null && slipstreamPs) slipstreamPs.Clear();
            if (wallSparkPs != null && wallSparkPs) wallSparkPs.Clear();
            if (wallEmberPs != null && wallEmberPs) wallEmberPs.Clear();
        }

        public void TriggerWallBounceSpark(Vector3 contactPoint, Vector3 normal)
        {
            // Direct bounces strictly do NOT produce sparks (only friction slides do)
        }

        private float lastSlideSparkTime = 0f;

        /// <summary>
        /// Emits translucent needle sparks and scattered micro-embers strictly while sliding at speed.
        /// </summary>
        public void EmitWallSlideSparks(Vector3 contactPoint, Vector3 tangentDir, float speedRatio)
        {
            if (wallSparkPs == null || !wallSparkPs || wallEmberPs == null || !wallEmberPs)
            {
                SetupWallSparkParticles();
            }
            if (wallSparkPs == null || !wallSparkPs || speedRatio <= 0.001f) return;

            if (Time.time - lastSlideSparkTime < 0.012f) return;
            lastSlideSparkTime = Time.time;

            Vector3 sprayDir = -tangentDir.normalized;
            sprayDir.y = UnityEngine.Random.Range(0.18f, 0.52f); // Upward spray off wall

            Vector3 spawnPos = new Vector3(contactPoint.x, 0.12f, contactPoint.z);

            // 1. Abundant needle streaks (scaled by speed ratio)
            wallSparkPs.transform.position = spawnPos;
            if (sprayDir.sqrMagnitude > 0.01f)
            {
                wallSparkPs.transform.rotation = Quaternion.LookRotation(sprayDir);
            }
            int sparkCount = Mathf.RoundToInt(Mathf.Lerp(14, 52, Mathf.Clamp01(speedRatio)));
            wallSparkPs.Emit(sparkCount);

            // 2. Scattering micro-specks / embers (scaled by speed ratio)
            if (wallEmberPs != null)
            {
                wallEmberPs.transform.position = spawnPos;
                Vector3 emberDir = -tangentDir.normalized + new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(0.20f, 0.65f), UnityEngine.Random.Range(-0.35f, 0.35f));
                wallEmberPs.transform.rotation = Quaternion.LookRotation(emberDir.normalized);
                int emberCount = Mathf.RoundToInt(Mathf.Lerp(8, 32, Mathf.Clamp01(speedRatio)));
                wallEmberPs.Emit(emberCount);
            }
        }

        private void LateUpdate()
        {
            if (trailAnchor != null)
            {
                Vector3 p = transform.position;
                trailAnchor.position = new Vector3(p.x, 0.08f, p.z);
                trailAnchor.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            if (rb != null && !rb.isKinematic)
            {
                float speed = rb.linearVelocity.magnitude;
                if (speed < 0.4f && mainTrail != null && mainTrail.emitting)
                {
                    SetEmitting(false);
                }
            }
        }

        private void OnDestroy()
        {
            if (trailAnchor != null)
            {
                Destroy(trailAnchor.gameObject);
            }
            if (wallSparkPs != null)
            {
                Destroy(wallSparkPs.gameObject);
            }
            if (wallEmberPs != null)
            {
                Destroy(wallEmberPs.gameObject);
            }
        }
    }
}
