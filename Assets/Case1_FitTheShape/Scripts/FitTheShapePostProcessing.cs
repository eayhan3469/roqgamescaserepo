using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

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

        [Header("Anti-Aliasing (Kenar Yumuşatma & Pürüzsüzleştirme)")]
        [Tooltip("Subpixel Morphological Anti-Aliasing (SMAA) removes all jagged edge artifacts.")]
        [SerializeField] private AntialiasingMode antialiasingMode = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        [SerializeField] private AntialiasingQuality antialiasingQuality = AntialiasingQuality.High;
        [SerializeField] private bool enableDithering = true;

        [Header("Color Vibrancy & Contrast (Candy Pop)")]
        [Tooltip("Post exposure boost to brighten the overall toy stage.")]
        [SerializeField] private float postExposure = 0.12f;

        [Tooltip("Contrast to make shape colors distinct from the drum.")]
        [SerializeField] private float contrast = 14.0f;

        [Tooltip("Saturation boost to give vibrant, juicy candy colors.")]
        [SerializeField] private float saturation = 26.0f;

        [Header("Soft Subtle Bloom Glow")]
        [Tooltip("Subtle warm bloom for golden stars and highlights without washing out.")]
        [SerializeField] private float bloomIntensity = 0.25f;
        [SerializeField] private float bloomThreshold = 1.05f;
        [SerializeField] private float bloomScatter = 0.55f;
        [SerializeField] private Color bloomTint = new Color(1.0f, 0.98f, 0.92f);

        [Header("Vignette & Tonemapping")]
        [SerializeField] private float vignetteIntensity = 0.14f;
        [SerializeField] private float vignetteSmoothness = 0.45f;
        [SerializeField] private bool useAcesTonemapping = true;

        [Header("Tilt-Shift / Depth of Field (Minyatür Oyuncak Odak)")]
        [Tooltip("Enable subtle background blur for a charming diorama / toy-box look.")]
        [SerializeField] private bool enableDepthOfField = true;
        [SerializeField] private DepthOfFieldMode dofMode = DepthOfFieldMode.Gaussian;
        [Tooltip("Distance where the sharp focus region begins.")]
        [SerializeField] private float dofGaussianStart = 95.0f;
        [Tooltip("Distance where background Gaussian blur reaches full strength.")]
        [SerializeField] private float dofGaussianEnd = 112.0f;
        [Tooltip("Max blur radius for Gaussian Depth of Field.")]
        [SerializeField] private float dofGaussianMaxRadius = 1.3f;

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
                    addData.antialiasing = antialiasingMode;
                    addData.antialiasingQuality = antialiasingQuality;
                    addData.dithering = enableDithering;
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
            bloom.tint.Override(bloomTint);

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

            // 7. Tilt-Shift / Depth of Field (Minyatür Oyuncak Odak)
            if (enableDepthOfField)
            {
                if (!profile.TryGet<DepthOfField>(out var dof))
                {
                    dof = profile.Add<DepthOfField>(true);
                }
                dof.active = true;
                dof.mode.Override(dofMode);
                dof.gaussianStart.Override(dofGaussianStart);
                dof.gaussianEnd.Override(dofGaussianEnd);
                dof.gaussianMaxRadius.Override(dofGaussianMaxRadius);
            }
            else
            {
                if (profile.TryGet<DepthOfField>(out var dof))
                {
                    dof.active = false;
                }
            }
        }
    }
}
