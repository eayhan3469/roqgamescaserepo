using System;
using UnityEngine;

namespace Stickerdom
{
    [ExecuteAlways]
    public class StickerAudioManager : MonoBehaviour
    {
        public static StickerAudioManager Instance { get; private set; }

        private const string MutePrefKey = "Stickerdom_Audio_Muted";
        private const string MasterVolumePrefKey = "Stickerdom_Audio_MasterVolume";

        [Header("Audio Clips")]
        [Tooltip("Played when a sticker corner is tapped and begins peeling.")]
        [SerializeField] private AudioClip peelClip;

        [Tooltip("Played during the sticker's parabolic arc flight.")]
        [SerializeField] private AudioClip flyClip;

        [Tooltip("Played when the sticker unrolls, lands, and stamps onto the slot.")]
        [SerializeField] private AudioClip stampClip;

        [Tooltip("Played when all ghost slots on the album page are completed.")]
        [SerializeField] private AudioClip victoryClip;

        [Header("Audio Source & Pitch")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private float minPitch = 0.95f;
        [SerializeField] private float maxPitch = 1.05f;

        [Header("Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float peelVolume = 0.90f;
        [Range(0f, 1f)] [SerializeField] private float flyVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float stampVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float victoryVolume = 1.0f;
        [SerializeField] private bool isMuted = false;

        public AudioClip PeelClip { get => peelClip; set => peelClip = value; }
        public AudioClip FlyClip { get => flyClip; set => flyClip = value; }
        public AudioClip StampClip { get => stampClip; set => stampClip = value; }
        public AudioClip VictoryClip { get => victoryClip; set => victoryClip = value; }
        public AudioSource SfxSource { get => sfxSource; set => sfxSource = value; }

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MasterVolumePrefKey, masterVolume);
                PlayerPrefs.Save();
                UpdateAudioSourceVolume();
            }
        }

        public bool IsMuted
        {
            get => isMuted;
            set
            {
                isMuted = value;
                PlayerPrefs.SetInt(MutePrefKey, isMuted ? 1 : 0);
                PlayerPrefs.Save();
                UpdateAudioSourceVolume();
            }
        }

        public float PeelVolume { get => peelVolume; set => peelVolume = Mathf.Clamp01(value); }
        public float FlyVolume { get => flyVolume; set => flyVolume = Mathf.Clamp01(value); }
        public float StampVolume { get => stampVolume; set => stampVolume = Mathf.Clamp01(value); }
        public float VictoryVolume { get => victoryVolume; set => victoryVolume = Mathf.Clamp01(value); }

        private bool hasPlayedVictory = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this && Application.isPlaying)
            {
                Destroy(gameObject);
                return;
            }

            EnsureAudioListener();
            InitializeAudioSource();
            LoadAudioPreferences();
        }

        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            EnsureAudioListener();
            InitializeAudioSource();
        }

        private void OnValidate()
        {
            UpdateAudioSourceVolume();
        }

        private void EnsureAudioListener()
        {
            if (FindObjectOfType<AudioListener>() == null)
            {
                Camera cam = Camera.main;
                if (cam != null && cam.GetComponent<AudioListener>() == null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                    Debug.Log("[StickerAudioManager] Added AudioListener to Main Camera.");
                }
                else if (GetComponent<AudioListener>() == null)
                {
                    gameObject.AddComponent<AudioListener>();
                    Debug.Log("[StickerAudioManager] Added AudioListener to AudioManager.");
                }
            }
        }

        private void InitializeAudioSource()
        {
            if (sfxSource == null)
            {
                sfxSource = GetComponent<AudioSource>();
                if (sfxSource == null)
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                }
            }

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f; // 2D Sound (always hearable)
            UpdateAudioSourceVolume();
        }

        private void LoadAudioPreferences()
        {
            if (!Application.isPlaying) return;

            if (PlayerPrefs.HasKey(MasterVolumePrefKey))
            {
                masterVolume = PlayerPrefs.GetFloat(MasterVolumePrefKey, 1.0f);
            }

            if (PlayerPrefs.HasKey(MutePrefKey))
            {
                isMuted = PlayerPrefs.GetInt(MutePrefKey, 0) == 1;
            }

            UpdateAudioSourceVolume();
        }

        private void UpdateAudioSourceVolume()
        {
            if (sfxSource != null)
            {
                sfxSource.mute = isMuted;
                sfxSource.volume = masterVolume;
            }
        }

        /// <summary>
        /// Sökülme sesi (Peel sound with slight pitch variation).
        /// </summary>
        public void PlayPeelSound()
        {
            PlayClipWithPitch(peelClip, peelVolume, minPitch, maxPitch);
        }

        /// <summary>
        /// Kavisli uçuş sesi (Fly swoosh sound).
        /// </summary>
        public void PlayFlySound()
        {
            PlayClipWithPitch(flyClip, flyVolume, 1.0f, 1.0f);
        }

        /// <summary>
        /// Yapışma ve mühürleme sesi (Stamp sound with slight pitch variation).
        /// </summary>
        public void PlayStampSound()
        {
            PlayClipWithPitch(stampClip, stampVolume, minPitch, maxPitch);
        }

        /// <summary>
        /// Tüm sticker'lar yerleştiğinde çalan zafer sesi.
        /// </summary>
        public void PlayVictorySound()
        {
            if (hasPlayedVictory) return;
            hasPlayedVictory = true;
            PlayClipWithPitch(victoryClip, victoryVolume, 1.0f, 1.0f);
        }

        /// <summary>
        /// Sahnedeki tüm yuvaların dolup dolmadığını kontrol eder, hepsi doluysa zafer sesini çalar.
        /// </summary>
        public void CheckAllSlotsCompleted()
        {
            GhostSlot[] slots = FindObjectsOfType<GhostSlot>();
            if (slots == null || slots.Length == 0) return;

            foreach (var slot in slots)
            {
                if (slot != null && !slot.IsOccupied)
                {
                    return; // Henüz dolmamış yuva var
                }
            }

            PlayVictorySound();
        }

        private void PlayClipWithPitch(AudioClip clip, float volumeScale, float pMin, float pMax)
        {
            if (clip == null)
            {
                Debug.LogWarning("[StickerAudioManager] Audio clip is missing/null!");
                return;
            }

            if (isMuted || masterVolume <= 0.001f) return;

            InitializeAudioSource();
            if (sfxSource == null) return;

            float pitch = UnityEngine.Random.Range(pMin, pMax);
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, volumeScale * masterVolume);
        }

        public void ResetVictoryState()
        {
            hasPlayedVictory = false;
        }
    }
}
