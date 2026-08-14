using _HoleBlock.Scripts.Contexts.Gameplay.Controllers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _HoleBlockGame.Scripts.Contexts.Gameplay.Contexts.BlockContext.Helpers
{
    public class BlockFractureParentUtilities : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)] private float _scaleFactor = 0.55f;
        [SerializeField] private LayerMask _layerMask;

        [Button]
        public void SetExcludeLayerMask()
        {
            var colliders = GetComponentsInChildren<MeshCollider>(true);
            foreach (var meshCollider in colliders)
            {
                meshCollider.excludeLayers = _layerMask;
            }
        }
        
        [Button]
        public void RemoveAllColliders()
        {
            MeshColliderSmaller.RemoveAllColliders(transform);
        }

        [Button]
        public void Small()
        {
            MeshColliderSmaller.Smaller(_scaleFactor, transform);
        }
    }
}