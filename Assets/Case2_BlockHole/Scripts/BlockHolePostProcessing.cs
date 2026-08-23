using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BlockHole
{
    [ExecuteAlways]
    public class BlockHolePostProcessing : MonoBehaviour
    {
        [Header("Anti-Aliasing (Kenar Yumuşatma)")]
        [SerializeField] private AntialiasingMode antialiasingMode = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        [SerializeField] private AntialiasingQuality antialiasingQuality = AntialiasingQuality.High;
        [SerializeField] private bool enableDithering = true;

        [Header("Soft Bloom Glow (Doğal Işıltı)")]
        [Tooltip("Balanced bloom intensity to prevent white color clipping.")]
        [SerializeField] private float bloomIntensity = 0.30f;
        [SerializeField] private float bloomThreshold = 1.02f;
        [SerializeField] private float bloomScatter = 0.60f;
        [SerializeField] private Color bloomTint = new Color(1.0f, 0.98f, 0.92f);

        [Header("Color Vibrancy & Contrast (Dengeli Canlılık)")]
        [SerializeField] private float postExposure = 0.12f;
        [SerializeField] private float contrast = 14.0f;
        [SerializeField] private float saturation = 26.0f;

        [Header("Vignette & Tonemapping")]
        [SerializeField] private float vignetteIntensity = 0.14f;
        [SerializeField] private float vignetteSmoothness = 0.45f;
        [SerializeField] private bool useAcesTonemapping = true;

        private Volume volume;
        private VolumeProfile profile;

        private void OnValidate()
        {
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

        public void SetupPostProcessing()
        {
            // 1. Ensure all cameras render Post Processing, SMAA, Dithering and HDR
            Camera[] allCameras = FindObjectsOfType<Camera>(true);
            foreach (Camera cam in allCameras)
            {
                if (cam == null) continue;

                cam.allowHDR = true;

                UniversalAdditionalCameraData addData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (addData == null)
                {
                    addData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }

                if (addData != null)
                {
                    addData.renderPostProcessing = true;
                    addData.antialiasing = antialiasingMode;
                    addData.antialiasingQuality = antialiasingQuality;
                    addData.dithering = enableDithering;
                    addData.volumeLayerMask = ~0;
                    addData.volumeTrigger = cam.transform;
                }
            }

            // 2. Setup Global Volume component
            gameObject.layer = 0;

            volume = GetComponent<Volume>();
            if (volume == null)
            {
                volume = gameObject.AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.weight = 1.0f;

            if (volume.profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Runtime_BlockHole_PPProfile";
                volume.profile = profile;
            }
            else
            {
                profile = volume.profile;
            }

            // 3. Bloom (Soft Saturated Aura - No White Burnout)
            if (!profile.TryGet<Bloom>(out var bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.active = true;
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(bloomThreshold);
            bloom.scatter.Override(bloomScatter);
            bloom.tint.Override(bloomTint);

            // 4. Color Adjustments (Vibrancy & Saturation)
            if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
            {
                colorAdj = profile.Add<ColorAdjustments>(true);
            }
            colorAdj.active = true;
            colorAdj.postExposure.Override(postExposure);
            colorAdj.contrast.Override(contrast);
            colorAdj.saturation.Override(saturation);

            // 5. Tonemapping (ACES Cinematic curve)
            if (useAcesTonemapping)
            {
                if (!profile.TryGet<Tonemapping>(out var tonemap))
                {
                    tonemap = profile.Add<Tonemapping>(true);
                }
                tonemap.active = true;
                tonemap.mode.Override(TonemappingMode.ACES);
            }

            // 6. Soft Framing Vignette
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
