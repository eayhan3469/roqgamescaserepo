using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Stickerdom
{
    public class StickerVFXManager : MonoBehaviour
    {
        public static StickerVFXManager Instance { get; private set; }

        [Header("VFX Prefabs")]
        [Tooltip("Golden 4-point star sparkle burst particle spawned when a sticker is peeled or lands.")]
        [SerializeField] private GameObject stampSparklePrefab;

        [Header("Peeling Adhesive Sparkle Trail Settings")]
        [Tooltip("Overall scale for adhesive stars popping out from under the peel crease.")]
        [Range(0.1f, 3.0f)] [SerializeField] private float peelSparkleScale = 1.0f;

        [Tooltip("Sparkle star life duration in seconds.")]
        [Range(0.10f, 0.8f)] [SerializeField] private float sparkleLifetime = 0.22f;

        [Header("Landing Stamp Cascade Settings")]
        [Tooltip("Number of stars in the rapid cascade (pıt-pıt effect).")]
        [Range(8, 40)] [SerializeField] private int stampCascadeCount = 18;

        [Tooltip("Delay in seconds between each star popping (pıt-pıt speed).")]
        [Range(0.005f, 0.05f)] [SerializeField] private float stampCascadeInterval = 0.016f;

        [Tooltip("Scale for stars in the landing cascade.")]
        [Range(0.2f, 3.0f)] [SerializeField] private float stampCascadeScale = 1.0f;

        [Header("Settings")]
        [Tooltip("Sorting order for spawned particles to render above all 2D sprites.")]
        [SerializeField] private int vfxSortingOrder = 200;

        [Tooltip("Z-offset towards camera so particles are always in front.")]
        [SerializeField] private float zOffset = -0.5f;

        [Tooltip("Auto destroy spawned particle instances after duration in seconds.")]
        [SerializeField] private float autoDestroyDelay = 1.5f;

        private ParticleSystem peelSparkleEmitter;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            SetupPeelSparkleEmitter();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void SetupPeelSparkleEmitter()
        {
            if (stampSparklePrefab == null) return;

            GameObject go = Instantiate(stampSparklePrefab, transform);
            go.name = "PeelSparkleTrailEmitter";
            go.transform.localPosition = Vector3.zero;

            peelSparkleEmitter = go.GetComponentInChildren<ParticleSystem>(true);
            if (peelSparkleEmitter != null)
            {
                var main = peelSparkleEmitter.main;
                main.loop = false;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = peelSparkleEmitter.emission;
                emission.enabled = false; // We manually emit with EmitParams

                var r = peelSparkleEmitter.GetComponent<ParticleSystemRenderer>();
                if (r != null)
                {
                    r.sortingOrder = vfxSortingOrder;
                }

                peelSparkleEmitter.Play();
            }
        }

        /// <summary>
        /// Emits a single crisp micro star from underneath the moving peel crease.
        /// </summary>
        public void EmitPeelSparkle(Vector3 worldPos, float scaleMultiplier = 1.0f)
        {
            if (peelSparkleEmitter == null) SetupPeelSparkleEmitter();
            if (peelSparkleEmitter == null) return;

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            emitParams.position = new Vector3(worldPos.x, worldPos.y, zOffset);
            emitParams.startSize = UnityEngine.Random.Range(0.06f, 0.12f) * peelSparkleScale * scaleMultiplier;
            emitParams.startLifetime = UnityEngine.Random.Range(sparkleLifetime * 0.8f, sparkleLifetime * 1.2f);
            emitParams.startColor = new Color(1.0f, 0.95f, 0.82f, 1.0f);
            emitParams.velocity = new Vector3(UnityEngine.Random.Range(-0.25f, 0.25f), UnityEngine.Random.Range(-0.25f, 0.25f), 0f);
            emitParams.rotation = UnityEngine.Random.Range(0f, 360f);

            peelSparkleEmitter.Emit(emitParams, 1);
        }

        /// <summary>
        /// Emits a prominent, glowing star sparkle on the sticker upon landing.
        /// </summary>
        public void EmitStampSparkle(Vector3 worldPos, float scale = 1.0f)
        {
            if (peelSparkleEmitter == null) SetupPeelSparkleEmitter();
            if (peelSparkleEmitter == null) return;

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            emitParams.position = new Vector3(worldPos.x, worldPos.y, zOffset);
            emitParams.startSize = UnityEngine.Random.Range(0.38f, 0.65f) * stampCascadeScale * scale;
            emitParams.startLifetime = 0.32f;
            emitParams.startColor = new Color(1.0f, 0.96f, 0.82f, 1.0f);
            emitParams.velocity = new Vector3(UnityEngine.Random.Range(-0.25f, 0.25f), UnityEngine.Random.Range(-0.25f, 0.25f), 0f);
            emitParams.rotation = UnityEngine.Random.Range(0f, 360f);

            peelSparkleEmitter.Emit(emitParams, 1);
        }

        /// <summary>
        /// Instantiates a sequential star sparkle cascade popping rapid-fire ("pıt-pıt-pıt-pıt") across the shape of the placed sticker!
        /// </summary>
        public void PlayStampCascade(Transform stickerTransform, Sprite sprite)
        {
            StartCoroutine(StampCascadeRoutine(stickerTransform, sprite));
        }

        private IEnumerator StampCascadeRoutine(Transform stickerTransform, Sprite sprite)
        {
            if (stickerTransform == null) yield break;

            Vector2 size = (sprite != null) ? sprite.rect.size / sprite.pixelsPerUnit : new Vector2(3.5f, 3.5f);
            float hw = size.x * 0.42f;
            float hh = size.y * 0.42f;

            int totalStars = stampCascadeCount;
            for (int i = 0; i < totalStars; i++)
            {
                if (stickerTransform == null) yield break;

                float t = i / (float)Mathf.Max(totalStars - 1, 1);
                // Sweep in an organic spiral/wave across the sticker body
                float angle = t * Mathf.PI * 3.8f;
                float radius = Mathf.Lerp(0.25f, 1.0f, t);
                float x = Mathf.Cos(angle) * hw * radius + UnityEngine.Random.Range(-hw * 0.15f, hw * 0.15f);
                float y = Mathf.Sin(angle) * hh * radius + UnityEngine.Random.Range(-hh * 0.15f, hh * 0.15f);

                Vector3 worldPt = stickerTransform.TransformPoint(new Vector3(x, y, 0f));
                EmitStampSparkle(worldPt, 1.0f);

                if (stampCascadeInterval > 0.001f)
                {
                    yield return new WaitForSeconds(stampCascadeInterval);
                }
            }
        }

        /// <summary>
        /// Instantiates a quick sparkle pop burst at the given position (e.g. sticker spawn / restart).
        /// </summary>
        public void PlayPeelVFX(Vector3 position)
        {
            PlayStampVFX(position);
        }

        /// <summary>
        /// Instantiates a golden star sparkle burst at the target stamp position upon landing.
        /// </summary>
        public void PlayStampVFX(Vector3 position)
        {
            SpawnAndPlay(stampSparklePrefab, position, 1.0f);
        }

        private GameObject SpawnAndPlay(GameObject prefab, Vector3 position, float scaleMultiplier = 1.0f)
        {
            if (prefab == null) return null;

            Vector3 spawnPos = new Vector3(position.x, position.y, zOffset);
            GameObject instance = Instantiate(prefab, spawnPos, Quaternion.identity);
            instance.transform.localScale = Vector3.one * scaleMultiplier;

            var renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var r in renderers)
            {
                r.sortingOrder = vfxSortingOrder;
            }

            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                ps.Clear();
                ps.Play(true);
            }

            Destroy(instance, autoDestroyDelay);
            return instance;
        }
    }
}
