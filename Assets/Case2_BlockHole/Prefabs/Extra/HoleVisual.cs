using UnityEngine;

namespace _HoleBlock.Scripts.Contexts.Gameplay.Contexts.HoleContext.Models
{
    public class HoleVisual : MonoBehaviour
    {
        [SerializeField] private Transform _centerTransform;
        [SerializeField] private GameObject _hoverElementsParent;
        
        public Transform CenterTransform => _centerTransform;
        public GameObject HoverElementsParent => _hoverElementsParent;
    }
}