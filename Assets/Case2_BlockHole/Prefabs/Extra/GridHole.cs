using _HoleBlockGame.Scripts.Contexts.Gameplay.Contexts.BlockContext.Settings;
using UnityEngine;

namespace _HoleBlock.Scripts.Contexts.Gameplay.Contexts.HoleContext.Models
{
    public class GridHole : MonoBehaviour
    {
        [SerializeField] private Transform _holeMainTransform;
        
        public Transform HoleMainTransform => _holeMainTransform;
    }
}