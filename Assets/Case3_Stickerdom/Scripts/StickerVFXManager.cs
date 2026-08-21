using System;
using UnityEngine;

namespace Stickerdom
{
    public class StickerVFXManager : MonoBehaviour
    {
        public static StickerVFXManager Instance { get; private set; }

        [Header("VFX Prefabs")]
        [Tooltip("Soft circle puff burst particle spawned when a sticker is peeled.")]
        [SerializeField] private GameObject peelPuffPrefab;

        [Tooltip("Golden 4-point star sparkle burst particle spawned when a sticker lands/stamps.")]
        [SerializeField] private GameObject stampSparklePrefab;

        [Header("Settings")]
        [Tooltip("Sorting order for spawned particles to render above all 2D sprites.")]
        [SerializeField] private int vfxSortingOrder = 150;

        [Tooltip("Z-offset towards camera so particles are always in front.")]
        [SerializeField] private float zOffset = -1.0f;

        [Tooltip("Auto destroy spawned particle instances after duration in seconds.")]
        [SerializeField] private float autoDestroyDelay = 1.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Instantiates a soft puff particle burst at the peeling position.
        /// </summary>
        public void PlayPeelVFX(Vector3 position)
        {
            SpawnAndPlay(peelPuffPrefab, position);
        }

        /// <summary>
        /// Instantiates a golden star sparkle burst at the target stamp position.
        /// </summary>
        public void PlayStampVFX(Vector3 position)
        {
            SpawnAndPlay(stampSparklePrefab, position);
        }

        private void SpawnAndPlay(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;

            Vector3 spawnPos = new Vector3(position.x, position.y, zOffset);
            GameObject instance = Instantiate(prefab, spawnPos, Quaternion.identity);

            // Force high sorting order so it is guaranteed to render above all 2D sprites
            var renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var r in renderers)
            {
                r.sortingOrder = vfxSortingOrder;
            }

            // Force play particle systems immediately
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                ps.Clear();
                ps.Play(true);
            }

            Destroy(instance, autoDestroyDelay);
        }
    }
}
