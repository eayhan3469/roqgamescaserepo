using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using DG.Tweening;

namespace FitTheShape
{
    [RequireComponent(typeof(Collider))]
    public class ShapeController : MonoBehaviour, IPointerClickHandler
    {
        [Header("Target & Anchor Transforms")]
        [Tooltip("The target segment or hole on the wheel.")]
        [FormerlySerializedAs("targetTransform")]
        [SerializeField] private Transform targetHole;

        [Tooltip("First flight anchor (FrontAnchor_FirstTrans) for anticipation/lift.")]
        [SerializeField] private Transform firstAnchor;

        [Tooltip("Last flight anchor (FrontAnchor_LastTrans) for final insertion/fit.")]
        [SerializeField] private Transform lastAnchor;

        [Tooltip("Inward offset so the shape sits snugly inside the hole without sticking out.")]
        [SerializeField] private float slotDepthOffset = 0.12f;

        [Header("Stage 1: Parabolic Arc Flight (Over the Drum)")]
        [Tooltip("Height of the arc towards the camera to prevent clipping through drum segments.")]
        [SerializeField] private float arcLiftTowardsCamera = 3.0f;

        [Tooltip("Duration of the parabolic arc flight to FrontAnchor_FirstTrans.")]
        [SerializeField] private float liftDuration = 0.28f;

        [Tooltip("Scale multiplier during the anticipation peak (relative to shape's original scale).")]
        [SerializeField] private float liftScaleMultiplier = 1.2f;

        [Tooltip("Easing curve for the parabolic arc.")]
        [SerializeField] private Ease liftEase = Ease.OutQuad;

        [Header("Stage 2: Insertion & Y-Axis Spin (Into Hole)")]
        [Tooltip("Duration of the final insertion snap into LastTrans.")]
        [SerializeField] private float insertDuration = 0.22f;

        [Tooltip("Spin rotation around local Y-axis in degrees.")]
        [SerializeField] private Vector3 spinRotation = new Vector3(0f, 360f, 0f);

        [Tooltip("Snappy easing curve for insertion into the hole.")]
        [SerializeField] private Ease insertEase = Ease.InBack;

        [Header("Feedback Hooks")]
        [SerializeField] private ParticleSystem vfxOnEntered;
        [SerializeField] private AudioSource sfxOnEntered;
        [SerializeField] private UnityEvent OnShapeMoveStarted;
        [SerializeField] private UnityEvent OnShapeEntered;

        private bool isTriggered = false;
        private Collider shapeCollider;
        private Vector3 originalScale;
        private Sequence activeSequence;

        public Transform TargetHole
        {
            get => targetHole;
            set
            {
                targetHole = value;
                ResolveAnchors();
            }
        }

        public Transform FirstAnchor
        {
            get => firstAnchor;
            set => firstAnchor = value;
        }

        public Transform LastAnchor
        {
            get => lastAnchor;
            set => lastAnchor = value;
        }

        public float SlotDepthOffset
        {
            get => slotDepthOffset;
            set => slotDepthOffset = value;
        }

        public float ArcLiftTowardsCamera
        {
            get => arcLiftTowardsCamera;
            set => arcLiftTowardsCamera = value;
        }

        public float LiftDuration
        {
            get => liftDuration;
            set => liftDuration = value;
        }

        public float InsertDuration
        {
            get => insertDuration;
            set => insertDuration = value;
        }

        public Ease LiftEase
        {
            get => liftEase;
            set => liftEase = value;
        }

        public Ease InsertEase
        {
            get => insertEase;
            set => insertEase = value;
        }

        private void Awake()
        {
            shapeCollider = GetComponent<Collider>();
            originalScale = transform.localScale;
            ResolveAnchors();
        }

        private void OnValidate()
        {
            ResolveAnchors();
        }

        public void ResolveAnchors()
        {
            if (targetHole == null) return;

            Transform searchRoot = targetHole.name.Contains("Segment_") ? targetHole : targetHole.parent;
            if (searchRoot == null) searchRoot = targetHole;

            if (firstAnchor == null)
            {
                firstAnchor = searchRoot.Find("FrontAnchor_FirstTrans");
            }

            if (lastAnchor == null)
            {
                lastAnchor = searchRoot.Find("FrontAnchor_LastTrans");
            }
        }

        private void OnMouseDown()
        {
            TriggerFitSequence();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerFitSequence();
        }

        public void TriggerFitSequence()
        {
            if (isTriggered) return;
            isTriggered = true;

            ResolveAnchors();

            if (firstAnchor == null || lastAnchor == null)
            {
                Debug.LogWarning($"[ShapeController] Missing anchor transforms on '{gameObject.name}'! TargetHole: {targetHole?.name}", this);
                return;
            }

            if (shapeCollider != null)
            {
                shapeCollider.enabled = false;
            }

            OnShapeMoveStarted?.Invoke();

            activeSequence?.Kill();
            activeSequence = DOTween.Sequence();

            Vector3 startPos = transform.position;
            Vector3 targetFirstPos = firstAnchor.position;

            // Kameranın tersine kavis tepe noktası
            Camera mainCam = Camera.main;
            Vector3 toCameraDir = mainCam != null ? -mainCam.transform.forward : new Vector3(0f, 0.94f, -0.34f);
            Vector3 arcMidPoint = Vector3.Lerp(startPos, targetFirstPos, 0.5f) + toCameraDir * arcLiftTowardsCamera;

            Vector3[] arcPath = new Vector3[] {
                startPos,
                arcMidPoint,
                targetFirstPos
            };

            // Şeklin yuvanın içine tam oturması için LastTrans'in normaline dik içe doğru ofset
            Vector3 sunkenTargetPos = lastAnchor.position - (lastAnchor.up * slotDepthOffset);

            // Stage 1: Tamburun üzerinden kameraya doğru kavisle uçuş
            Vector3 popScale = originalScale * liftScaleMultiplier;
            activeSequence.Append(transform.DOPath(arcPath, liftDuration, PathType.CatmullRom).SetEase(liftEase));
            activeSequence.Join(transform.DORotateQuaternion(firstAnchor.rotation, liftDuration).SetEase(liftEase));
            activeSequence.Join(transform.DOScale(popScale, liftDuration).SetEase(liftEase));

            // Stage 2: Yuvanın içine 360 spin ile net yerleşme
            activeSequence.Append(transform.DOMove(sunkenTargetPos, insertDuration).SetEase(insertEase));
            activeSequence.Join(transform.DORotate(spinRotation, insertDuration, RotateMode.LocalAxisAdd).SetEase(insertEase));
            activeSequence.Join(transform.DOScale(originalScale, insertDuration).SetEase(insertEase));

            // Stage 3: Tam yerine oturduğunda koordinatları sabitle ve dalgayı tetikle
            activeSequence.OnComplete(() =>
            {
                // Non-uniform scale bozulmasını engellemek için parent almadan doğrudan dünya koordinatlarında sabitliyoruz
                transform.position = sunkenTargetPos;
                transform.rotation = lastAnchor.rotation;
                transform.localScale = originalScale;

                if (vfxOnEntered != null)
                {
                    vfxOnEntered.Play();
                }

                if (sfxOnEntered != null)
                {
                    sfxOnEntered.Play();
                }

                // Dalgayı şekil referansıyla birlikte başlat (şekil de segmentle birlikte senkron çöküp yaylanır)
                if (WheelReactor.Instance != null)
                {
                    WheelReactor.Instance.TriggerReaction(lastAnchor, transform);
                }

                OnShapeEntered?.Invoke();
            });
        }

        private void OnDestroy()
        {
            activeSequence?.Kill();
        }
    }
}
