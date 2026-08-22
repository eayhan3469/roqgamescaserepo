using System;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Unlocks 60 / 90 / 120 FPS on Android APK and mobile builds, prevents screen sleep, and optimizes runtime performance.
    /// </summary>
    public static class MobilePerformanceOptimizer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void OptimizeBeforeSplashScreen()
        {
            ApplyTargetFrameRate();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OptimizeBeforeSceneLoad()
        {
            ApplyTargetFrameRate();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Time.fixedDeltaTime = 1.0f / 60.0f;
        }

        public static void ApplyTargetFrameRate()
        {
            // On mobile, vSyncCount must be 0 for Application.targetFrameRate to take effect smoothly
            QualitySettings.vSyncCount = 0;

            int targetRate = 60;

            try
            {
                #if UNITY_2022_1_OR_NEWER
                double refreshRate = Screen.currentResolution.refreshRateRatio.value;
                if (refreshRate >= 115.0) targetRate = 120;
                else if (refreshRate >= 85.0) targetRate = 90;
                else targetRate = 60;
                #else
                int refreshRate = Screen.currentResolution.refreshRate;
                if (refreshRate >= 115) targetRate = 120;
                else if (refreshRate >= 85) targetRate = 90;
                else targetRate = 60;
                #endif
            }
            catch
            {
                targetRate = 60;
            }

            Application.targetFrameRate = targetRate;
        }
    }
}
