using UnityEngine;
using UnityEngine.Rendering;

namespace Buca
{
    [ExecuteAlways]
    public class BucaPostProcessManager : MonoBehaviour
    {
        [Header("Volume Profile")]
        [SerializeField] private VolumeProfile volumeProfile;

        private Volume volume;

        private void Awake()
        {
            SetupVolume();
        }

        private void OnEnable()
        {
            SetupVolume();
        }

        private void SetupVolume()
        {
            volume = GetComponent<Volume>();
            if (volume == null)
            {
                volume = gameObject.AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.priority = 1f;
            volume.weight = 1f;

            if (volumeProfile == null)
            {
                volumeProfile = Resources.Load<VolumeProfile>("BucaVolumeProfile");
            }

            if (volume.sharedProfile == null && volumeProfile != null)
            {
                volume.sharedProfile = volumeProfile;
            }
        }
    }
}
