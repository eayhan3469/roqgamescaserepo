using System;
using UnityEngine;

namespace Buca
{
    public class BucaDiscTrailEffect : MonoBehaviour
    {
        [Header("Core Ribbon Trail (Sharp Inner Glow)")]
        [SerializeField] private float coreWidth = 1.15f;
        [SerializeField] private float coreLifetime = 0.28f;
        [SerializeField] private Color coreColorStart = new Color(1.0f, 1.0f, 1.0f, 0.95f);
        [SerializeField] private Color coreColorEnd = new Color(0.15f, 0.85f, 1.0f, 0.0f);

        [Header("Aura Ribbon Trail (Soft Outer Ambient Glow)")]
        [SerializeField] private float auraWidth = 1.65f;
        [SerializeField] private float auraLifetime = 0.38f;
        [SerializeField] private Color auraColorStart = new Color(0.15f, 0.85f, 1.0f, 0.45f);
        [SerializeField] private Color auraColorEnd = new Color(0.0f, 0.45f, 1.0f, 0.0f);

        [Header("Spark Particle Slipstream (Soft Glow Embers)")]
        [SerializeField] private float particlesPerMeter = 16f;
        [SerializeField] private float particleLifetime = 0.42f;
        [SerializeField] private float particleStartSize = 0.22f;
        [SerializeField] private Color particleColorStart = new Color(0.40f, 0.95f, 1.0f, 0.95f);
        [SerializeField] private Color particleColorEnd = new Color(1.0f, 0.85f, 0.20f, 0.0f);

        [Header("Material Customization (Optional)")]
        [SerializeField] private Material customTrailMaterial;
        [SerializeField] private Material customParticleMaterial;

        private Transform trailAnchor;
        private TrailRenderer coreTrail;
        private TrailRenderer auraTrail;
        private ParticleSystem slipstreamPs;
        private ParticleSystem wallSparkPs;
        private Rigidbody rb;

        private static Material sharedSoftCircleMat;
        private static Material sharedSoftStarMat;

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

            SetupCoreTrail();
            SetupAuraTrail();
            SetupSlipstreamParticles();
            SetupWallSparkParticles();

            SetEmitting(false);
        }

        private void SetupCoreTrail()
        {
            GameObject coreGo = new GameObject("VFX_CoreTrail");
            coreGo.transform.SetParent(trailAnchor);
            coreGo.transform.localPosition = Vector3.zero;
            coreGo.transform.localRotation = Quaternion.identity;

            coreTrail = coreGo.AddComponent<TrailRenderer>();
            coreTrail.time = coreLifetime;
            coreTrail.minVertexDistance = 0.06f;
            coreTrail.autodestruct = false;
            coreTrail.emitting = false;
            coreTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coreTrail.receiveShadows = false;
            coreTrail.alignment = LineAlignment.TransformZ;

            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, coreWidth);
            curve.AddKey(0.40f, coreWidth * 0.85f);
            curve.AddKey(0.75f, coreWidth * 0.50f);
            curve.AddKey(1f, 0.05f);
            coreTrail.widthCurve = curve;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(coreColorStart, 0f), new GradientColorKey(coreColorEnd, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(coreColorStart.a, 0f), new GradientAlphaKey(coreColorEnd.a, 1f) }
            );
            coreTrail.colorGradient = grad;

            coreTrail.material = customTrailMaterial != null ? customTrailMaterial : GetOrCreateSoftCircleMaterial();
        }

        private void SetupAuraTrail()
        {
            GameObject auraGo = new GameObject("VFX_AuraTrail");
            auraGo.transform.SetParent(trailAnchor);
            auraGo.transform.localPosition = new Vector3(0f, -0.01f, 0f);
            auraGo.transform.localRotation = Quaternion.identity;

            auraTrail = auraGo.AddComponent<TrailRenderer>();
            auraTrail.time = auraLifetime;
            auraTrail.minVertexDistance = 0.06f;
            auraTrail.autodestruct = false;
            auraTrail.emitting = false;
            auraTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            auraTrail.receiveShadows = false;
            auraTrail.alignment = LineAlignment.TransformZ;

            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, auraWidth);
            curve.AddKey(0.45f, auraWidth * 0.80f);
            curve.AddKey(0.80f, auraWidth * 0.40f);
            curve.AddKey(1f, 0.08f);
            auraTrail.widthCurve = curve;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(auraColorStart, 0f), new GradientColorKey(auraColorEnd, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(auraColorStart.a, 0f), new GradientAlphaKey(auraColorEnd.a, 1f) }
            );
            auraTrail.colorGradient = grad;

            auraTrail.material = customTrailMaterial != null ? customTrailMaterial : GetOrCreateSoftCircleMaterial();
        }

        private void SetupSlipstreamParticles()
        {
            GameObject psGo = new GameObject("VFX_SlipstreamParticles");
            psGo.transform.SetParent(trailAnchor);
            psGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            psGo.transform.localRotation = Quaternion.identity;

            slipstreamPs = psGo.AddComponent<ParticleSystem>();
            slipstreamPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = slipstreamPs.main;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = particleLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(particleStartSize * 0.6f, particleStartSize * 1.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.maxParticles = 150;

            var emission = slipstreamPs.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = particlesPerMeter;

            var shape = slipstreamPs.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;

            var colorOverLifetime = slipstreamPs.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(particleColorStart, 0f), new GradientColorKey(particleColorEnd, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = slipstreamPs.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, 0.05f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.material = customParticleMaterial != null ? customParticleMaterial : GetOrCreateSoftStarMaterial();
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.5f;
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
            if (coreTrail != null) coreTrail.emitting = active;
            if (auraTrail != null) auraTrail.emitting = active;

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
            if (coreTrail != null) coreTrail.Clear();
            if (auraTrail != null) auraTrail.Clear();
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
            // Keep trail anchor strictly in horizontal XZ ground plane (y = 0.08m above floor) and isolate from puck spin
            if (trailAnchor != null)
            {
                Vector3 p = transform.position;
                trailAnchor.position = new Vector3(p.x, 0.08f, p.z);
                trailAnchor.rotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // Dynamically scale trail opacity and particle rate with current movement speed
            if (rb != null && !rb.isKinematic)
            {
                float speed = rb.linearVelocity.magnitude;
                if (speed < 0.5f && coreTrail != null && coreTrail.emitting)
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
