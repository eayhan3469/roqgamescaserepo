using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

namespace FitTheShape
{
    public class WheelReactor : MonoBehaviour
    {
        public static WheelReactor Instance { get; private set; }

        [Header("Wheel Grid Structure (5x15 Matrix)")]
        [Tooltip("Root Transform of the wheel (Drum).")]
        [SerializeField] private Transform wheelRoot;

        [Tooltip("List of all 75 individual 1x1 segment blocks.")]
        [SerializeField] private List<Transform> allWheelBlocks = new List<Transform>();

        [Header("Hybrid Shockwave: Center Inward + Ripple Outward")]
        [Tooltip("Inward depression depth at the epicenter hit block (Heavy physical thud).")]
        [SerializeField] private float maxIndentDepth = 0.55f;

        [Tooltip("Maximum outward pop height for immediate surrounding neighbor blocks.")]
        [SerializeField] private float maxOutwardPop = 0.35f;

        [Tooltip("Minimum outward pop height at the outer edge segments.")]
        [SerializeField] private float minOutwardPop = 0.04f;

        [Tooltip("Maximum radius of the ripple effect in grid distance.")]
        [SerializeField] private float maxRippleRadius = 6.0f;

        [Tooltip("Steepness power of the distance falloff.")]
        [SerializeField] private float falloffPower = 1.8f;

        [Tooltip("Delay per grid distance step as the wave cascades outward.")]
        [SerializeField] private float stepDelay = 0.045f;

        [Tooltip("Duration of the initial push/pop phase.")]
        [SerializeField] private float pushDuration = 0.08f;

        [Tooltip("Duration of the spring recovery phase.")]
        [SerializeField] private float springDuration = 0.28f;

        [Tooltip("Overshoot multiplier for the elastic return.")]
        [SerializeField] private float springOvershoot = 1.35f;

        [Header("Feedback Hooks")]
        [Tooltip("Optional particle system played at the moment of impact.")]
        [SerializeField] private ParticleSystem impactParticles;

        [SerializeField] private AudioSource impactAudio;
        [SerializeField] private UnityEvent onReactionTriggered;

        private Transform[,] grid = new Transform[5, 15];
        private Dictionary<Transform, Vector2Int> blockGridCoords = new Dictionary<Transform, Vector2Int>();
        private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();
        private bool isInitialized = false;

        public List<Transform> AllWheelBlocks
        {
            get => allWheelBlocks;
            set => allWheelBlocks = value;
        }

        public float MaxIndentDepth
        {
            get => maxIndentDepth;
            set => maxIndentDepth = value;
        }

        public float MaxOutwardPop
        {
            get => maxOutwardPop;
            set => maxOutwardPop = value;
        }

        public float StepDelay
        {
            get => stepDelay;
            set => stepDelay = value;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            InitializeGrid();
        }

        private void Start()
        {
            InitializeGrid();
        }

        public void InitializeGrid()
        {
            if (isInitialized && allWheelBlocks.Count == 75) return;

            if (wheelRoot == null)
            {
                wheelRoot = transform.name.Contains("Drum") 
                    ? transform 
                    : (transform.parent != null && transform.parent.name.Contains("Drum") ? transform.parent : GameObject.Find("Drum")?.transform);
            }

            if (wheelRoot == null) return;

            allWheelBlocks.Clear();
            blockGridCoords.Clear();
            originalPositions.Clear();
            grid = new Transform[5, 15];

            for (int c = 0; c < 5; c++)
            {
                for (int r = 0; r < 15; r++)
                {
                    Transform seg = wheelRoot.Find($"Segment_c{c}_r{r}");
                    if (seg != null)
                    {
                        grid[c, r] = seg;
                        allWheelBlocks.Add(seg);
                        blockGridCoords[seg] = new Vector2Int(c, r);
                        originalPositions[seg] = seg.localPosition;
                    }
                }
            }

            isInitialized = true;
        }

        public void TriggerReaction(Transform hitTransform, Transform seatedShape = null)
        {
            if (hitTransform == null)
            {
                TriggerReaction(transform.position);
                return;
            }

            Transform segment = hitTransform;
            while (segment != null && !segment.name.StartsWith("Segment_c") && segment.parent != null && segment.parent != wheelRoot)
            {
                segment = segment.parent;
            }

            if (!isInitialized || allWheelBlocks.Count == 0)
            {
                InitializeGrid();
            }

            if (segment != null && blockGridCoords.TryGetValue(segment, out Vector2Int coords))
            {
                TriggerGridReaction(coords.x, coords.y, segment.position, seatedShape);
            }
            else
            {
                TriggerReaction(hitTransform.position);
            }
        }

