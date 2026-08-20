using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BlockHole
{
    [ExecuteAlways]
    public class BlockHolePostProcessing : MonoBehaviour
    {
        [Header("Bloom Juice")]
        [Tooltip("Balanced bloom intensity to prevent white color clipping.")]
        [SerializeField] private float bloomIntensity = 0.65f;
        [SerializeField] private float bloomThreshold = 0.96f;
        [SerializeField] private float bloomScatter = 0.75f;

        [Header("Color Vibrancy & Contrast")]
        [SerializeField] private float postExposure = 0.15f;
        [SerializeField] private float contrast = 16f;
        [SerializeField] private float saturation = 30f;

        [Header("Vignette & Tonemapping")]
        [SerializeField] private float vignetteIntensity = 0.20f;
        [SerializeField] private bool useAcesTonemapping = true;

        private Volume volume;
        private VolumeProfile profile;

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
            // 1. Ensure all cameras render Post Processing and HDR
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
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(bloomThreshold);
            bloom.scatter.Override(bloomScatter);

            // 4. Color Adjustments (Vibrancy & Saturation)
            if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
            {
                colorAdj = profile.Add<ColorAdjustments>(true);
            }
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
                tonemap.mode.Override(TonemappingMode.ACES);
            }

            // 6. Soft Framing Vignette
            if (!profile.TryGet<Vignette>(out var vig))
            {
                vig = profile.Add<Vignette>(true);
            }
            vig.intensity.Override(vignetteIntensity);
            vig.smoothness.Override(0.45f);
        }
    }
}
