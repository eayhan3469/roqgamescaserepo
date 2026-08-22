using System;
using UnityEngine;

namespace Buca
{
    [ExecuteAlways]
    public class BucaAudioManager : MonoBehaviour
    {
        private static BucaAudioManager instance;
        public static BucaAudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<BucaAudioManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("[BucaAudioManager]");
                        instance = go.AddComponent<BucaAudioManager>();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        private const string MutePrefKey = "Buca_Audio_Muted";
        private const string MasterVolumePrefKey = "Buca_Audio_MasterVolume";

        [Header("Audio Clips")]
        [Tooltip("Played when the disc smashes into a block at high impact.")]
        [SerializeField] private AudioClip[] blockImpactClips;

        [Tooltip("Played when blocks clatter, tumble, and hit each other or the floor.")]
        [SerializeField] private AudioClip[] blockClatterClips;

        [Tooltip("Played when the disc is released and launched.")]
        [SerializeField] private AudioClip launchClip;

        [Tooltip("Played when the disc bounces off a side wall or obstacle.")]
        [SerializeField] private AudioClip wallBounceClip;

        [Tooltip("Played when the disc hits a green neon obstacle.")]
        [SerializeField] private AudioClip obstacleDeflectClip;

        [Tooltip("Played when the level target is cleared.")]
        [SerializeField] private AudioClip victoryClip;

        [Header("Audio Source & Pitch Settings")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource clatterSource;
        [SerializeField] private AudioSource victorySource;
        [SerializeField] private float minPitch = 0.92f;
        [SerializeField] private float maxPitch = 1.12f;

        [Header("Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float blockImpactVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float blockClatterVolume = 0.65f;
        [Range(0f, 1f)] [SerializeField] private float launchVolume = 0.80f;
        [Range(0f, 1f)] [SerializeField] private float wallBounceVolume = 0.60f;
        [Range(0f, 1f)] [SerializeField] private float victoryVolume = 1.0f;
        [SerializeField] private bool isMuted = false;

        public AudioClip[] BlockImpactClips { get => blockImpactClips; set => blockImpactClips = value; }
        public AudioClip[] BlockClatterClips { get => blockClatterClips; set => blockClatterClips = value; }
        public AudioClip LaunchClip { get => launchClip; set => launchClip = value; }
        public AudioClip WallBounceClip { get => wallBounceClip; set => wallBounceClip = value; }
        public AudioClip ObstacleDeflectClip { get => obstacleDeflectClip; set => obstacleDeflectClip = value; }
        public AudioClip VictoryClip { get => victoryClip; set => victoryClip = value; }
        public AudioSource SfxSource { get => sfxSource; set => sfxSource = value; }
        public AudioSource ClatterSource { get => clatterSource; set => clatterSource = value; }
        public AudioSource VictorySource { get => victorySource; set => victorySource = value; }

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

        public float BlockImpactVolume { get => blockImpactVolume; set => blockImpactVolume = Mathf.Clamp01(value); }
        public float BlockClatterVolume { get => blockClatterVolume; set => blockClatterVolume = Mathf.Clamp01(value); }
        public float LaunchVolume { get => launchVolume; set => launchVolume = Mathf.Clamp01(value); }
        public float WallBounceVolume { get => wallBounceVolume; set => wallBounceVolume = Mathf.Clamp01(value); }
        public float VictoryVolume { get => victoryVolume; set => victoryVolume = Mathf.Clamp01(value); }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this && Application.isPlaying)
            {
                Destroy(gameObject);
                return;
            }

            EnsureAudioListener();
            InitializeAudioSources();
            LoadAudioPreferences();
            AutoLoadClipsIfEmpty();
        }

        private void OnEnable()
        {
            if (instance == null) instance = this;
            EnsureAudioListener();
            InitializeAudioSources();
            AutoLoadClipsIfEmpty();
        }

        private void EnsureAudioListener()
        {
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                }
                else
                {
                    gameObject.AddComponent<AudioListener>();
                }
            }
        }

        private void InitializeAudioSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();

            if (sfxSource == null)
            {
                sfxSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
            }
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            if (clatterSource == null)
            {
                clatterSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
            }
            clatterSource.playOnAwake = false;
            clatterSource.spatialBlend = 0f;

