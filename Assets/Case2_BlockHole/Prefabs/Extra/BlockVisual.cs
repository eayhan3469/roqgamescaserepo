using System.Collections.Generic;
using _Efsun.Scripts.EfsunUI.Models;
using RayFire;
using TMPro;
using UnityEngine;

namespace _HoleBlock.Scripts.Contexts.Gameplay.Contexts.MovableBlockContext.Models
{
    public class BlockVisual : MonoBehaviour
    {
        [SerializeField] private Transform _centerTransform;
        [SerializeField] private Transform _onBlockCenterTransform;
        
        [SerializeField] private MeshRenderer _blockMeshRenderer;
        
        [SerializeField] private List<ParticleSystem> _iceElements;
        [SerializeField] private TMP_Text _iceBreakText;

        [SerializeField] private GameObject _chain; 
        [SerializeField] private MeshRenderer _chainMeshRenderer;
        
        public Transform CenterTransform => _centerTransform;
        public Transform OnBlockCenterTransform => _onBlockCenterTransform;
        public MeshRenderer BlockMeshRenderer => _blockMeshRenderer;
        public List<ParticleSystem> IceElements => _iceElements; 
        public TMP_Text IceBreakText => _iceBreakText;
        public MeshRenderer ChainMeshRenderer => _chainMeshRenderer;
        public GameObject Chain => _chain;
        

        public void SetIceBreakText(TMP_Text text)
        {
            _iceBreakText = text;
        }
    }
}