using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Buca
{
    public class BucaJuiceManager : MonoBehaviour
    {
        private static BucaJuiceManager instance;
        public static BucaJuiceManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<BucaJuiceManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("[BucaJuiceManager]");
                        instance = go.AddComponent<BucaJuiceManager>();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Screenshake Settings (Block Destruction)")]
        [SerializeField] private float baseShakeDuration = 0.12f;
        [SerializeField] private float minShakeStrength = 0.10f;
        [SerializeField] private float maxShakeStrength = 0.25f;
        [SerializeField] private int shakeVibrato = 26;
        [SerializeField] private float shakeRandomness = 85.0f;
        [SerializeField] private float shakeCooldown = 0.08f;

        [Header("Micro Hit-Stop Settings")]
        [SerializeField] private bool enableHitStop = true;
        [SerializeField] private float hitStopSlowScale = 0.04f;
        [SerializeField] private float minHitStopDuration = 0.028f;
        [SerializeField] private float maxHitStopDuration = 0.042f;
        [SerializeField] private float hitStopCooldown = 0.45f;

        [Header("Ground Shockwave Settings")]
        [SerializeField] private bool enableShockwave = false;
        [SerializeField] private float shockwaveDuration = 0.40f;
        [SerializeField] private float shockwaveBaseSize = 10.5f;

        [Header("VFX Prefab")]
        [SerializeField] private GameObject hitVfxPrefab;

        private Camera mainCamera;
        private Vector3 initialCamPos;
        private float lastShakeTime = -1f;
        private float lastHitStopTime = -1f;
        private Tween currentShakeTween;
        private Coroutine hitStopCoroutine;

        private Queue<ParticleSystem> vfxPool = new Queue<ParticleSystem>();
        private Queue<ParticleSystem> shockwavePool = new Queue<ParticleSystem>();
        private Queue<ParticleSystem> blockWarpPool = new Queue<ParticleSystem>();

        private ParticleSystem leftConfettiCannon;
        private ParticleSystem rightConfettiCannon;

        private static Material shockwaveMaterial;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitCamera();
            LoadVfxPrefab();
        }

        private void OnEnable()
        {
            InitCamera();
            Time.timeScale = 1.0f;
        }

        private void OnDisable()
        {
            Time.timeScale = 1.0f;
        }

        private void InitCamera()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            if (mainCamera != null)
            {
                initialCamPos = mainCamera.transform.position;
            }
        }

        private void LoadVfxPrefab()
        {
            if (hitVfxPrefab == null)
            {
                hitVfxPrefab = Resources.Load<GameObject>("Prefabs/PFX_BlockShatter");
            }
        }

        /// <summary>
        /// Triggers full visceral impact juice: micro-screenshake, hit-stop, audio crunch, and particle sparks.
        /// </summary>
        public void TriggerImpactJuice(Vector3 hitPoint, Vector3 normal, float speedRatio)
        {
            TriggerCameraShake(speedRatio);
            TriggerHitStop(speedRatio);

            if (BucaAudioManager.Instance != null)
            {
                BucaAudioManager.Instance.PlayBlockImpactSound(speedRatio);
            }

            SpawnHitVFX(hitPoint, normal);
        }

        /// <summary>
        /// Smashes time to a micro-freeze (0.035s) on direct block hit so the impact feels massive and heavy.
        /// </summary>
        public void TriggerHitStop(float speedRatio)
        {
            if (!enableHitStop) return;
            if (speedRatio < 0.35f) return; // Only on solid impacts
            if (Time.unscaledTime - lastHitStopTime < hitStopCooldown) return;
            lastHitStopTime = Time.unscaledTime;

            float freezeDuration = Mathf.Lerp(minHitStopDuration, maxHitStopDuration, Mathf.Clamp01(speedRatio));

            if (hitStopCoroutine != null)
            {
                StopCoroutine(hitStopCoroutine);
            }
            hitStopCoroutine = StartCoroutine(HitStopRoutine(freezeDuration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            Time.timeScale = hitStopSlowScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Time.timeScale = 1.0f;
            hitStopCoroutine = null;
        }

        /// <summary>
        /// Punchy, satisfying destruction screenshake when blocks are smashed and knocked down.
        /// </summary>
        public void TriggerCameraShake(float speedRatio)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            if (Time.time - lastShakeTime < shakeCooldown) return;
            lastShakeTime = Time.time;

            float strength = Mathf.Lerp(minShakeStrength, maxShakeStrength, Mathf.Clamp01(speedRatio));

            if (currentShakeTween != null && currentShakeTween.IsActive())
            {
                currentShakeTween.Kill();
            }

            mainCamera.transform.position = initialCamPos;
            currentShakeTween = mainCamera.transform.DOShakePosition(
                baseShakeDuration,
                strength: new Vector3(strength, strength * 0.45f, strength),
                vibrato: shakeVibrato,
                randomness: shakeRandomness,
                snapping: false,
                fadeOut: true
            ).OnComplete(() =>
            {
                if (mainCamera != null) mainCamera.transform.position = initialCamPos;
            });
        }

        /// <summary>
        /// Spawns a horizontal ground-parallel neon shockwave ring that expands and dissipates outward across the floor.
        /// </summary>
        public void SpawnGroundShockwave(Vector3 hitPoint, float speedRatio)
        {
            if (!enableShockwave) return;

            ParticleSystem ps = GetPooledShockwave();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.transform.position = new Vector3(hitPoint.x, 0.05f, hitPoint.z);

                var main = ps.main;
                main.startSize = shockwaveBaseSize * Mathf.Lerp(0.85f, 1.25f, Mathf.Clamp01(speedRatio));

                ps.gameObject.SetActive(true);
                ps.Play();
            }
        }

        private ParticleSystem GetPooledShockwave()
        {
            if (shockwavePool.Count > 0)
            {
                ParticleSystem ps = shockwavePool.Dequeue();
                if (ps != null)
                {
                    shockwavePool.Enqueue(ps);
                    return ps;
                }
            }

            GameObject shockwaveObj = CreateProceduralShockwaveSystem();
            if (shockwaveObj != null)
            {
                shockwaveObj.transform.SetParent(transform);
                ParticleSystem ps = shockwaveObj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    shockwavePool.Enqueue(ps);
                    return ps;
                }
            }

            return null;
        }

        private GameObject CreateProceduralShockwaveSystem()
        {
            GameObject go = new GameObject("ProceduralShockwaveRing");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = shockwaveDuration;
            main.loop = false;
            main.startLifetime = shockwaveDuration;
            main.startSpeed = 0f;
            main.startSize = shockwaveBaseSize;
            main.startColor = new Color(0.40f, 1.20f, 0.90f, 1.0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var shape = ps.shape;
            shape.enabled = false;

            // Size Over Lifetime: energetic outward sweep from small impact center to huge ring
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.08f, 3.8f, 3.8f),
                new Keyframe(1f, 1.0f, 0.1f, 0.0f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            // Color Over Lifetime: high sustained brightness during expansion, then soft fade
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.45f, 1.20f, 0.90f), 0f), new GradientColorKey(new Color(0.20f, 1.0f, 0.70f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0f), new GradientAlphaKey(0.95f, 0.50f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOverLifetime.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.material = GetOrCreateShockwaveMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go;
        }

        private static Material GetOrCreateShockwaveMaterial()
        {
            if (shockwaveMaterial != null) return shockwaveMaterial;

            Shader s = Shader.Find("Buca/ShockwaveRing")
                    ?? Shader.Find("Buca/SoftParticleAdditive")
                    ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Particles/Standard Unlit");

            shockwaveMaterial = new Material(s);
            shockwaveMaterial.name = "PFX_ShockwaveRing_Runtime";
            shockwaveMaterial.renderQueue = 3100;

            return shockwaveMaterial;
        }

        /// <summary>
        /// Sci-fi warp vortex ring & sparkle implosion when a block warps away.
        /// </summary>
        public void SpawnBlockWarpVFX(Vector3 position)
        {
            ParticleSystem ps = GetPooledBlockWarp();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.transform.position = position;
                ps.gameObject.SetActive(true);
                ps.Play();
            }
        }

        public void SpawnBlockPopVFX(Vector3 position)
        {
            SpawnBlockWarpVFX(position);
        }

        private ParticleSystem GetPooledBlockWarp()
        {
            if (blockWarpPool.Count > 0)
            {
                ParticleSystem ps = blockWarpPool.Dequeue();
                if (ps != null)
                {
                    blockWarpPool.Enqueue(ps);
                    return ps;
                }
            }

            GameObject go = new GameObject("ProceduralBlockWarpVFX");
            go.transform.SetParent(transform);

            // Clean micro-sparkle twinkle poof (zero rings)
            ParticleSystem newPs = go.AddComponent<ParticleSystem>();
            newPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = newPs.main;
            main.duration = 0.30f;
            main.loop = false;
            main.startLifetime = 0.25f;
            main.startSpeed = 2.5f;
            main.startSize = 0.20f;
            main.startColor = new Color(0.4f, 1.2f, 0.9f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = newPs.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 8) });

            var shape = newPs.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var sizeOL = newPs.sizeOverLifetime;
            sizeOL.enabled = true;
            sizeOL.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = BucaDiscTrailEffect.GetOrCreateSoftStarMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            blockWarpPool.Enqueue(newPs);
            return newPs;
        }

        /// <summary>
        /// Spectacular Dual Confetti & Firework Cannons positioned precisely at the Left & Right corners of the screen!
        /// </summary>
        public void SpawnDualVictoryCannons(Vector3 arenaCenter)
        {
            if (mainCamera == null) mainCamera = Camera.main;

            // Dynamically project exact screen corners onto the ground plane (y = 0.2f)
            Vector3 leftCornerPos = GetScreenWorldGroundPosition(0.05f, 0.12f);
            Vector3 rightCornerPos = GetScreenWorldGroundPosition(0.95f, 0.12f);
            Vector3 screenUpperCenterPos = GetScreenWorldGroundPosition(0.50f, 0.70f);

            // Calculate precise aiming angles from corners towards screen upper center
            Vector3 leftAimDir = (screenUpperCenterPos - leftCornerPos).normalized + Vector3.up * 1.35f;
            Vector3 rightAimDir = (screenUpperCenterPos - rightCornerPos).normalized + Vector3.up * 1.35f;

            if (leftConfettiCannon == null)
            {
                leftConfettiCannon = CreateSingleCannonSystem("LeftConfettiCannon");
            }

            if (rightConfettiCannon == null)
            {
                rightConfettiCannon = CreateSingleCannonSystem("RightConfettiCannon");
            }

            if (leftConfettiCannon != null)
            {
                leftConfettiCannon.transform.position = leftCornerPos;
                leftConfettiCannon.transform.rotation = Quaternion.LookRotation(leftAimDir);
                leftConfettiCannon.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                leftConfettiCannon.gameObject.SetActive(true);
                leftConfettiCannon.Play(true);
            }

            if (rightConfettiCannon != null)
            {
                rightConfettiCannon.transform.position = rightCornerPos;
                rightConfettiCannon.transform.rotation = Quaternion.LookRotation(rightAimDir);
                rightConfettiCannon.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                rightConfettiCannon.gameObject.SetActive(true);
                rightConfettiCannon.Play(true);
            }
        }

        private Vector3 GetScreenWorldGroundPosition(float viewportX, float viewportY)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return new Vector3(-28.5f, 0.2f, -15.5f);

            Ray ray = mainCamera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, 0.2f, 0f));

            if (groundPlane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            return mainCamera.transform.position + mainCamera.transform.forward * 12f;
        }

        public void SpawnVictoryCelebration(Vector3 arenaCenter)
        {
            SpawnDualVictoryCannons(arenaCenter);
        }

        private ParticleSystem CreateSingleCannonSystem(string cannonName)
        {
            GameObject root = new GameObject("VFX_" + cannonName);
            root.transform.SetParent(transform);

            // 1. Confetti Streamers
            ParticleSystem confettiPs = root.AddComponent<ParticleSystem>();
            confettiPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = confettiPs.main;
            main.duration = 2.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10.0f, 18.0f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.14f, 0.25f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
            main.startSizeZ = 0.05f;
            main.gravityModifier = 0.90f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var startColor = main.startColor;
            startColor.mode = ParticleSystemGradientMode.RandomColor;
            Gradient festiveGrad = new Gradient();
            festiveGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1.0f, 0.85f, 0.1f), 0.0f),  // Gold
                    new GradientColorKey(new Color(1.0f, 0.2f, 0.6f), 0.25f),  // Hot Pink
                    new GradientColorKey(new Color(0.1f, 0.9f, 1.0f), 0.50f),  // Cyan
                    new GradientColorKey(new Color(0.3f, 1.0f, 0.4f), 0.75f),  // Lime
                    new GradientColorKey(new Color(1.0f, 0.5f, 0.1f), 1.0f)   // Orange
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0f), new GradientAlphaKey(1.0f, 1f) }
            );
            startColor.gradient = festiveGrad;
            main.startColor = startColor;

            var emission = confettiPs.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, 95),
                new ParticleSystem.Burst(0.18f, 65)
            });

            var shape = confettiPs.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.4f;
            shape.rotation = Vector3.zero;

            var rotOL = confettiPs.rotationOverLifetime;
            rotOL.enabled = true;
            rotOL.separateAxes = true;
            rotOL.x = new ParticleSystem.MinMaxCurve(-360f, 360f);
            rotOL.y = new ParticleSystem.MinMaxCurve(-360f, 360f);
            rotOL.z = new ParticleSystem.MinMaxCurve(-360f, 360f);

            var colorOL = confettiPs.colorOverLifetime;
            colorOL.enabled = true;
            Gradient alphaFadeGrad = new Gradient();
            alphaFadeGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0f), new GradientAlphaKey(1.0f, 0.75f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOL.color = alphaFadeGrad;

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = BucaDiscTrailEffect.GetOrCreateSoftCircleMaterial();

            // 2. Child Sparkler Stars Cannon
            GameObject starsGo = new GameObject("StarsChild");
            starsGo.transform.SetParent(root.transform, false);
            ParticleSystem starsPs = starsGo.AddComponent<ParticleSystem>();
            starsPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var starsMain = starsPs.main;
            starsMain.duration = 2.0f;
            starsMain.loop = false;
            starsMain.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.0f);
            starsMain.startSpeed = new ParticleSystem.MinMaxCurve(12.0f, 20.0f);
            starsMain.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
            starsMain.startColor = new Color(1.0f, 0.94f, 0.35f, 1.0f); // Radiant Gold
            starsMain.gravityModifier = 0.70f;
            starsMain.simulationSpace = ParticleSystemSimulationSpace.World;
            starsMain.playOnAwake = false;

            var starsEmission = starsPs.emission;
            starsEmission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.04f, 50),
                new ParticleSystem.Burst(0.22f, 35)
            });

            var starsShape = starsPs.shape;
            starsShape.shapeType = ParticleSystemShapeType.Cone;
            starsShape.angle = 20f;
            starsShape.radius = 0.3f;
            starsShape.rotation = Vector3.zero;

            var starsSizeOL = starsPs.sizeOverLifetime;
            starsSizeOL.enabled = true;
            starsSizeOL.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var starsRend = starsGo.GetComponent<ParticleSystemRenderer>();
            starsRend.material = BucaDiscTrailEffect.GetOrCreateSoftStarMaterial();

            return confettiPs;
        }

        /// <summary>
        /// Spawns particle burst at hit point with safe particle lifetime handling.
        /// </summary>
        public void SpawnHitVFX(Vector3 hitPoint, Vector3 normal)
        {
            ParticleSystem ps = GetPooledVFX();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.transform.position = hitPoint + normal * 0.1f;
                if (normal.sqrMagnitude > 0.01f)
                {
                    ps.transform.rotation = Quaternion.LookRotation(normal);
                }
                ps.gameObject.SetActive(true);
                ps.Play();
            }
        }

        private ParticleSystem GetPooledVFX()
        {
            if (vfxPool.Count > 0)
            {
                ParticleSystem ps = vfxPool.Dequeue();
                if (ps != null)
                {
                    vfxPool.Enqueue(ps);
                    return ps;
                }
            }

            // Create new instance if pool empty
            GameObject vfxObj = null;
            if (hitVfxPrefab != null)
            {
                vfxObj = Instantiate(hitVfxPrefab);
            }
            else
            {
                vfxObj = CreateProceduralSparkVFX();
            }

            if (vfxObj != null)
            {
                vfxObj.transform.SetParent(transform);
                ParticleSystem ps = vfxObj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    vfxPool.Enqueue(ps);
                    return ps;
                }
            }

            return null;
        }

        private GameObject CreateProceduralSparkVFX()
        {
            GameObject go = new GameObject("ProceduralHitSparks");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            // Stop system safely before modifying properties to avoid Unity playing duration error
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = 0.3f;
            main.startSpeed = 6.0f;
            main.startSize = 0.18f;
            main.startColor = new Color(1.0f, 0.95f, 0.4f, 1.0f);
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 18) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.1f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = BucaDiscTrailEffect.GetOrCreateSoftStarMaterial();

            return go;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1.0f;
            if (currentShakeTween != null && currentShakeTween.IsActive())
            {
                currentShakeTween.Kill();
            }
            if (mainCamera != null)
            {
                mainCamera.transform.position = initialCamPos;
            }
        }
    }
}