            if (victorySource == null)
            {
                victorySource = sources.Length > 2 ? sources[2] : gameObject.AddComponent<AudioSource>();
            }
            victorySource.playOnAwake = false;
            victorySource.spatialBlend = 0f;
            victorySource.pitch = 1.0f;
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
            if (clatterSource != null) clatterSource.volume = effectiveVolume;
            if (victorySource != null) victorySource.volume = effectiveVolume;
        }

        private void AutoLoadClipsIfEmpty()
        {
            if (launchClip == null)
            {
                launchClip = Resources.Load<AudioClip>("Case4/AUC_Launch")
                          ?? Resources.Load<AudioClip>("AudioClips/AUC_Launch")
                          ?? Resources.Load<AudioClip>("AUC_Launch");
            }

            if (wallBounceClip == null)
            {
                wallBounceClip = Resources.Load<AudioClip>("Case4/AUC_Ricochet")
                              ?? Resources.Load<AudioClip>("AudioClips/AUC_Ricochet")
                              ?? Resources.Load<AudioClip>("AUC_Ricochet");
            }

            if (obstacleDeflectClip == null)
            {
                obstacleDeflectClip = Resources.Load<AudioClip>("Case4/AUC_ObstacleDeflect")
                                   ?? Resources.Load<AudioClip>("AudioClips/AUC_ObstacleDeflect")
                                   ?? Resources.Load<AudioClip>("AUC_ObstacleDeflect");
            }

            if (blockImpactClips == null || blockImpactClips.Length == 0 || blockImpactClips[0] == null)
            {
                blockImpactClips = new AudioClip[]
                {
                    Resources.Load<AudioClip>("Case4/AUC_BlocksDomino") ?? Resources.Load<AudioClip>("AUC_BlocksDomino"),
                    Resources.Load<AudioClip>("Case4/AUC_ObstacleDeflect") ?? Resources.Load<AudioClip>("AUC_ObstacleDeflect")
                };
            }

            if (blockClatterClips == null || blockClatterClips.Length == 0 || blockClatterClips[0] == null)
            {
                blockClatterClips = new AudioClip[]
                {
                    Resources.Load<AudioClip>("Case4/AUC_BlocksDomino") ?? Resources.Load<AudioClip>("AUC_BlocksDomino")
                };
            }

            if (victoryClip == null)
            {
                victoryClip = Resources.Load<AudioClip>("Case4/AUC_Victory")
                           ?? Resources.Load<AudioClip>("AudioClips/AUC_Victory")
                           ?? Resources.Load<AudioClip>("AUC_Victory");
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

        /// <summary>
        /// Plays heavy block impact sound scaled to velocity ratio with randomized pitch.
        /// </summary>
        public void PlayBlockImpactSound(float intensity = 1.0f)
        {
            if (blockImpactClips == null || blockImpactClips.Length == 0) return;
            AudioClip clip = blockImpactClips[UnityEngine.Random.Range(0, blockImpactClips.Length)];
            float scaledVolume = blockImpactVolume * Mathf.Clamp(intensity, 0.4f, 1.0f);
            PlayClipWithPitch(sfxSource, clip, scaledVolume, true, minPitch, maxPitch);
        }

        /// <summary>
        /// Plays block clatter / tumble sound with wide pitch variation.
        /// </summary>
        public void PlayBlockClatterSound(float volumeRatio = 1.0f)
        {
            if (blockClatterClips == null || blockClatterClips.Length == 0) return;
            AudioClip clip = blockClatterClips[UnityEngine.Random.Range(0, blockClatterClips.Length)];
            float scaledVolume = blockClatterVolume * Mathf.Clamp01(volumeRatio);
            PlayClipWithPitch(clatterSource, clip, scaledVolume, true, 0.85f, 1.25f);
        }

        /// <summary>
        /// Plays launch snap/whoosh sound on release.
        /// </summary>
        public void PlayLaunchSound(float powerRatio = 1.0f)
        {
            if (launchClip == null) return;
            float scaledVolume = launchVolume * Mathf.Clamp(powerRatio, 0.5f, 1.0f);
            PlayClipWithPitch(sfxSource, launchClip, scaledVolume, true, 0.95f, 1.05f);
        }

        /// <summary>
        /// Plays wall contact bounce sound (Ricochet).
        /// </summary>
        public void PlayWallBounceSound(float speedRatio = 1.0f)
        {
            if (wallBounceClip == null) return;
            float scaledVolume = wallBounceVolume * Mathf.Clamp(speedRatio, 0.3f, 1.0f);
            PlayClipWithPitch(sfxSource, wallBounceClip, scaledVolume, true, 0.90f, 1.15f);
        }

        /// <summary>
        /// Plays neon obstacle deflect / impact sound.
        /// </summary>
        public void PlayObstacleDeflectSound(float speedRatio = 1.0f)
        {
            AudioClip clip = obstacleDeflectClip != null ? obstacleDeflectClip : wallBounceClip;
            if (clip == null) return;
            float scaledVolume = wallBounceVolume * Mathf.Clamp(speedRatio, 0.4f, 1.0f);
            PlayClipWithPitch(sfxSource, clip, scaledVolume, true, 0.92f, 1.10f);
        }

        /// <summary>
        /// Plays victory fanfare sound on a dedicated audio channel with locked 1.0 pitch.
        /// </summary>
        public void PlayVictorySound()
        {
            if (victoryClip == null || isMuted || masterVolume <= 0.001f) return;
            if (victorySource == null) InitializeAudioSources();

            victorySource.pitch = 1.0f;
            victorySource.PlayOneShot(victoryClip, victoryVolume * masterVolume);
        }

        private void PlayClipWithPitch(AudioSource source, AudioClip clip, float volume, bool randomizePitch, float pitchMin, float pitchMax)
        {
            if (clip == null || isMuted || masterVolume <= 0.001f) return;

            if (source == null)
            {
                InitializeAudioSources();
                source = sfxSource;
            }

            if (randomizePitch)
            {
                source.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
            }
            else
            {
                source.pitch = 1.0f;
            }

            source.PlayOneShot(clip, volume * masterVolume);
        }
    }
}
