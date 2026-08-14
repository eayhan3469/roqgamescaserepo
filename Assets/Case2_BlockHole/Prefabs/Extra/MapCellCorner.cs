using System.Collections.Generic;
using UnityEngine;

namespace _CarMatchMania.Scripts.Contexts._GameplayContext.Models
{
    public class MapCellCorner : MonoBehaviour
    {
        [System.Serializable]
        private class RotationAndData
        {
            [SerializeField] private MapCellRotationType _rotationType;
            [SerializeField] private MapCellCornerType _cornerType;
            [SerializeField] private GameObject _rotationVisual;
            [SerializeField] private Transform _startPoint;
            [SerializeField] private Transform _startPointSecond;
            [SerializeField] private Transform _endPoint;
            [SerializeField] private Transform _endPointSecond;
            
            public MapCellRotationType RotationType => _rotationType;
            public MapCellCornerType CornerType => _cornerType;
            public GameObject RotationVisual => _rotationVisual;
            public Transform StartPoint => _startPoint;     
            public Transform EndPoint => _endPoint;
            public Transform StartPointSecond => _startPointSecond;
            public Transform EndPointSecond => _endPointSecond;
        }
        
        [SerializeField] private Transform _root;
        [SerializeField] private Transform _sitPoint;
        [SerializeField] private Transform _innerCornerSitPoint;
        
        [SerializeField] private Transform _leftPointForScaling;
        [SerializeField] private Transform _rightPointForScaling;
        [SerializeField] private List<RotationAndData> _rotationAndData;

        public Transform StartTransform { get; private set; }

        public Transform EndTransform { get; private set; }

        private Transform _startMidpoint;
        private Transform _endMidpoint;
        
        public Transform StartMidpoint => _startMidpoint;
        public Transform EndMidpoint => _endMidpoint;

        public Transform Root => _root;
        public Transform SitPoint => _sitPoint;
        public Transform InnerCornerSitPoint => _innerCornerSitPoint;
        public Transform LeftPointForScaling => _leftPointForScaling;
        public Transform RightPointForScaling => _rightPointForScaling;
         
        public MapCellRotationType CurrentRotationType;
        public MapCellCornerType CurrentCornerType;
        
        
        public void ApplyRotation(MapCellRotationType rotationType, MapCellCornerType cornerType)
        {
            CurrentRotationType = rotationType;
            CurrentCornerType = cornerType;
            if (_startMidpoint != null) Destroy(_startMidpoint.gameObject);
            if (_endMidpoint != null) Destroy(_endMidpoint.gameObject);
            
            var degree = rotationType.ToDegrees();
            
            foreach (var rotationAndData in _rotationAndData) 
            {
                var isActive = rotationAndData.CornerType == cornerType; 
                rotationAndData.RotationVisual.SetActive(isActive);
                if (!isActive)
                {
                    continue;
                }
                var newRotation = Quaternion.Euler(0f, degree + gameObject.transform.rotation.eulerAngles.y, 0f);
                gameObject.transform.rotation = newRotation;
                
                StartTransform = CreateMidpoint("StartMidpoint", rotationAndData.StartPoint, rotationAndData.StartPointSecond);
                EndTransform = CreateMidpoint("EndMidpoint", rotationAndData.EndPoint, rotationAndData.EndPointSecond);
            }
        }
        
        public Vector3 GetRightBottomPoint()
        {
            return _rotationAndData[1].EndPointSecond.position;
        }

        public Vector3 GetLeftBottomPoint()
        {
            return _rotationAndData[0].StartPoint.position;
        }
        
        private Transform CreateMidpoint(string midpointName, Transform pointA, Transform pointB)
        {
            var midpointObj = new GameObject(midpointName);
            var midTransform = midpointObj.transform;
            midTransform.SetParent(transform);
            midTransform.position = (pointA.position + pointB.position) * 0.5f;
            midTransform.rotation = Quaternion.Lerp(pointA.rotation, pointB.rotation, 0.5f);

            if (midpointName.Contains("Start"))
                _startMidpoint = midTransform;
            else
                _endMidpoint = midTransform;

            return midTransform;
        }
    }
}