using System;
using System.Collections.Generic;
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

        [Tooltip("Optional: a longer sustained sound for a block being absorbed/pulled into a hole (e.g. the jelly bonus scene's suction effect), running through the whole fall rather than a single impact instant. Left unassigned by default — not auto-loaded, no default clip shipped for it — assign one per-scene to opt in.")]
        [SerializeField] private AudioClip absorbClip;

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
        [Range(0f, 1f)] [SerializeField] private float absorbVolume = 0.8f;
        [SerializeField] private bool isMuted = false;

        public AudioClip[] PickupClips { get => pickupClips; set => pickupClips = value; }
        public AudioClip SlideClip { get => slideClip; set => slideClip = value; }
        public AudioClip SnapBackClip { get => snapBackClip; set => snapBackClip = value; }
        public AudioClip[] DropClips { get => dropClips; set => dropClips = value; }
        public AudioClip[] ShatterClips { get => shatterClips; set => shatterClips = value; }
        public AudioClip VictoryClip { get => victoryClip; set => victoryClip = value; }
        public AudioClip AbsorbClip { get => absorbClip; set => absorbClip = value; }
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
        public float AbsorbVolume { get => absorbVolume; set => absorbVolume = Mathf.Clamp01(value); }

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
            if (victoryClip == null) return;
            PlayClipWithPitch(victoryClip, 1.0f, false);
        }

        /// <summary>
        /// Longer sustained "being absorbed/pulled into a hole" sound (see absorbClip's
        /// tooltip) — no-ops if unassigned, so leaving it empty is always safe. Uses the same
        /// shared sfxSource as every other cue here; AudioSource.PlayOneShot layers additively
        /// rather than interrupting, so this can safely overlap with a shorter impact one-shot
        /// (e.g. PlayShatterSound) firing during the same fall.
        ///
        /// <paramref name="targetDurationSeconds"/>: when greater than 0, syncs the clip's
        /// loudest moment (found via FindPeakTime, not just its raw total length) to land
        /// exactly at this many seconds after playback starts, by computing a pitch multiplier
        /// = peakTime / targetDurationSeconds. This is deliberately NOT "stretch the whole clip
        /// to match the target duration" — a clip's audible climax rarely sits at its very last
        /// sample, so scaling by total length alone can land the climax well before or after
        /// the visual event it is meant to punctuate even when the *end* of the clip lines up.
        /// Syncing on the analyzed peak instead makes the loudest/most impactful moment of the
        /// sound coincide with the moment the caller cares about (e.g. the block actually
        /// disappearing at the bottom of the fall). Left at the default (0) to fall back to the
        /// normal random pitch variance every other cue in this class uses.
        /// </summary>
        public void PlayAbsorbSound(float targetDurationSeconds = 0f)
        {
            if (absorbClip == null || isMuted || masterVolume <= 0.001f) return;

            if (sfxSource == null)
            {
                sfxSource = GetComponent<AudioSource>();
                if (sfxSource == null)
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                }
            }

            if (targetDurationSeconds > 0.01f)
            {
                float peakTime = Mathf.Max(FindPeakTime(absorbClip), 0.05f);
                // Wide clamp — exact peak sync is the whole point of this path, so only guard
                // against genuinely degenerate cases (a peak at literally the first/last few
                // samples paired with a very different target duration), not against ordinary
                // pitch shifts a real clip's peak position can call for.
                sfxSource.pitch = Mathf.Clamp(peakTime / targetDurationSeconds, 0.35f, 2.5f);
            }
            else
            {
                sfxSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            }

            sfxSource.PlayOneShot(absorbClip, absorbVolume * masterVolume);
        }

        private static readonly Dictionary<AudioClip, float> peakTimeCache = new Dictionary<AudioClip, float>();

        /// <summary>
        /// Analyzes an AudioClip's decoded PCM data (via AudioClip.GetData, so the clip must be
        /// DecompressOnLoad/PCM-friendly — true of every SFX in this project after the Vorbis-
        /// import lessons elsewhere in this branch) to find the timestamp, in seconds from the
        /// start, of its loudest moment — computed as the center of the highest-RMS ~50ms
        /// window, not a single instantaneous sample peak, since a lone spike is not usually
        /// what a listener perceives as "the peak" of a sound. Cached per-clip (static
        /// dictionary keyed by the clip reference) since the result never changes for a given
        /// asset and re-scanning every play call would be wasteful. Falls back to the clip's
        /// midpoint if analysis fails for any reason (unreadable data, zero-length clip, etc.)
        /// rather than throwing.
        /// </summary>
        private static float FindPeakTime(AudioClip clip)
        {
            if (clip == null) return 0f;
            if (peakTimeCache.TryGetValue(clip, out float cached)) return cached;

            float peakTime = clip.length * 0.5f;
            try
            {
                int channels = Mathf.Max(clip.channels, 1);
                int totalSamples = clip.samples * channels;
                if (totalSamples > 0 && clip.frequency > 0)
                {
                    float[] data = new float[totalSamples];
                    if (clip.GetData(data, 0))
                    {
                        int windowSize = Mathf.Max(1, Mathf.RoundToInt(clip.frequency * 0.05f)) * channels;
                        float bestRms = -1f;
                        int bestWindowStart = 0;
                        for (int i = 0; i + windowSize <= totalSamples; i += windowSize)
                        {
                            double sumSq = 0;
                            for (int j = 0; j < windowSize; j++)
                            {
                                float s = data[i + j];
                                sumSq += (double)s * s;
                            }
                            float rms = (float)Math.Sqrt(sumSq / windowSize);
                            if (rms > bestRms)
                            {
                                bestRms = rms;
                                bestWindowStart = i;
                            }
                        }
                        int centerSampleIndex = bestWindowStart + windowSize / 2;
                        int frameIndex = centerSampleIndex / channels;
                        peakTime = (float)frameIndex / clip.frequency;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"BlockHoleAudioManager: peak-time analysis failed for '{clip.name}', falling back to clip midpoint. {e.Message}");
                peakTime = clip.length * 0.5f;
            }

            peakTimeCache[clip] = peakTime;
            return peakTime;
        }

        public void PlayTilePopSound(float pitch = 1.0f)
        {
            if (pickupClips != null && pickupClips.Length > 0)
            {
                AudioClip clip = pickupClips[0];
                if (sfxSource != null)
                {
                    sfxSource.pitch = pitch;
                    sfxSource.PlayOneShot(clip, pickupVolume * masterVolume);
                }
            }
            else if (snapBackClip != null)
            {
                if (sfxSource != null)
                {
                    sfxSource.pitch = pitch;
                    sfxSource.PlayOneShot(snapBackClip, snapBackVolume * masterVolume);
                }
            }
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
