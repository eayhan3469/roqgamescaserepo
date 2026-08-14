using UnityEngine;

namespace _HoleBlock.Scripts.Contexts.Gameplay.Contexts.MovableBlockContext.Models
{
    public class BlockVisualFracture : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _meshRenderer;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private MeshCollider _meshCollider;
        
        public MeshRenderer MeshRenderer => _meshRenderer;
        public Rigidbody Rigidbody => _rigidbody;
        public MeshCollider MeshCollider => _meshCollider;

        public void TryFetchElements()
        {
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
            _meshCollider = GetComponentInChildren<MeshCollider>();
            _rigidbody = GetComponentInChildren<Rigidbody>();
        }
    }
}