        public void TriggerReaction(Vector3 impactWorldPosition)
        {
            if (!isInitialized || allWheelBlocks.Count == 0)
            {
                InitializeGrid();
            }

            int bestCol = 0;
            int bestRow = 0;
            float minDistance = float.MaxValue;

            for (int c = 0; c < 5; c++)
            {
                for (int r = 0; r < 15; r++)
                {
                    Transform block = grid[c, r];
                    if (block == null) continue;

                    Transform anchor = block.Find("FrontAnchor_LastTrans") ?? block;
                    float d = Vector3.Distance(impactWorldPosition, anchor.position);
                    if (d < minDistance)
                    {
                        minDistance = d;
                        bestCol = c;
                        bestRow = r;
                    }
                }
            }

            TriggerGridReaction(bestCol, bestRow, impactWorldPosition, null);
        }

        private void TriggerGridReaction(int hitCol, int hitRow, Vector3 impactPos, Transform seatedShape)
        {
            // Trigger Resonance Wobble Audio
            if (FitTheShapeAudioManager.Instance != null)
            {
                FitTheShapeAudioManager.Instance.PlayWobbleSound();
            }

            if (impactParticles != null)
            {
                impactParticles.transform.position = impactPos;
                impactParticles.Play();
            }

            if (impactAudio != null)
            {
                impactAudio.Play();
            }

            onReactionTriggered?.Invoke();

            if (!isInitialized || allWheelBlocks.Count == 0)
            {
                InitializeGrid();
            }

            // 1. Merkez segment (hitCol, hitRow) fiziksel darbeyle İÇE ÇÖKER
            // 2. Çevre halkalar su dalgası gibi DIŞA KABARARAK (Pop) yayılır
            for (int c = 0; c < 5; c++)
            {
                for (int r = 0; r < 15; r++)
                {
                    Transform block = grid[c, r];
                    if (block == null) continue;

                    float dC = Mathf.Abs(c - hitCol);
                    int rawDR = Mathf.Abs(r - hitRow);
                    int dR = rawDR > 7 ? 15 - rawDR : rawDR;

                    float ringDist = Mathf.Sqrt(dC * dC + dR * dR);
                    if (ringDist > maxRippleRadius) continue;

                    bool isHitCenter = (c == hitCol && r == hitRow);
                    float delay = ringDist * stepDelay;

                    // Çevre halkalar için dışa kabarma yüksekliği hesabı
                    float norm = Mathf.Clamp01(1.0f - (ringDist / maxRippleRadius));
                    float curve = Mathf.Pow(norm, falloffPower);
                    float popHeight = Mathf.Lerp(minOutwardPop, maxOutwardPop, curve);

                    if (delay <= 0.001f)
                    {
                        PlayGridRipple(block, popHeight, isHitCenter, isHitCenter ? seatedShape : null);
                    }
                    else
                    {
                        DOVirtual.DelayedCall(delay, () => PlayGridRipple(block, popHeight, false, null));
                    }
                }
            }
        }

        private void PlayGridRipple(Transform block, float amount, bool isHitCenter, Transform seatedShape)
        {
            if (block == null) return;

            block.DOKill();

            if (!originalPositions.TryGetValue(block, out Vector3 origPos))
            {
                origPos = block.localPosition;
                originalPositions[block] = origPos;
            }

            Vector3 normalDir = block.localRotation * Vector3.up;
            Sequence rippleSeq = DOTween.Sequence();
            rippleSeq.SetTarget(block);

            if (isHitCenter)
            {
                // MERKEZ BLOK: İçe Çöküş (Darbe, Ağırlık ve Tokluk)
                Vector3 inwardTarget = origPos - normalDir * maxIndentDepth;
                rippleSeq.Append(block.DOLocalMove(inwardTarget, pushDuration).SetEase(Ease.OutCubic));
                rippleSeq.Append(block.DOLocalMove(origPos, springDuration).SetEase(Ease.OutBack, springOvershoot));

                // Şekil de merkez blokla senkron dünya koordinatlarında içe çöküp yaylanır
                if (seatedShape != null)
                {
                    seatedShape.DOKill();
                    Vector3 shapeOrigWorld = seatedShape.position;
                    Vector3 shapeTargetWorld = shapeOrigWorld - (block.up * maxIndentDepth);

                    Sequence shapeSeq = DOTween.Sequence();
                    shapeSeq.SetTarget(seatedShape);
                    shapeSeq.Append(seatedShape.DOMove(shapeTargetWorld, pushDuration).SetEase(Ease.OutCubic));
                    shapeSeq.Append(seatedShape.DOMove(shapeOrigWorld, springDuration).SetEase(Ease.OutBack, springOvershoot));
                }
            }
            else
            {
                // ÇEVRE BLOKLAR: Dışa Pop / Kabarma (Su Dalgası & Juicy Ödül)
                Vector3 outwardTarget = origPos + normalDir * amount;
                rippleSeq.Append(block.DOLocalMove(outwardTarget, pushDuration).SetEase(Ease.OutQuad));
                rippleSeq.Append(block.DOLocalMove(origPos, springDuration).SetEase(Ease.OutBack, springOvershoot));
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < allWheelBlocks.Count; i++)
            {
                if (allWheelBlocks[i] != null)
                {
                    allWheelBlocks[i].DOKill();
                }
            }
        }
    }
}
