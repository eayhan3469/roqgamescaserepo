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

        [Header("Audio Source & Pitch Settings")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private float minPitch = 0.95f;
        [SerializeField] private float maxPitch = 1.05f;

        [Header("Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float launchVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float impactVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float wobbleVolume = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float sparkleVolume = 0.90f;

        public AudioClip WhooshLaunchClip { get => whooshLaunchClip; set => whooshLaunchClip = value; }
        public AudioClip SnapImpactClip { get => snapImpactClip; set => snapImpactClip = value; }
        public AudioClip ResonanceWobbleClip { get => resonanceWobbleClip; set => resonanceWobbleClip = value; }
        public AudioClip SuccessSparkleClip { get => successSparkleClip; set => successSparkleClip = value; }
        public AudioSource SfxSource { get => sfxSource; set => sfxSource = value; }

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

            if (sfxSource == null)
            {
                sfxSource = GetComponent<AudioSource>();
                if (sfxSource == null)
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                }
            }

            sfxSource.playOnAwake = false;
        }

        public void PlayLaunchSound()
        {
            PlayClipWithPitch(whooshLaunchClip, launchVolume, true);
        }

        public void PlaySnapImpactSound()
        {
            PlayClipWithPitch(snapImpactClip, impactVolume, true);
        }

        public void PlayWobbleSound()
        {
            PlayClipWithPitch(resonanceWobbleClip, wobbleVolume, true);
        }

        public void PlaySuccessSound()
        {
            PlayClipWithPitch(successSparkleClip, sparkleVolume, false);
        }

        private void PlayClipWithPitch(AudioClip clip, float volume, bool randomizePitch)
        {
            if (clip == null) return;

            if (sfxSource == null)
            {
                sfxSource = GetComponent<AudioSource>();
                if (sfxSource == null)
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                }
            }

            if (randomizePitch)
            {
                sfxSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            }
            else
            {
                sfxSource.pitch = 1.0f;
            }

            sfxSource.PlayOneShot(clip, volume);
        }
    }
}
