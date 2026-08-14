using System.Collections.Generic;
using UnityEngine;

namespace _CarMatchMania.Scripts.Contexts._GameplayContext.Models
{
    public class MapCellEdge : MonoBehaviour
    {
        
        [System.Serializable]
        public class DirectionAndVisualPair 
        {
            [SerializeField] private Direction _direction;
            [SerializeField] private List<GameObject> _visual;
            
            public Direction Direction => _direction;
            public List<GameObject> Visual => _visual;
        }
        
        [SerializeField] private Transform _root;
        [SerializeField] private Transform _start;
        [SerializeField] private Transform _end;
        [SerializeField] private Transform _middleLeft;
        [SerializeField] private Transform _middleRight;
        [SerializeField] private Transform _leftPartLeft;
        [SerializeField] private Transform _leftPartRight;
        [SerializeField] private Transform _leftPart;
        [SerializeField] private Transform _rightPartLeft;
        [SerializeField] private Transform _rightPartRight;
        [SerializeField] private Transform _rightPart;
        [SerializeField] private DirectionAndVisualPair[] _directionVisualPairs;
        
        public Transform Root => _root;
        public Transform Start => _start;
        public Transform End => _end;
        public Transform MiddleLeft => _middleLeft;
        public Transform MiddleRight => _middleRight;
        public Transform LeftPartLeft => _leftPartLeft;
        public Transform LeftPartRight => _leftPartRight;
        public Transform RightPartLeft => _rightPartLeft;
        public Transform RightPartRight => _rightPartRight;
        public Transform LeftPart => _leftPart;
        public Transform RightPart => _rightPart;

        public void ArrangeEdges(Vector3 start, Vector3 end)
        {
            var leftPartLenght = Vector3.Distance(LeftPartLeft.position, LeftPartRight.position);
            var rightPartLenght = Vector3.Distance(RightPartLeft.position, RightPartRight.position);
            
            var leftTargetLenght = Vector3.Distance(start, MiddleLeft.position);
            var rightTargetLenght = Vector3.Distance(end, MiddleRight.position);
            
            LeftPart.localScale = new Vector3(
                leftTargetLenght / leftPartLenght * LeftPart.localScale.x,
                LeftPart.localScale.y,
                LeftPart.localScale.z
            );
            
            RightPart.localScale = new Vector3(
                rightTargetLenght / rightPartLenght * RightPart.localScale.x,
                RightPart.localScale.y,
                RightPart.localScale.z
            );
        }

        public void ApplyDirection(Direction direction)
        {
            float rotationZ = direction switch
            {
                Direction.Up => -90f,
                Direction.Right => -0f,
                Direction.Down => -270f,
                Direction.Left => -180f,
                _ => Root.eulerAngles.z
            };
            
            Root.eulerAngles = new Vector3(
                Root.eulerAngles.x,
                rotationZ,
                Root.eulerAngles.y
            );
            
            foreach (var pair in _directionVisualPairs)
            {
                bool isActive = pair.Direction == direction;
                pair.Visual.ForEach(v => v.gameObject.SetActive(isActive));
            }
        }
    }
}