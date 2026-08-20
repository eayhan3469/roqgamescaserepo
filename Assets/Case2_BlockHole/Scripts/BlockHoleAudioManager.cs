using System;
using UnityEngine;

namespace BlockHole
{
    [ExecuteAlways]
    public class BlockHoleAudioManager : MonoBehaviour
    {
        public static BlockHoleAudioManager Instance { get; private set; }

        private const string MutePrefKey = "BlockHole_Audio_Muted";
        private const string MasterVolumePrefKey = "BlockHole_Audio_MasterVolume";

        [Header("Audio Clips")]
        [Tooltip("Played when a block is tapped/picked up.")]
        [SerializeField] private AudioClip[] pickupClips;

        [Tooltip("Played when a block slides across grid tiles.")]
        [SerializeField] private AudioClip slideClip;

        [Tooltip("Played when a block is released and snaps back into place on the grid.")]
        [SerializeField] private AudioClip snapBackClip;

        [Tooltip("Played when a block is swallowed into a hole.")]
        [SerializeField] private AudioClip[] dropClips;

        [Tooltip("Played when a block shatters at the bottom of the hole.")]
        [SerializeField] private AudioClip[] shatterClips;

        [Tooltip("Played when all blocks are cleared / victory.")]
        [SerializeField] private AudioClip victoryClip;

        [Header("Audio Source & Pitch Settings")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private float minPitch = 0.95f;
        [SerializeField] private float maxPitch = 1.05f;

        [Header("Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float pickupVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float snapBackVolume = 0.90f;
        [Range(0f, 1f)] [SerializeField] private float dropVolume = 0.95f;
        [Range(0f, 1f)] [SerializeField] private float shatterVolume = 1.0f;
        [SerializeField] private bool isMuted = false;

        public AudioClip[] PickupClips { get => pickupClips; set => pickupClips = value; }
        public AudioClip SlideClip { get => slideClip; set => slideClip = value; }
        public AudioClip SnapBackClip { get => snapBackClip; set => snapBackClip = value; }
        public AudioClip[] DropClips { get => dropClips; set => dropClips = value; }
        public AudioClip[] ShatterClips { get => shatterClips; set => shatterClips = value; }
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
                UpdateAudioSourcesVolume();
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
                UpdateAudioSourcesVolume();
            }
        }

        public float PickupVolume { get => pickupVolume; set => pickupVolume = Mathf.Clamp01(value); }
        public float SnapBackVolume { get => snapBackVolume; set => snapBackVolume = Mathf.Clamp01(value); }
        public float DropVolume { get => dropVolume; set => dropVolume = Mathf.Clamp01(value); }
        public float ShatterVolume { get => shatterVolume; set => shatterVolume = Mathf.Clamp01(value); }

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

            InitializeAudioSources();
            LoadAudioPreferences();
            AutoLoadClipsIfEmpty();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            InitializeAudioSources();
            AutoLoadClipsIfEmpty();
        }

        private void InitializeAudioSources()
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
            sfxSource.spatialBlend = 0f;
        }

        private void LoadAudioPreferences()
        {
            isMuted = PlayerPrefs.GetInt(MutePrefKey, 0) == 1;
            masterVolume = PlayerPrefs.GetFloat(MasterVolumePrefKey, 1.0f);
            UpdateAudioSourcesVolume();
        }

        private void UpdateAudioSourcesVolume()
        {
            float effectiveVolume = isMuted ? 0f : masterVolume;
            if (sfxSource != null) sfxSource.volume = effectiveVolume;
        }

        private void AutoLoadClipsIfEmpty()
        {
            if (pickupClips == null || pickupClips.Length == 0 || pickupClips[0] == null)
            {
                pickupClips = new AudioClip[]
                {
                    Resources.Load<AudioClip>("AudioClips/AUC_Pickup_01") ?? Resources.Load<AudioClip>("AUC_Pickup_01"),
                    Resources.Load<AudioClip>("AudioClips/AUC_Pickup_02") ?? Resources.Load<AudioClip>("AUC_Pickup_02")
                };
            }

            if (slideClip == null)
            {
                slideClip = Resources.Load<AudioClip>("AudioClips/AUC_SlideFriction") ?? Resources.Load<AudioClip>("AUC_SlideFriction");
            }

            if (snapBackClip == null)
            {
                snapBackClip = Resources.Load<AudioClip>("AudioClips/AUC_SnapBack") ?? Resources.Load<AudioClip>("AUC_SnapBack");
            }

            if (dropClips == null || dropClips.Length == 0 || dropClips[0] == null)
            {
                dropClips = new AudioClip[]
                {
                    Resources.Load<AudioClip>("AudioClips/AUC_DropSwallowed_01") ?? Resources.Load<AudioClip>("AUC_DropSwallowed_01"),
                    Resources.Load<AudioClip>("AudioClips/AUC_DropSwallowed_02") ?? Resources.Load<AudioClip>("AUC_DropSwallowed_02")
                };
            }

            if (shatterClips == null || shatterClips.Length == 0 || shatterClips[0] == null)
            {
                shatterClips = new AudioClip[]
                {
                    Resources.Load<AudioClip>("AudioClips/AUC_Shatter_01") ?? Resources.Load<AudioClip>("AUC_Shatter_01"),
                    Resources.Load<AudioClip>("AudioClips/AUC_Shatter_02") ?? Resources.Load<AudioClip>("AUC_Shatter_02")
                };
            }

            if (victoryClip == null)
            {
                victoryClip = Resources.Load<AudioClip>("AudioClips/AUC_Victory") ?? Resources.Load<AudioClip>("AUC_Victory");
            }
        }

        public void ToggleMute()
        {
            IsMuted = !IsMuted;
        }

        public void SetMasterVolume(float volume)
        {
            MasterVolume = volume;
        }

        public void SetMuted(bool mute)
        {
            IsMuted = mute;
        }

        public void PlayPickupSound()
        {
            if (pickupClips == null || pickupClips.Length == 0) return;
            AudioClip clip = pickupClips[UnityEngine.Random.Range(0, pickupClips.Length)];
            PlayClipWithPitch(clip, pickupVolume, true);
        }

        public void PlaySlideSound()
        {
            // Disabled for now per user request
        }

        public void PlaySnapBackSound()
        {
            if (snapBackClip == null) return;
            PlayClipWithPitch(snapBackClip, snapBackVolume, true);
        }

        public void PlayDropSound()
        {
            if (dropClips == null || dropClips.Length == 0) return;
            AudioClip clip = dropClips[UnityEngine.Random.Range(0, dropClips.Length)];
            PlayClipWithPitch(clip, dropVolume, true);
        }

        public void PlayShatterSound()
        {
            if (shatterClips == null || shatterClips.Length == 0) return;
            AudioClip clip = shatterClips[UnityEngine.Random.Range(0, shatterClips.Length)];
            PlayClipWithPitch(clip, shatterVolume, true);
        }

        public void PlayVictorySound()
        {
            // Disabled for now per user request
        }

        private void PlayClipWithPitch(AudioClip clip, float volume, bool randomizePitch)
        {
            if (clip == null || isMuted || masterVolume <= 0.001f) return;

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

            sfxSource.PlayOneShot(clip, volume * masterVolume);
        }
    }
}
