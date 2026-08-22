using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FitTheShape
{
    [ExecuteAlways]
    public class FitTheShapePostProcessing : MonoBehaviour
    {
        private static FitTheShapePostProcessing instance;
        public static FitTheShapePostProcessing Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<FitTheShapePostProcessing>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("[Global Volume]");
                        instance = go.AddComponent<FitTheShapePostProcessing>();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Color Vibrancy & Contrast (Candy Pop)")]
        [Tooltip("Post exposure boost to brighten the overall toy stage.")]
        [SerializeField] private float postExposure = 0.12f;

        [Tooltip("Contrast to make shape colors distinct from the drum.")]
        [SerializeField] private float contrast = 14.0f;

        [Tooltip("Saturation boost to give vibrant, juicy candy colors.")]
        [SerializeField] private float saturation = 28.0f;

        [Header("Soft Bloom Glow")]
        [Tooltip("Subtle warm bloom for golden stars and highlights.")]
        [SerializeField] private float bloomIntensity = 0.55f;
        [SerializeField] private float bloomThreshold = 0.95f;
        [SerializeField] private float bloomScatter = 0.70f;

        [Header("Vignette & Tonemapping")]
        [SerializeField] private float vignetteIntensity = 0.18f;
        [SerializeField] private float vignetteSmoothness = 0.45f;
        [SerializeField] private bool useAcesTonemapping = true;

        private Volume volume;
        private VolumeProfile profile;

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

            SetupPostProcessing();
        }

        private void OnEnable()
        {
            SetupPostProcessing();
        }

        private void Start()
        {
            SetupPostProcessing();
        }

        private void OnValidate()
        {
            SetupPostProcessing();
        }

        public void SetupPostProcessing()
        {
            // 1. Ensure Camera has Post Processing and HDR enabled
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.allowHDR = true;

                UniversalAdditionalCameraData addData = mainCam.GetComponent<UniversalAdditionalCameraData>();
                if (addData == null)
                {
                    addData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }

                if (addData != null)
                {
                    addData.renderPostProcessing = true;
                    addData.volumeLayerMask = ~0;
                    addData.volumeTrigger = mainCam.transform;
                }
            }

            // 2. Setup Global Volume component
            gameObject.layer = 0;

            if (volume == null)
            {
                volume = GetComponent<Volume>();
                if (volume == null)
                {
                    volume = gameObject.AddComponent<Volume>();
                }
            }

            volume.isGlobal = true;
            volume.priority = 1.0f;
            volume.weight = 1.0f;

            if (volume.profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Runtime_FitTheShape_Profile";
                volume.profile = profile;
            }
            else
            {
                profile = volume.profile;
            }

            // 3. Bloom Component
            if (!profile.TryGet<Bloom>(out var bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.active = true;
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(bloomThreshold);
            bloom.scatter.Override(bloomScatter);

            // 4. Color Adjustments (Vibrancy, Contrast & Exposure)
            if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
            {
                colorAdj = profile.Add<ColorAdjustments>(true);
            }
            colorAdj.active = true;
            colorAdj.postExposure.Override(postExposure);
            colorAdj.contrast.Override(contrast);
            colorAdj.saturation.Override(saturation);

            // 5. ACES Tonemapping
            if (useAcesTonemapping)
            {
                if (!profile.TryGet<Tonemapping>(out var tonemap))
                {
                    tonemap = profile.Add<Tonemapping>(true);
                }
                tonemap.active = true;
                tonemap.mode.Override(TonemappingMode.ACES);
            }

            // 6. Framing Vignette
            if (!profile.TryGet<Vignette>(out var vig))
            {
                vig = profile.Add<Vignette>(true);
            }
            vig.active = true;
            vig.intensity.Override(vignetteIntensity);
            vig.smoothness.Override(vignetteSmoothness);
        }
    }
}
