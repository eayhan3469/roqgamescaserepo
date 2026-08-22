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
        private Rigidbody rb;

        private static Material sharedStreakedTrailMat;
        private static Material sharedSoftCircleMat;
        private static Material sharedSoftStarMat;

        public static Material GetOrCreateStreakedTrailMaterial()
        {
            if (sharedStreakedTrailMat == null)
            {
                sharedStreakedTrailMat = Resources.Load<Material>("PFX_BucaStreakedTrail");

                if (sharedStreakedTrailMat == null)
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
            if (sharedSoftCircleMat == null)
            {
                sharedSoftCircleMat = Resources.Load<Material>("PFX_SoftCircleAdditive")
                                   ?? Resources.Load<Material>("PFX_BucaSoft");

                if (sharedSoftCircleMat == null)
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
            if (sharedSoftStarMat == null)
            {
                sharedSoftStarMat = Resources.Load<Material>("PFX_SoftStarAdditive")
                                 ?? Resources.Load<Material>("PFX_BucaStar");

                if (sharedSoftStarMat == null)
                {
                    Shader shader = Shader.Find("Buca/SoftParticleAdditive")
                                 ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                 ?? Shader.Find("Sprites/Default");
                    sharedSoftStarMat = new Material(shader) { name = "PFX_ProceduralSoftStar" };
                }
            }
            return sharedSoftStarMat;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            GameObject anchorGo = new GameObject("VFX_DiscTrailAnchor");
            trailAnchor = anchorGo.transform;
            trailAnchor.position = new Vector3(transform.position.x, 0.08f, transform.position.z);
            trailAnchor.rotation = Quaternion.Euler(90f, 0f, 0f);

            SetupMainTrail();
            SetupSlipstreamParticles();
            SetupWallSparkParticles();

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
            GameObject sparkGo = new GameObject("VFX_WallSparks");
            sparkGo.transform.SetParent(trailAnchor);
            sparkGo.transform.localPosition = Vector3.zero;

            wallSparkPs = sparkGo.AddComponent<ParticleSystem>();
            wallSparkPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = wallSparkPs.main;
            main.duration = 0.25f;
            main.loop = false;
            main.startLifetime = 0.25f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = wallSparkPs.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            var shape = wallSparkPs.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.1f;

            var colorOverLifetime = wallSparkPs.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.5f), new GradientColorKey(new Color(1f, 0.3f, 0.1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var renderer = sparkGo.GetComponent<ParticleSystemRenderer>();
            renderer.material = customParticleMaterial != null ? customParticleMaterial : GetOrCreateSoftStarMaterial();
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
            if (mainTrail != null) mainTrail.Clear();
            if (slipstreamPs != null) slipstreamPs.Clear();
            if (wallSparkPs != null) wallSparkPs.Clear();
        }

        public void TriggerWallBounceSpark(Vector3 contactPoint, Vector3 normal)
        {
            if (wallSparkPs == null) return;

            wallSparkPs.transform.position = contactPoint + normal * 0.05f;
            if (normal.sqrMagnitude > 0.01f)
            {
                wallSparkPs.transform.rotation = Quaternion.LookRotation(normal);
            }

            wallSparkPs.Emit(8);
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
        }
    }
}
