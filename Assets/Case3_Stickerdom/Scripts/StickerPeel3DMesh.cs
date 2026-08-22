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
        [Range(12, 40)] [SerializeField] private int gridResolution = 32;

        [Header("Peel Mechanics")]
        [Tooltip("Direction angle in degrees from which the corner curls up.")]
        [Range(0f, 360f)] [SerializeField] private float peelAngle = 45.0f;

        [Tooltip("Cylinder roll radius (smaller = tighter curl, larger = looser curve).")]
        [Range(0.15f, 0.80f)] [SerializeField] private float rollRadius = 0.38f;

        [Tooltip("Backside adhesive color.")]
        [SerializeField] private Color backSideColor = new Color(0.90f, 0.91f, 0.93f, 1.0f);

        private SpriteRenderer spriteRenderer;
        private GameObject meshHolder;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh deformedMesh;

        private Vector3[] baseVertices;
        private Vector3[] workingVertices;
        private Color[] workingColors;
        private Vector2[] baseUVs;
        private int[] baseTriangles;

        private float currentPeelProgress = 0f;
        private float currentShineProgress = -0.5f;
        private Material dynamicMat;
        private bool isInitialized = false;

        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");
        private static readonly int PropColor = Shader.PropertyToID("_Color");
        private static readonly int PropBackSideColor = Shader.PropertyToID("_BackSideColor");
        private static readonly int PropShineProgress = Shader.PropertyToID("_ShineProgress");

        public float PeelProgress
        {
            get => currentPeelProgress;
            set
            {
                currentPeelProgress = Mathf.Clamp(value, 0f, 1.0f);
                DeformMesh();
            }
        }

        public float ShineProgress
        {
            get => currentShineProgress;
            set
            {
                currentShineProgress = value;
                if (dynamicMat != null)
                {
                    dynamicMat.SetFloat(PropShineProgress, currentShineProgress);
                }
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

            Material baseMat = Resources.Load<Material>("Mat_StickerDoubleSided");
            Shader shader = (baseMat != null && baseMat.shader != null) ? baseMat.shader : Shader.Find("Custom/StickerDoubleSidedURP");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            }

            dynamicMat = new Material(shader);
            dynamicMat.name = $"Mat_{gameObject.name}_Instance";
            dynamicMat.SetTexture(PropMainTex, sprite.texture);
            if (dynamicMat.HasProperty("_BaseMap")) dynamicMat.SetTexture("_BaseMap", sprite.texture);
            dynamicMat.mainTexture = sprite.texture;
            dynamicMat.SetColor(PropColor, spriteRenderer.color);
            dynamicMat.SetColor(PropBackSideColor, backSideColor);
            dynamicMat.SetFloat(PropShineProgress, currentShineProgress);
            meshRenderer.material = dynamicMat;
            meshRenderer.sortingOrder = spriteRenderer.sortingOrder;

            // Disable original SpriteRenderer so the 3D curling mesh renders
            spriteRenderer.enabled = false;

            // 2. Subdivide Grid Mesh (Higher resolution for silky smooth curvature)
            int res = gridResolution;
            int numVerts = (res + 1) * (res + 1);
            baseVertices = new Vector3[numVerts];
            workingVertices = new Vector3[numVerts];
            workingColors = new Color[numVerts];
            baseUVs = new Vector2[numVerts];
            baseTriangles = new int[res * res * 6];

            Vector4 uvRect = UnityEngine.Sprites.DataUtility.GetInnerUV(sprite);
            float minU = uvRect.x;
            float minV = uvRect.y;
            float maxU = uvRect.z;
            float maxV = uvRect.w;

            int vertIdx = 0;
            for (int y = 0; y <= res; y++)
            {
                float normY = (float)y / res;
                float posY = Mathf.Lerp(-halfH, halfH, normY);
                float uvY = Mathf.Lerp(minV, maxV, normY);

                for (int x = 0; x <= res; x++)
                {
                    float normX = (float)x / res;
                    float posX = Mathf.Lerp(-halfW, halfW, normX);
                    float uvX = Mathf.Lerp(minU, maxU, normX);

                    baseVertices[vertIdx] = new Vector3(posX, posY, 0f);
                    workingVertices[vertIdx] = baseVertices[vertIdx];
                    workingColors[vertIdx] = new Color(0f, 0f, 0f, 1f);
                    baseUVs[vertIdx] = new Vector2(uvX, uvY);
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
            deformedMesh.colors = workingColors;
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
        /// True 3D cylinder curl vertex deformation with exact geometric Apex Highlight.
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
                    workingColors[i] = new Color(0f, 0f, 0f, 1f);
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

                        // Geometric Apex Highlight peaks precisely at the roll crest
                        float normalizedCurve = Mathf.Clamp01(alpha / Mathf.PI);
                        float apexFactor = Mathf.Sin(normalizedCurve * Mathf.PI);
                        workingColors[i] = new Color(apexFactor, 0f, 0f, 1f);
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

                        // Flat folded back
                        workingColors[i] = new Color(0f, 0f, 0f, 1f);
                    }
                }
            }

            deformedMesh.vertices = workingVertices;
            deformedMesh.colors = workingColors;
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
        /// 3D SÖKÜLME: 0 -> 1 (Ultra-smooth Ease.InOutSine ile kadifemsi sökülme).
        /// </summary>
        public Tween AnimatePeelOff(float duration = 0.46f)
        {
            return DOTween.To(() => currentPeelProgress, x => PeelProgress = x, 1.0f, duration).SetEase(Ease.InOutSine);
        }

        /// <summary>
        /// 3D GERİ YAPIŞTIRMA (UNROLL): 1 -> 0 (Ultra-smooth Ease.InOutSine ile kadifemsi sayfaya açılma).
        /// </summary>
        public Tween AnimateReverseUnroll(float duration = 0.42f)
        {
            return DOTween.To(() => currentPeelProgress, x => PeelProgress = x, 0.0f, duration).SetEase(Ease.InOutSine);
        }

        /// <summary>
        /// Yapışma tamamlandığında sticker'ın üzerinden geçen ışık şeridi (Shine Ray Sweep).
        /// </summary>
        public Tween AnimateShineRay(float duration = 0.50f)
        {
            currentShineProgress = -0.3f;
            if (dynamicMat != null) dynamicMat.SetFloat(PropShineProgress, currentShineProgress);

            return DOTween.To(() => currentShineProgress, x => ShineProgress = x, 1.3f, duration).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    currentShineProgress = -0.5f;
                    if (dynamicMat != null) dynamicMat.SetFloat(PropShineProgress, currentShineProgress);
                });
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
