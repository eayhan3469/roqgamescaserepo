using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

namespace BlockHole
{
    public class HoleTarget : MonoBehaviour
    {
        [Header("Shape Identification")]
        [Tooltip("Shape type matching BlockDraggable.shapeType.")]
        [SerializeField] private BlockShapeType shapeType = BlockShapeType.Single;

        [Tooltip("Exact world position where the block's root transform should snap when entering this hole.")]
        [SerializeField] private Vector3 targetDropWorldPos;

        [Tooltip("Exact anchor grid position of this hole on the board (if applicable).")]
        [SerializeField] private Vector2Int anchorGridPos;

        [Tooltip("Relative tile footprint cells forming this hole.")]
        [SerializeField] private List<Vector2Int> footprint = new List<Vector2Int> { Vector2Int.zero };

        [Header("Tolerances & Drop Settings")]
        [Tooltip("Maximum XZ distance tolerance for snapping into this hole.")]
        [SerializeField] private float snapTolerance = 0.85f;

        [Tooltip("Y position down the hole shaft where the block falls.")]
        [SerializeField] private float dropDepthY = -2.5f;

        [Header("State & Visuals")]
        [SerializeField] private bool isFilled = false;
        [SerializeField] private bool isHighlighted = false;
        [SerializeField] private ParticleSystem holeGlowRays;
        [SerializeField] private ParticleSystem magicDust;
        [SerializeField] private GameObject isActiveVisual;
        [SerializeField] private Color emissionGlowColor = Color.white;
        [Range(0f, 1f)] [SerializeField] private float maxHighlightEmission = 0.25f;

        [Header("Events")]
        [SerializeField] private UnityEvent onBlockEnteredHole;

        private MeshRenderer meshRenderer;
        private Material dynamicMat;
        private Tweener emissionTween;
        private ParticleSystem[] allChildParticles;

        public BlockShapeType ShapeType { get => shapeType; set => shapeType = value; }
        public Vector3 TargetDropWorldPos { get => targetDropWorldPos; set => targetDropWorldPos = value; }
        public Vector2Int AnchorGridPos { get => anchorGridPos; set => anchorGridPos = value; }
        public List<Vector2Int> Footprint => footprint;
        public float SnapTolerance => snapTolerance;
        public float DropDepthY => dropDepthY;
        public bool IsFilled => isFilled;
        public bool IsHighlighted => isHighlighted;
        public Color EmissionGlowColor { get => emissionGlowColor; set => emissionGlowColor = value; }

        private void Awake()
        {
            InitializeVisuals();
        }

        public void InitializeVisuals()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                dynamicMat = Application.isPlaying ? meshRenderer.material : meshRenderer.sharedMaterial;

                if (emissionGlowColor == Color.white && dynamicMat != null && dynamicMat.HasProperty("_BaseColor"))
                {
                    emissionGlowColor = dynamicMat.color;
                }
            }

            if (holeGlowRays == null)
            {
                holeGlowRays = transform.Find("HoleGlowRays")?.GetComponent<ParticleSystem>();
            }
            if (magicDust == null)
            {
                magicDust = transform.Find("MagicDust")?.GetComponent<ParticleSystem>();
            }
            if (isActiveVisual == null)
            {
                isActiveVisual = transform.Find("IsActive")?.gameObject;
            }

            allChildParticles = GetComponentsInChildren<ParticleSystem>(true);
            SetHighlight(false, true);
        }

        public bool Matches(BlockDraggable block)
        {
            if (isFilled || block == null) return false;
            return block.ShapeType == shapeType;
        }

        public bool IsWithinSnapDistance(Vector3 blockWorldPos)
        {
            if (isFilled) return false;
            Vector2 bXZ = new Vector2(blockWorldPos.x, blockWorldPos.z);
            Vector2 hXZ = new Vector2(targetDropWorldPos.x, targetDropWorldPos.z);
            return Vector2.Distance(bXZ, hXZ) <= snapTolerance;
        }

        public void SetHighlight(bool active, bool immediate = false)
        {
            if (isFilled) return;
            isHighlighted = active;

            emissionTween?.Kill();

            if (dynamicMat == null && meshRenderer != null)
            {
                dynamicMat = Application.isPlaying ? meshRenderer.material : meshRenderer.sharedMaterial;
            }

            if (active)
            {
                if (isActiveVisual != null) isActiveVisual.SetActive(true);

                if (allChildParticles != null)
                {
                    foreach (var ps in allChildParticles)
                    {
                        if (ps != null && !ps.isPlaying) ps.Play();
                    }
                }

                // Subtle soft rim glow preserving deep 3D dark pit
                if (dynamicMat != null && dynamicMat.HasProperty("_EmissionColor") && maxHighlightEmission > 0f)
                {
                    dynamicMat.EnableKeyword("_EMISSION");
                    Color targetColor = (emissionGlowColor != Color.black ? emissionGlowColor : Color.white) * maxHighlightEmission;
                    if (immediate)
                    {
                        dynamicMat.SetColor("_EmissionColor", targetColor);
                    }
                    else
                    {
                        emissionTween = dynamicMat.DOColor(targetColor, "_EmissionColor", 0.2f);
                    }
                }
            }
            else
            {
                if (allChildParticles != null)
                {
                    foreach (var ps in allChildParticles)
                    {
                        if (ps != null && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }

                if (dynamicMat != null && dynamicMat.HasProperty("_EmissionColor"))
                {
                    if (immediate)
                    {
                        dynamicMat.SetColor("_EmissionColor", Color.black);
                    }
                    else
                    {
                        emissionTween = dynamicMat.DOColor(Color.black, "_EmissionColor", 0.25f);
                    }
                }
            }
        }

        public void SetFilled(bool filled)
        {
            isFilled = filled;
            if (isFilled)
            {
                SetHighlight(false, true);
                if (isActiveVisual != null)
                {
                    isActiveVisual.SetActive(false);
                }
            }
        }

        public void OnBlockDropped(BlockDraggable block)
        {
            SetFilled(true);
            onBlockEnteredHole?.Invoke();
        }

        private void OnDestroy()
        {
            emissionTween?.Kill();
        }
    }
}
