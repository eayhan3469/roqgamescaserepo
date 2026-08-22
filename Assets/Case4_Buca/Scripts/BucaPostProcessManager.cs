using UnityEngine;
using UnityEngine.Rendering;

namespace Buca
{
    public class BucaPostProcessManager : MonoBehaviour
    {
        [Header("Volume Profile")]
        [SerializeField] private VolumeProfile volumeProfile;

        private Volume volume;

        private void Awake()
        {
            SetupVolume();
        }

        private void SetupVolume()
        {
            if (!TryGetComponent<Volume>(out volume))
            {
                volume = gameObject.AddComponent<Volume>();
            }

            if (volume != null)
            {
                volume.isGlobal = true;
                volume.priority = 1f;
                volume.weight = 1f;

                if (volumeProfile == null)
                {
                    volumeProfile = Resources.Load<VolumeProfile>("BucaVolumeProfile");
                }

                if (volumeProfile != null && volume.sharedProfile == null)
                {
                    volume.sharedProfile = volumeProfile;
                }
            }
        }
    }
}
