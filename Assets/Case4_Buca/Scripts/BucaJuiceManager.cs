using System;
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

        [Header("Screenshake Settings")]
        [SerializeField] private float baseShakeDuration = 0.15f;
        [SerializeField] private float minShakeStrength = 0.14f;
        [SerializeField] private float maxShakeStrength = 0.35f;
        [SerializeField] private int shakeVibrato = 28;
        [SerializeField] private float shakeRandomness = 90.0f;
        [SerializeField] private float shakeCooldown = 0.06f;

        [Header("VFX Prefab")]
        [SerializeField] private GameObject hitVfxPrefab;

        private Camera mainCamera;
        private Vector3 initialCamPos;
        private float lastShakeTime = -1f;
        private Tween currentShakeTween;
        private Queue<ParticleSystem> vfxPool = new Queue<ParticleSystem>();

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

        private void InitCamera()
        {
            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                initialCamPos = mainCamera.transform.position;
            }
        }

        private void LoadVfxPrefab()
        {
            if (hitVfxPrefab == null)
            {
                hitVfxPrefab = Resources.Load<GameObject>("Case4/GreenShatter");
            }
        }

        /// <summary>
        /// Triggers full visceral impact juice: micro-screenshake, audio crunch, and particle sparks.
        /// </summary>
        public void TriggerImpactJuice(Vector3 hitPoint, Vector3 normal, float speedRatio)
        {
            TriggerCameraShake(speedRatio);

            if (BucaAudioManager.Instance != null)
            {
                BucaAudioManager.Instance.PlayBlockImpactSound(speedRatio);
            }

            SpawnHitVFX(hitPoint, normal);
        }

        /// <summary>
        /// Snappy camera screenshake that returns cleanly to initial camera position.
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
                strength: new Vector3(strength, strength * 0.5f, strength),
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
            renderer.material = new Material(Shader.Find("Sprites/Default"));

            return go;
        }

        private void OnDestroy()
        {
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
