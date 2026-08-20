using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace BlockHole
{
    [RequireComponent(typeof(LineRenderer))]
    public class HoleEdgeGlow : MonoBehaviour
    {
        [Header("Glow Appearance")]
        [SerializeField] private Material glowMaterial;
        [SerializeField] private Color glowColor = Color.cyan;
        [Tooltip("Thin, sharp, crisp laser outline width.")]
        [SerializeField] private float baseLineWidth = 0.055f;
        [SerializeField] private float pulseLineWidth = 0.075f;
        [SerializeField] private float heightOffset = 0.045f;

        [Header("Juice & Animation")]
        [SerializeField] private float fadeInDuration = 0.12f;
        [SerializeField] private float fadeOutDuration = 0.15f;
        [SerializeField] private float pulseCycleDuration = 0.55f;

        private LineRenderer lineRenderer;
        private Tweener widthTween;
        private Tweener pulseTween;
        private bool isGlowing = false;
        private Material dynamicGlowMat;

        public Color GlowColor
        {
            get => glowColor;
            set
            {
                glowColor = value;
                ApplyColor();
            }
        }

        public Material GlowMaterial
        {
            get => glowMaterial;
            set
            {
                glowMaterial = value;
                if (lineRenderer != null && glowMaterial != null)
                {
                    lineRenderer.sharedMaterial = glowMaterial;
                }
            }
        }

        public Material GlowParticleMaterial
        {
            get => GlowMaterial;
            set => GlowMaterial = value;
        }

        private void Awake()
        {
            InitializeRenderer();
        }

        public void InitializeRenderer()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (lineRenderer != null)
            {
                lineRenderer.useWorldSpace = true;
                lineRenderer.loop = true;
                lineRenderer.alignment = LineAlignment.View;
                lineRenderer.numCornerVertices = 6;
                lineRenderer.numCapVertices = 6;
                lineRenderer.textureMode = LineTextureMode.Tile;
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;
                lineRenderer.startWidth = 0f;
                lineRenderer.endWidth = 0f;
                lineRenderer.enabled = false;

                if (glowMaterial == null)
                {
                    glowMaterial = Resources.Load<Material>("Mat_LaserRimGlow");
                }

                if (glowMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit") 
                                 ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                                 ?? Shader.Find("Sprites/Default");
                    
                    dynamicGlowMat = new Material(shader);
                    dynamicGlowMat.name = "Mat_ProceduralNeonGlow";
                    dynamicGlowMat.SetFloat("_Surface", 1f);
                    dynamicGlowMat.SetFloat("_Blend", 0f);
                    dynamicGlowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    dynamicGlowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    dynamicGlowMat.SetInt("_ZWrite", 0);
                    dynamicGlowMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;

                    glowMaterial = dynamicGlowMat;
                }

                lineRenderer.sharedMaterial = glowMaterial;
                ApplyColor();
            }
        }

        private void ApplyColor()
        {
            if (lineRenderer == null) return;

            Gradient gradient = new Gradient();
            Color vibrantColor = glowColor * 1.25f;
            vibrantColor.a = 0.95f;

            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(vibrantColor, 0.0f), new GradientColorKey(vibrantColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.95f, 0.0f), new GradientAlphaKey(0.95f, 1.0f) }
            );

            lineRenderer.colorGradient = gradient;
        }

        public void BuildPerimeterFromCells(List<Vector2Int> footprintCells, Vector3 originWorldPos, float tileSize = 1.0f)
        {
            if (footprintCells == null || footprintCells.Count == 0) return;

            InitializeRenderer();

            // 1. Collect all boundary edges
            HashSet<Vector2Int> cellSet = new HashSet<Vector2Int>(footprintCells);
            List<Segment> perimeterSegments = new List<Segment>();

            foreach (var cell in footprintCells)
            {
                float minX = cell.x * tileSize;
                float maxX = (cell.x + 1) * tileSize;
                float minZ = cell.y * tileSize;
                float maxZ = (cell.y + 1) * tileSize;

                Vector2 p00 = new Vector2(minX, minZ);
                Vector2 p10 = new Vector2(maxX, minZ);
                Vector2 p11 = new Vector2(maxX, maxZ);
                Vector2 p01 = new Vector2(minX, maxZ);

                // Bottom Edge
                if (!cellSet.Contains(new Vector2Int(cell.x, cell.y - 1)))
                    perimeterSegments.Add(new Segment(p00, p10));

                // Right Edge
                if (!cellSet.Contains(new Vector2Int(cell.x + 1, cell.y)))
                    perimeterSegments.Add(new Segment(p10, p11));

                // Top Edge
                if (!cellSet.Contains(new Vector2Int(cell.x, cell.y + 1)))
                    perimeterSegments.Add(new Segment(p11, p01));

                // Left Edge
                if (!cellSet.Contains(new Vector2Int(cell.x - 1, cell.y)))
                    perimeterSegments.Add(new Segment(p01, p00));
            }

            // 2. Chain segments into an ordered closed polygon loop
            List<Vector2> orderedPoints = ChainSegments(perimeterSegments);

            // 3. Convert to world positions with height offset
            Vector3[] worldPoints = new Vector3[orderedPoints.Count];
            for (int i = 0; i < orderedPoints.Count; i++)
            {
                worldPoints[i] = new Vector3(
                    originWorldPos.x + orderedPoints[i].x,
                    heightOffset,
                    originWorldPos.z + orderedPoints[i].y
                );
            }

            lineRenderer.positionCount = worldPoints.Length;
            lineRenderer.SetPositions(worldPoints);
        }

        private List<Vector2> ChainSegments(List<Segment> segments)
        {
            List<Vector2> points = new List<Vector2>();
            if (segments.Count == 0) return points;

            List<Segment> pool = new List<Segment>(segments);
            Segment current = pool[0];
            pool.RemoveAt(0);
            points.Add(current.A);

            Vector2 nextTarget = current.B;

            while (pool.Count > 0)
            {
                int bestIdx = -1;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (Vector2.Distance(pool[i].A, nextTarget) < 0.001f)
                    {
                        bestIdx = i;
                        break;
                    }
                }

                if (bestIdx == -1) break;

                current = pool[bestIdx];
                pool.RemoveAt(bestIdx);

                points.Add(current.A);
                nextTarget = current.B;
            }

            return SimplifyCollinearPoints(points);
        }

        private List<Vector2> SimplifyCollinearPoints(List<Vector2> points)
        {
            if (points.Count <= 3) return points;

            List<Vector2> simplified = new List<Vector2>();
            int n = points.Count;

            for (int i = 0; i < n; i++)
            {
                Vector2 prev = points[(i - 1 + n) % n];
                Vector2 curr = points[i];
                Vector2 next = points[(i + 1) % n];

                Vector2 dir1 = (curr - prev).normalized;
                Vector2 dir2 = (next - curr).normalized;

                if (Vector2.Distance(dir1, dir2) > 0.01f)
                {
                    simplified.Add(curr);
                }
            }

            return simplified.Count >= 3 ? simplified : points;
        }

        public void SetGlow(bool active, bool immediate = false)
        {
            isGlowing = active;

            widthTween?.Kill();
            pulseTween?.Kill();

            if (lineRenderer == null) return;

            if (active)
            {
                lineRenderer.enabled = true;

                if (immediate)
                {
                    lineRenderer.startWidth = baseLineWidth;
                    lineRenderer.endWidth = baseLineWidth;
                    StartPulse();
                }
                else
                {
                    widthTween = DOTween.To(
                        () => lineRenderer.startWidth,
                        w => { lineRenderer.startWidth = w; lineRenderer.endWidth = w; },
                        baseLineWidth,
                        fadeInDuration
                    ).SetEase(Ease.OutQuad).OnComplete(StartPulse);
                }
            }
            else
            {
                if (immediate)
                {
                    lineRenderer.startWidth = 0f;
                    lineRenderer.endWidth = 0f;
                    lineRenderer.enabled = false;
                }
                else
                {
                    widthTween = DOTween.To(
                        () => lineRenderer.startWidth,
                        w => { lineRenderer.startWidth = w; lineRenderer.endWidth = w; },
                        0f,
                        fadeOutDuration
                    ).SetEase(Ease.InQuad).OnComplete(() =>
                    {
                        if (!isGlowing) lineRenderer.enabled = false;
                    });
                }
            }
        }

        private void StartPulse()
        {
            if (!isGlowing || lineRenderer == null) return;

            pulseTween = DOTween.To(
                () => lineRenderer.startWidth,
                w => { lineRenderer.startWidth = w; lineRenderer.endWidth = w; },
                pulseLineWidth,
                pulseCycleDuration
            ).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        private void OnDestroy()
        {
            widthTween?.Kill();
            pulseTween?.Kill();
            if (dynamicGlowMat != null)
            {
                if (Application.isPlaying) Destroy(dynamicGlowMat);
                else DestroyImmediate(dynamicGlowMat);
            }
        }

        private struct Segment
        {
            public Vector2 A;
            public Vector2 B;

            public Segment(Vector2 a, Vector2 b)
            {
                A = a;
                B = b;
            }
        }
    }
}
