using System;
using UnityEngine;

namespace FitTheShape
{
    public class FitTheShapeAudioManager : MonoBehaviour
    {
        public static FitTheShapeAudioManager Instance { get; private set; }

        [Header("Audio Clips")]
        [Tooltip("Played when a shape is clicked and begins flight.")]
        [SerializeField] private AudioClip whooshLaunchClip;

        [Tooltip("Played when a shape lands in the target slot.")]
        [SerializeField] private AudioClip snapImpactClip;

        [Tooltip("Played when the wheel surface ripple wave begins propagating.")]
        [SerializeField] private AudioClip resonanceWobbleClip;

        [Tooltip("Played for success / completion polish.")]
        [SerializeField] private AudioClip successSparkleClip;

        [Header("Dedicated Audio Sources")]
        [SerializeField] private AudioSource launchSource;
        [SerializeField] private AudioSource snapSource;
        [SerializeField] private AudioSource wobbleSource;
        [SerializeField] private AudioSource sparkleSource;

        [Header("Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float launchVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float impactVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float wobbleVolume = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float sparkleVolume = 0.90f;

        [Header("Pitch Variation Settings")]
        [SerializeField] private float minPitch = 0.96f;
        [SerializeField] private float maxPitch = 1.04f;

        public AudioClip WhooshLaunchClip { get => whooshLaunchClip; set => whooshLaunchClip = value; }
        public AudioClip SnapImpactClip { get => snapImpactClip; set => snapImpactClip = value; }
        public AudioClip ResonanceWobbleClip { get => resonanceWobbleClip; set => resonanceWobbleClip = value; }
        public AudioClip SuccessSparkleClip { get => successSparkleClip; set => successSparkleClip = value; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeSources();
        }

        private void InitializeSources()
        {
            if (launchSource == null) launchSource = CreateDedicatedSource("LaunchSource");
            if (snapSource == null) snapSource = CreateDedicatedSource("SnapSource");
            if (wobbleSource == null) wobbleSource = CreateDedicatedSource("WobbleSource");
            if (sparkleSource == null) sparkleSource = CreateDedicatedSource("SparkleSource");
        }

        private AudioSource CreateDedicatedSource(string name)
        {
            Transform existing = transform.Find(name);
            if (existing != null && existing.TryGetComponent<AudioSource>(out var existingSrc))
            {
                return existingSrc;
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // Pure 2D crisp stereo sound
            return src;
        }

        public void PlayLaunchSound()
        {
            if (whooshLaunchClip == null) return;
            if (launchSource == null) InitializeSources();

            launchSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            launchSource.PlayOneShot(whooshLaunchClip, launchVolume);
        }

        public void PlaySnapImpactSound()
        {
            if (snapImpactClip == null) return;
            if (snapSource == null) InitializeSources();

            snapSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            snapSource.PlayOneShot(snapImpactClip, impactVolume);
        }

        public void PlayWobbleSound()
        {
            if (resonanceWobbleClip == null) return;
            if (wobbleSource == null) InitializeSources();

            wobbleSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
            wobbleSource.PlayOneShot(resonanceWobbleClip, wobbleVolume);
        }

        /// <summary>
        /// Plays the magical success sparkle sound on an isolated channel strictly locked at 1.0 pitch.
        /// </summary>
        public void PlaySuccessSound()
        {
            if (successSparkleClip == null) return;
            if (sparkleSource == null) InitializeSources();

            sparkleSource.pitch = 1.0f; // Strictly locked, zero drift
            sparkleSource.PlayOneShot(successSparkleClip, sparkleVolume);
        }
    }
}
