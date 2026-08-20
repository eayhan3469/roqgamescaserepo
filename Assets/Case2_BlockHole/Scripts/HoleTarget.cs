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

        [Header("Neon Edge Glow")]
        [SerializeField] private HoleEdgeGlow edgeGlow;
        [SerializeField] private Color emissionGlowColor = Color.white;
        [SerializeField] private Material edgeGlowMaterial;

        [Header("State & Visuals")]
        [SerializeField] private bool isFilled = false;
        [SerializeField] private bool isHighlighted = false;
        [SerializeField] private GameObject isActiveVisual;

        [Header("Staggered Hole Close Timings")]
        [Tooltip("Delay after block impact before floor tiles start rising (waits for debris to finish).")]
        [SerializeField] private float holeCloseInitialDelay = 0.48f;

        [Tooltip("Stagger interval between each rising floor tile (tık-tık-tık effect).")]
        [SerializeField] private float tileStaggerInterval = 0.085f;

        [Header("Events")]
        [SerializeField] private UnityEvent onBlockEnteredHole;

        private MeshRenderer meshRenderer;
        private Material dynamicMat;
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
        public HoleEdgeGlow EdgeGlow { get => edgeGlow; set => edgeGlow = value; }

        private void Awake()
        {
            InitializeVisuals();
        }

        private void Start()
        {
            // Re-verify perimeter once GridManager is fully initialized
            if (edgeGlow != null && GridManager.Instance != null)
            {
                Vector3 originPos = GridManager.Instance.OriginWorldPosition;
                Vector3 gridOriginBase = originPos - new Vector3(0.5f, 0f, 0.5f);
                edgeGlow.BuildPerimeterFromCells(GetWorldFootprint(), gridOriginBase, GridManager.Instance.TileSize);
            }
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

            if (isActiveVisual == null)
            {
                isActiveVisual = transform.Find("IsActive")?.gameObject;
            }

            // Cache child particles (including VacumTile)
            allChildParticles = GetComponentsInChildren<ParticleSystem>(true);

            // Setup Crisp Neon Edge Glow
            if (edgeGlow == null)
            {
                edgeGlow = GetComponent<HoleEdgeGlow>() ?? gameObject.AddComponent<HoleEdgeGlow>();
            }

            if (edgeGlow != null)
            {
                if (edgeGlowMaterial != null) edgeGlow.GlowMaterial = edgeGlowMaterial;
                edgeGlow.GlowColor = emissionGlowColor;

                // Build contour lines from grid cells
                Vector3 originPos = GridManager.Instance != null ? GridManager.Instance.OriginWorldPosition : new Vector3(0.5f, 0.03f, 1.0f);
                Vector3 gridOriginBase = originPos - new Vector3(0.5f, 0f, 0.5f);
                edgeGlow.BuildPerimeterFromCells(GetWorldFootprint(), gridOriginBase, GridManager.Instance != null ? GridManager.Instance.TileSize : 1.0f);
            }

            SetHighlight(false, true);
        }

        public List<Vector2Int> GetWorldFootprint()
        {
            List<Vector2Int> list = new List<Vector2Int>(footprint.Count);
            for (int i = 0; i < footprint.Count; i++)
            {
                list.Add(anchorGridPos + footprint[i]);
            }
            return list;
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

            // Trigger Crisp Neon Perimeter Edge Glow
            if (edgeGlow != null)
            {
                edgeGlow.SetGlow(active, immediate);
            }

            if (isActiveVisual != null)
            {
                isActiveVisual.SetActive(active);
            }

            // Play / Stop child vacuum particles (VacumTile)
            if (allChildParticles != null)
            {
                foreach (var ps in allChildParticles)
                {
                    if (ps != null && ps != edgeGlow.GetComponent<ParticleSystem>())
                    {
                        if (active)
                        {
                            if (!ps.isPlaying) ps.Play();
                        }
                        else
                        {
                            if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                        }
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
            CloseHoleWithStaggeredTiles(holeCloseInitialDelay, tileStaggerInterval);
        }

        public void CloseHoleWithStaggeredTiles(float initialDelay = 0.48f, float tileInterval = 0.085f)
        {
            DOVirtual.DelayedCall(initialDelay, () =>
            {
                // Deactivate hole visual once pieces have gone
                transform.DOScale(Vector3.zero, 0.20f).SetEase(Ease.InBack).OnComplete(() =>
                {
                    gameObject.SetActive(false);
                });

                if (GridManager.Instance != null)
                {
                    List<Vector2Int> cells = GetWorldFootprint();
                    for (int i = 0; i < cells.Count; i++)
                    {
                        Vector2Int cell = cells[i];
                        float tileDelay = i * tileInterval;
                        DOVirtual.DelayedCall(tileDelay, () =>
                        {
                            GridManager.Instance.CloseHoleCell(cell);
                        });
                    }
                }
            });
        }
    }
}
