using UnityEngine;

namespace _CarMatchMania.Scripts.Contexts._GameplayContext.Models
{
    public class GridWall : MonoBehaviour
    {
        [SerializeField] private Transform _leftBottomPoint;
        [SerializeField] private Transform _rightBottomPoint;
        
        public Transform LeftBottomPoint => _leftBottomPoint;
        public Transform RightBottomPoint => _rightBottomPoint;

        public void PlaceWallRightToOtherWall(GridWall otherWall)
        {
            // otherwall's right bottom point should be at this wall's left bottom point
            var otherWallRightBottomPoint = otherWall.RightBottomPoint;
            var thisWallLeftBottomPoint = LeftBottomPoint;
            var offset = thisWallLeftBottomPoint.position - otherWallRightBottomPoint.position;
            transform.position += offset;
        }
        
        public void PlaceWallLeftToOtherWall(GridWall otherWall)
        {
            // otherwall's left bottom point should be at this wall's right bottom point
            var otherWallLeftBottomPoint = otherWall.LeftBottomPoint;
            var thisWallRightBottomPoint = RightBottomPoint;
            var offset = thisWallRightBottomPoint.position - otherWallLeftBottomPoint.position;
            transform.position -= offset;
        }
     }
}