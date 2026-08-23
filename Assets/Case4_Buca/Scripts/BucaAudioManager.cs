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

        [Tooltip("Played when the disc scrapes or slides smoothly along a side wall.")]
        [SerializeField] private AudioClip wallSlideClip;

        [Tooltip("Played when the disc hits a green neon obstacle.")]
        [SerializeField] private AudioClip obstacleDeflectClip;

        [Tooltip("Played when each block dissolves/pops away.")]
        [SerializeField] private AudioClip blockPopClip;

        [Tooltip("Played when the level target is cleared.")]
        [SerializeField] private AudioClip victoryClip;

        [Header("Audio Source & Pitch Settings")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource bounceSource;
        [SerializeField] private AudioSource clatterSource;
        [SerializeField] private AudioSource warpSource;
        [SerializeField] private AudioSource victorySource;
        [SerializeField] private AudioSource slideSource;
        [SerializeField] private float minPitch = 1.0f;
        [SerializeField] private float maxPitch = 1.0f;

        [Header("Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float blockImpactVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float blockClatterVolume = 0.65f;
        [Range(0f, 1f)] [SerializeField] private float launchVolume = 0.80f;
        [Range(0f, 1f)] [SerializeField] private float wallBounceVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float wallSlideVolume = 0.70f;
        [Range(0f, 1f)] [SerializeField] private float victoryVolume = 1.0f;
        [SerializeField] private bool isMuted = false;

        public AudioClip[] BlockImpactClips { get => blockImpactClips; set => blockImpactClips = value; }
        public AudioClip[] BlockClatterClips { get => blockClatterClips; set => blockClatterClips = value; }
        public AudioClip LaunchClip { get => launchClip; set => launchClip = value; }
        public AudioClip WallBounceClip { get => wallBounceClip; set => wallBounceClip = value; }
        public AudioClip WallSlideClip { get => wallSlideClip; set => wallSlideClip = value; }
        public AudioClip ObstacleDeflectClip { get => obstacleDeflectClip; set => obstacleDeflectClip = value; }
        public AudioClip BlockPopClip { get => blockPopClip; set => blockPopClip = value; }
        public AudioClip VictoryClip { get => victoryClip; set => victoryClip = value; }
        public AudioSource SfxSource { get => sfxSource; set => sfxSource = value; }
        public AudioSource BounceSource { get => bounceSource; set => bounceSource = value; }
        public AudioSource ClatterSource { get => clatterSource; set => clatterSource = value; }
        public AudioSource WarpSource { get => warpSource; set => warpSource = value; }
        public AudioSource VictorySource { get => victorySource; set => victorySource = value; }
        public AudioSource SlideSource { get => slideSource; set => slideSource = value; }

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

        public float WallBounceVolume
        {
            get => wallBounceVolume;
            set => wallBounceVolume = Mathf.Clamp01(value);
        }

        public float WallSlideVolume
        {
            get => wallSlideVolume;
            set => wallSlideVolume = Mathf.Clamp01(value);
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
        public float VictoryVolume { get => victoryVolume; set => victoryVolume = Mathf.Clamp01(value); }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Enforce ultra-low latency hardware audio buffer
            try
            {
                var config = AudioSettings.GetConfiguration();
                if (config.dspBufferSize > 256)
                {
                    config.dspBufferSize = 256;
                    AudioSettings.Reset(config);
                }
            }
            catch { }

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
            sfxSource.dopplerLevel = 0f;
            sfxSource.pitch = 1.0f;

            if (bounceSource == null)
            {
                bounceSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
            }
            bounceSource.playOnAwake = false;
            bounceSource.spatialBlend = 0f;
            bounceSource.dopplerLevel = 0f;
            bounceSource.pitch = 1.0f;

            if (clatterSource == null)
            {
                clatterSource = sources.Length > 2 ? sources[2] : gameObject.AddComponent<AudioSource>();
            }
            clatterSource.playOnAwake = false;
            clatterSource.spatialBlend = 0f;
            clatterSource.dopplerLevel = 0f;
            clatterSource.pitch = 1.0f;

            if (warpSource == null)
            {
                warpSource = sources.Length > 3 ? sources[3] : gameObject.AddComponent<AudioSource>();
            }
            warpSource.playOnAwake = false;
            warpSource.spatialBlend = 0f;
            warpSource.dopplerLevel = 0f;
            warpSource.pitch = 1.0f;

            if (victorySource == null)
            {
                victorySource = sources.Length > 4 ? sources[4] : gameObject.AddComponent<AudioSource>();
            }
            victorySource.playOnAwake = false;
            victorySource.spatialBlend = 0f;
            victorySource.dopplerLevel = 0f;
            victorySource.pitch = 1.0f;

            if (slideSource == null)
            {
                slideSource = sources.Length > 5 ? sources[5] : gameObject.AddComponent<AudioSource>();
            }
            slideSource.playOnAwake = false;
            slideSource.spatialBlend = 0f;
            slideSource.dopplerLevel = 0f;
            slideSource.loop = true;
            slideSource.pitch = 1.0f;
            if (wallSlideClip != null) slideSource.clip = wallSlideClip;
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
            if (bounceSource != null) bounceSource.volume = effectiveVolume;
            if (clatterSource != null) clatterSource.volume = effectiveVolume;
            if (warpSource != null) warpSource.volume = effectiveVolume;
            if (victorySource != null) victorySource.volume = effectiveVolume;
            if (slideSource != null && !slideSource.isPlaying) slideSource.volume = 0f;
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

            if (wallSlideClip == null)
            {
                wallSlideClip = Resources.Load<AudioClip>("Case4/AUC_Slide")
                             ?? Resources.Load<AudioClip>("AudioClips/AUC_Slide")
                             ?? Resources.Load<AudioClip>("AUC_Slide");
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
                    Resources.Load<AudioClip>("Case4/AUC_Ricochet") ?? Resources.Load<AudioClip>("AUC_Ricochet"),
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

            if (blockPopClip == null)
            {
                blockPopClip = Resources.Load<AudioClip>("Case3/AUC_Stamp")
                            ?? Resources.Load<AudioClip>("AudioClips/AUC_Stamp")
                            ?? Resources.Load<AudioClip>("Case2/AUC_DropSwallowed_01")
                            ?? Resources.Load<AudioClip>("Case4/AUC_ObstacleDeflect")
                            ?? obstacleDeflectClip;
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
        /// Plays heavy block impact sound scaled to velocity ratio.
        /// </summary>
        public void PlayBlockImpactSound(float intensity = 1.0f)
        {
            if (blockImpactClips == null || blockImpactClips.Length == 0) return;
            AudioClip clip = blockImpactClips[UnityEngine.Random.Range(0, blockImpactClips.Length)];
            float scaledVolume = blockImpactVolume * Mathf.Clamp(intensity, 0.4f, 1.0f);
            PlayClipWithPitch(clatterSource, clip, scaledVolume, false, 1.0f, 1.0f);
        }

        /// <summary>
        /// Plays block clatter / tumble sound.
        /// </summary>
        public void PlayBlockClatterSound(float volumeRatio = 1.0f)
        {
            if (blockClatterClips == null || blockClatterClips.Length == 0) return;
            AudioClip clip = blockClatterClips[UnityEngine.Random.Range(0, blockClatterClips.Length)];
            float scaledVolume = blockClatterVolume * Mathf.Clamp01(volumeRatio);
            PlayClipWithPitch(clatterSource, clip, scaledVolume, false, 1.0f, 1.0f);
        }

        /// <summary>
        /// Plays launch snap/whoosh sound on release.
        /// </summary>
        public void PlayLaunchSound(float powerRatio = 1.0f)
        {
            if (launchClip == null) return;
            float scaledVolume = launchVolume * Mathf.Clamp(powerRatio, 0.5f, 1.0f);
            PlayClipWithPitch(sfxSource, launchClip, scaledVolume, false, 1.0f, 1.0f);
        }

        /// <summary>
        /// Plays wall contact bounce sound (Ricochet).
        /// </summary>
        public void PlayWallBounceSound(float speedRatio = 1.0f)
        {
            if (wallBounceClip == null) return;
            float scaledVolume = wallBounceVolume * Mathf.Clamp(speedRatio, 0.50f, 1.0f);
            PlayClipWithPitch(bounceSource != null ? bounceSource : sfxSource, wallBounceClip, scaledVolume, false, 1.0f, 1.0f);
        }

        private float targetSlideVolume = 0f;

        /// <summary>
        /// Controls smooth continuous wall scraping/friction sound while disc is sliding along walls.
        /// </summary>
        public void SetWallSliding(bool isSliding, float speedRatio = 1.0f)
        {
            if (isMuted || masterVolume <= 0.001f || wallSlideClip == null)
            {
                targetSlideVolume = 0f;
                return;
            }

            if (isSliding && speedRatio > 0.05f)
            {
                if (slideSource == null) InitializeAudioSources();
                if (slideSource.clip == null) slideSource.clip = wallSlideClip;

                if (!slideSource.isPlaying)
                {
                    slideSource.volume = 0.01f;
                    slideSource.pitch = 1.0f;
                    slideSource.Play();
                }

                targetSlideVolume = wallSlideVolume * masterVolume * Mathf.Clamp(speedRatio, 0.25f, 1.0f);
            }
            else
            {
                targetSlideVolume = 0f;
            }
        }

        /// <summary>
        /// Plays a single one-shot glancing wall slide scrape sound.
        /// </summary>
        public void PlayWallSlideOneShot(float speedRatio = 1.0f)
        {
            if (wallSlideClip == null) return;
            float scaledVolume = wallSlideVolume * Mathf.Clamp(speedRatio, 0.3f, 1.0f);
            PlayClipWithPitch(sfxSource, wallSlideClip, scaledVolume, false, 1.0f, 1.0f);
        }

        private void Update()
        {
            if (slideSource != null && slideSource.isPlaying)
            {
                // Fast and smooth volume tracking with rock-solid pitch
                float fadeSpeed = targetSlideVolume > slideSource.volume ? 12.0f : 20.0f;
                slideSource.volume = Mathf.MoveTowards(slideSource.volume, targetSlideVolume, Time.deltaTime * fadeSpeed);
                slideSource.pitch = 1.0f;

                if (slideSource.volume <= 0.005f && targetSlideVolume <= 0.005f)
                {
                    slideSource.Stop();
                    slideSource.volume = 0f;
                }
            }
        }

        /// <summary>
        /// Plays neon obstacle deflect / impact sound.
        /// </summary>
        public void PlayObstacleDeflectSound(float speedRatio = 1.0f)
        {
            AudioClip clip = obstacleDeflectClip != null ? obstacleDeflectClip : wallBounceClip;
            if (clip == null) return;
            float scaledVolume = wallBounceVolume * Mathf.Clamp(speedRatio, 0.4f, 1.0f);
            PlayClipWithPitch(sfxSource, clip, scaledVolume, false, 1.0f, 1.0f);
        }

        /// <summary>
        /// Plays a fast sci-fi pop / warp sound when a block teleports away.
        /// </summary>
        public void PlayBlockWarpSound()
        {
            AudioClip clip = blockPopClip != null ? blockPopClip : (obstacleDeflectClip != null ? obstacleDeflectClip : wallBounceClip);
            if (clip == null) return;
            PlayClipWithPitch(warpSource != null ? warpSource : clatterSource, clip, 0.50f, false, 1.0f, 1.0f);
        }

        /// <summary>
        /// Plays a cute bubble/sparkle pop sound when a block dissolves.
        /// </summary>
        public void PlayBlockPopSound()
        {
            PlayBlockWarpSound();
        }

        /// <summary>
        /// Plays victory fanfare sound on a dedicated audio channel with locked 1.0 pitch.
        /// </summary>
        public void PlayVictorySound()
        {
            if (victoryClip == null || isMuted || masterVolume <= 0.001f) return;
            if (victorySource == null) InitializeAudioSources();

            victorySource.pitch = 1.0f;
            victorySource.ignoreListenerPause = true;
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
