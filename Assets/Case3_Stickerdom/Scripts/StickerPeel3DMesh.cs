using System;
using UnityEngine;
using DG.Tweening;

namespace Stickerdom
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class StickerPeel3DMesh : MonoBehaviour
    {
        private static readonly float[] CornerAngles = new float[] { 45f, 135f, 225f, 315f };

        [Header("3D Subdivided Mesh Settings")]
        [Range(12, 36)] [SerializeField] private int gridResolution = 28;

        [Header("Peel Mechanics")]
        [Tooltip("Direction angle in degrees from which the corner curls up.")]
        [Range(0f, 360f)] [SerializeField] private float peelAngle = 45.0f;

        [Tooltip("Cylinder roll radius (smaller = tighter curl, larger = looser curve).")]
        [Range(0.15f, 0.80f)] [SerializeField] private float rollRadius = 0.38f;

        [Tooltip("Backside adhesive color.")]
        [SerializeField] private Color backSideColor = new Color(0.92f, 0.93f, 0.95f, 1.0f);

        private SpriteRenderer spriteRenderer;
        private GameObject meshHolder;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh deformedMesh;

        private Vector3[] baseVertices;
        private Vector3[] workingVertices;
        private Vector2[] baseUVs;
        private int[] baseTriangles;

        private float currentPeelProgress = 0f;
        private Material dynamicMat;
        private bool isInitialized = false;

        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");
        private static readonly int PropBackSideColor = Shader.PropertyToID("_BackSideColor");

        public float PeelProgress
        {
            get => currentPeelProgress;
            set
            {
                currentPeelProgress = Mathf.Clamp(value, 0f, 1.0f);
                DeformMesh();
            }
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            BuildMesh();
        }

        private void BuildMesh()
        {
            if (isInitialized || spriteRenderer == null || spriteRenderer.sprite == null) return;

            Sprite sprite = spriteRenderer.sprite;
            Vector2 spriteSize = sprite.rect.size / sprite.pixelsPerUnit;
            float halfW = spriteSize.x * 0.5f;
            float halfH = spriteSize.y * 0.5f;

            // 1. Create Child GameObject for 3D Mesh
            meshHolder = new GameObject($"{gameObject.name}_3DCurlMesh");
            meshHolder.transform.SetParent(transform, false);
            meshHolder.transform.localPosition = Vector3.zero;
            meshHolder.transform.localRotation = Quaternion.identity;
            meshHolder.transform.localScale = Vector3.one;

            meshFilter = meshHolder.AddComponent<MeshFilter>();
            meshRenderer = meshHolder.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("Custom/StickerDoubleSidedURP") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            dynamicMat = new Material(shader);
            dynamicMat.SetTexture(PropMainTex, sprite.texture);
            dynamicMat.SetColor(PropBackSideColor, backSideColor);
            meshRenderer.material = dynamicMat;
            meshRenderer.sortingOrder = spriteRenderer.sortingOrder;

            // Disable original SpriteRenderer so the 3D curling mesh renders
            spriteRenderer.enabled = false;

            // 2. Subdivide Grid Mesh
            int res = gridResolution;
            int numVerts = (res + 1) * (res + 1);
            baseVertices = new Vector3[numVerts];
            workingVertices = new Vector3[numVerts];
            baseUVs = new Vector2[numVerts];
            baseTriangles = new int[res * res * 6];

            int vertIdx = 0;
            for (int y = 0; y <= res; y++)
            {
                float normY = (float)y / res;
                float posY = Mathf.Lerp(-halfH, halfH, normY);

                for (int x = 0; x <= res; x++)
                {
                    float normX = (float)x / res;
                    float posX = Mathf.Lerp(-halfW, halfW, normX);

                    baseVertices[vertIdx] = new Vector3(posX, posY, 0f);
                    workingVertices[vertIdx] = baseVertices[vertIdx];
                    baseUVs[vertIdx] = new Vector2(normX, normY);
                    vertIdx++;
                }
            }

            int triIdx = 0;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i0 = y * (res + 1) + x;
                    int i1 = i0 + 1;
                    int i2 = (y + 1) * (res + 1) + x;
                    int i3 = i2 + 1;

                    // Front Triangles
                    baseTriangles[triIdx++] = i0;
                    baseTriangles[triIdx++] = i2;
                    baseTriangles[triIdx++] = i1;

                    baseTriangles[triIdx++] = i1;
                    baseTriangles[triIdx++] = i2;
                    baseTriangles[triIdx++] = i3;
                }
            }

            deformedMesh = new Mesh();
            deformedMesh.name = $"{gameObject.name}_DeformedMesh";
            deformedMesh.vertices = workingVertices;
            deformedMesh.uv = baseUVs;
            deformedMesh.triangles = baseTriangles;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateBounds();

            meshFilter.mesh = deformedMesh;
            isInitialized = true;
        }

        public void UpdateSortingOrder(int order)
        {
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = order;
            }
        }

        /// <summary>
        /// True 3D cylinder curl vertex deformation.
        /// </summary>
        private void DeformMesh()
        {
            if (!isInitialized || deformedMesh == null) return;

            float rad = peelAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            Sprite sprite = spriteRenderer.sprite;
            Vector2 spriteSize = sprite.rect.size / sprite.pixelsPerUnit;
            float hw = spriteSize.x * 0.5f;
            float hh = spriteSize.y * 0.5f;

            // 4 corners projection
            float p00 = Vector2.Dot(new Vector2(-hw, -hh), dir);
            float p10 = Vector2.Dot(new Vector2(hw, -hh), dir);
            float p01 = Vector2.Dot(new Vector2(-hw, hh), dir);
            float p11 = Vector2.Dot(new Vector2(hw, hh), dir);

            float pMax = Mathf.Max(Mathf.Max(p00, p10), Mathf.Max(p01, p11));
            float pMin = Mathf.Min(Mathf.Min(p00, p10), Mathf.Min(p01, p11));
            float span = Mathf.Max(pMax - pMin, 0.001f);

            float R = Mathf.Max(rollRadius, 0.1f);
            float tCrease = currentPeelProgress;

            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 basePos = baseVertices[i];
                float proj = Vector2.Dot(new Vector2(basePos.x, basePos.y), dir);
                // t: 0 at peel start corner, 1 at opposite corner
                float t = (pMax - proj) / span;

                // Distance past the fold line into the curled flap
                float curlDist = (tCrease - t) * span;

                if (curlDist <= 0f)
                {
                    // Flat on table
                    workingVertices[i] = basePos;
                }
                else
                {
                    // 3D Cylinder Curl Wrapping
                    float alpha = curlDist / R;

                    if (alpha <= Mathf.PI)
                    {
                        // Wrapping along the 3D cylinder curve lifting towards camera (negative Z)
                        float deltaD = -(curlDist - R * Mathf.Sin(alpha));
                        float zOffset = -R * (1f - Mathf.Cos(alpha));

                        workingVertices[i] = new Vector3(
                            basePos.x + deltaD * dir.x,
                            basePos.y + deltaD * dir.y,
                            zOffset
                        );
                    }
                    else
                    {
                        // Completely rolled over the top and lying flat on top layer (backwards)
                        float deltaD = -(2f * curlDist - Mathf.PI * R);
                        float zOffset = -2f * R;

                        workingVertices[i] = new Vector3(
                            basePos.x + deltaD * dir.x,
                            basePos.y + deltaD * dir.y,
                            zOffset
                        );
                    }
                }
            }

            deformedMesh.vertices = workingVertices;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateBounds();
        }

        public float PickRandomCornerAngle()
        {
            float chosen = CornerAngles[UnityEngine.Random.Range(0, CornerAngles.Length)];
            SetPeelAngle(chosen);
            return chosen;
        }

        public void SetPeelAngle(float angle)
        {
            peelAngle = angle;
            DeformMesh();
        }

        /// <summary>
        /// 3D SÖKÜLME: 0 -> 1 (Sticker köşeden başlayarak 3D rulo şeklinde sökülür, arkası tamamen öne katlanır).
        /// </summary>
        public Tween AnimatePeelOff(float duration = 0.22f)
        {
            return DOTween.To(() => currentPeelProgress, x => PeelProgress = x, 1.0f, duration).SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// 3D GERİ YAPIŞTIRMA (UNROLL): 1 -> 0 (Ayrılan son noktadan başlayarak rulo sayfaya açılır ve düzleşir).
        /// </summary>
        public Tween AnimateReverseUnroll(float duration = 0.20f)
        {
            return DOTween.To(() => currentPeelProgress, x => PeelProgress = x, 0.0f, duration).SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// Sökülmeyi sıfırlar (düzleştirir).
        /// </summary>
        public void ResetPeel()
        {
            currentPeelProgress = 0f;
            DeformMesh();
        }

        private void OnDestroy()
        {
            if (deformedMesh != null) Destroy(deformedMesh);
            if (dynamicMat != null) Destroy(dynamicMat);
        }
    }
}
