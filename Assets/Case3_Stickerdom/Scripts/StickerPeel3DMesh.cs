using System;
using UnityEngine;
using DG.Tweening;

namespace Stickerdom
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class StickerPeel3DMesh : MonoBehaviour
    {
        private static readonly float[] CornerAngles = new float[] { 45f, 135f, 225f, 315f };

        [Header("🎚️ LIVE PEEL TEST SLIDER (CANLI TEST KAYDIRICI)")]
        [Tooltip("Sticker'ın sökülme seviyesi (0.0: Masada düz, 1.0: Tamamen sökülmüş). Bu slider'ı sürükleyerek anında canlı test edebilirsiniz!")]
        [Range(0f, 1f)] [SerializeField] private float peelProgress = 0f;

        [Header("🔥 MULTI-STAGE GRADIENT CONTROLS (CANLI AYARLAR)")]
        [Tooltip("1. Zemin temas ayrılma çizgisi rengi (Tam Siyah).")]
        [SerializeField] private Color creaseColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);

        [Tooltip("2. Beyaz tepe parlama rengi (Parlak Beyaz).")]
        [SerializeField] private Color peakColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

        [Tooltip("3. Koyu gri çukur tonu rengi.")]
        [SerializeField] private Color midDipColor = new Color(0.70f, 0.72f, 0.78f, 1.0f);

        [Tooltip("4. Kanat gövde ara degrade tonu rengi.")]
        [SerializeField] private Color endToneColor = Color.white;

        [Tooltip("5. Kanat serbest uç bitiş rengi.")]
        [SerializeField] private Color tipColor = Color.white;

        [Tooltip("1. Zemin temas çizgisi (Tam Siyah) genişliği/konumu (0.00 - 1.00).")]
        [Range(0.00f, 1.00f)] [SerializeField] private float creasePosition = 0.249f;

        [Tooltip("2. Tepe noktasının kanattaki konumu (0.00 - 1.00).")]
        [Range(0.00f, 1.00f)] [SerializeField] private float peakPosition = 0.438f;

        [Tooltip("3. Koyu gri çukurun kanattaki konumu (0.00 - 1.00).")]
        [Range(0.00f, 1.00f)] [SerializeField] private float dipPosition = 0.609f;

        [Tooltip("4. Gövde ara degrade bitiş konumu (0.00 - 1.00).")]
        [Range(0.00f, 1.00f)] [SerializeField] private float tailPosition = 0.70f;

        [Tooltip("Tepe noktasındaki parlama şiddeti.")]
        [Range(0.0f, 3.0f)] [SerializeField] private float apexGlossIntensity = 0.28f;

        [Tooltip("Degrade yönünü tersine çevir (Gerektiğinde tek tıkla ters çevirebilirsiniz).")]
        [SerializeField] private bool invertGradientDirection = false;

        [Header("📐 PEEL MECHANICS & 3D MESH")]
        [Tooltip("Cylinder roll radius (smaller = tighter curl, larger = looser curve).")]
        [Range(0.15f, 0.80f)] [SerializeField] private float rollRadius = 0.20f;

        [Tooltip("Direction angle in degrees from which the corner curls up.")]
        [Range(0f, 360f)] [SerializeField] private float peelAngle = 45.0f;

        [Range(12, 64)] [SerializeField] private int gridResolution = 36;

        [Tooltip("Backside adhesive color tint.")]
        [SerializeField] private Color backSideColor = Color.white;

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
        private static readonly int PropCreaseColor = Shader.PropertyToID("_CreaseColor");
        private static readonly int PropPeakColor = Shader.PropertyToID("_PeakColor");
        private static readonly int PropMidDipColor = Shader.PropertyToID("_MidDipColor");
        private static readonly int PropEndToneColor = Shader.PropertyToID("_EndToneColor");
        private static readonly int PropTipColor = Shader.PropertyToID("_TipColor");
        private static readonly int PropCreasePosition = Shader.PropertyToID("_CreasePosition");
        private static readonly int PropPeakPosition = Shader.PropertyToID("_PeakPosition");
        private static readonly int PropDipPosition = Shader.PropertyToID("_DipPosition");
        private static readonly int PropTailPosition = Shader.PropertyToID("_TailPosition");
        private static readonly int PropApexGlossIntensity = Shader.PropertyToID("_ApexGlossIntensity");
        private static readonly int PropInvertGradient = Shader.PropertyToID("_InvertGradient");

        public float PeelProgress
        {
            get => currentPeelProgress;
            set
            {
                currentPeelProgress = Mathf.Clamp(value, 0f, 1.0f);
                peelProgress = currentPeelProgress;
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

        private void OnValidate()
        {
            currentPeelProgress = peelProgress;
            ApplyMaterialProperties();
            if (isInitialized)
            {
                DeformMesh();
            }
        }

        private void Update()
        {
            // Inspector slider'ları sürüklendiğinde anında materyale yansıt
            ApplyMaterialProperties();
#if UNITY_EDITOR
            if (Mathf.Abs(currentPeelProgress - peelProgress) > 0.0001f)
            {
                currentPeelProgress = peelProgress;
                DeformMesh();
            }
#endif
        }

        public void ApplyMaterialProperties()
        {
            Material targetMat = dynamicMat;
            if (targetMat == null && meshRenderer != null)
            {
                targetMat = Application.isPlaying ? meshRenderer.material : meshRenderer.sharedMaterial;
            }

            if (targetMat != null)
            {
                targetMat.SetColor(PropBackSideColor, backSideColor);
                targetMat.SetColor(PropCreaseColor, creaseColor);
                targetMat.SetColor(PropPeakColor, peakColor);
                targetMat.SetColor(PropMidDipColor, midDipColor);
                targetMat.SetColor(PropEndToneColor, endToneColor);
                targetMat.SetColor(PropTipColor, tipColor);
                targetMat.SetFloat(PropCreasePosition, creasePosition);
                targetMat.SetFloat(PropPeakPosition, peakPosition);
                targetMat.SetFloat(PropDipPosition, dipPosition);
                targetMat.SetFloat(PropTailPosition, tailPosition);
                targetMat.SetFloat(PropApexGlossIntensity, apexGlossIntensity);
                targetMat.SetFloat(PropInvertGradient, invertGradientDirection ? 1f : 0f);
            }
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
            dynamicMat.SetFloat(PropShineProgress, currentShineProgress);
            ApplyMaterialProperties();

            meshRenderer.material = dynamicMat;
            meshRenderer.sortingOrder = spriteRenderer.sortingOrder;

            // Disable original SpriteRenderer so the 3D curling mesh renders
            spriteRenderer.enabled = false;

            // 2. Subdivide Peel-Aligned Dual-Layer Grid Mesh (Front Sheet + Back Sheet)
            // Izgara çizgileri katlanma açısına (peelAngle) tam paralel hizalanır; testere dişi / karelenme SIFIRA iner!
            int res = gridResolution;
            int singleVerts = (res + 1) * (res + 1);
            int totalVerts = singleVerts * 2;
            int singleTris = res * res * 6;
            int totalTris = singleTris * 2;

            baseVertices = new Vector3[totalVerts];
            workingVertices = new Vector3[totalVerts];
            workingColors = new Color[totalVerts];
            baseUVs = new Vector2[totalVerts];
            baseTriangles = new int[totalTris];

            Vector4 uvRect = UnityEngine.Sprites.DataUtility.GetInnerUV(sprite);
            float minU = uvRect.x;
            float minV = uvRect.y;
            float maxU = uvRect.z;
            float maxV = uvRect.w;

            float rad = peelAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 perp = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));

            // 4 corners projection along peel direction
            float p00 = Vector2.Dot(new Vector2(-halfW, -halfH), dir);
            float p10 = Vector2.Dot(new Vector2(halfW, -halfH), dir);
            float p01 = Vector2.Dot(new Vector2(-halfW, halfH), dir);
            float p11 = Vector2.Dot(new Vector2(halfW, halfH), dir);

            float dMin = Mathf.Min(Mathf.Min(p00, p10), Mathf.Min(p01, p11));
            float dMax = Mathf.Max(Mathf.Max(p00, p10), Mathf.Max(p01, p11));

            // 4 corners projection along perpendicular direction
            float q00 = Vector2.Dot(new Vector2(-halfW, -halfH), perp);
            float q10 = Vector2.Dot(new Vector2(halfW, -halfH), perp);
            float q01 = Vector2.Dot(new Vector2(-halfW, halfH), perp);
            float q11 = Vector2.Dot(new Vector2(halfW, halfH), perp);

            float perpMin = Mathf.Min(Mathf.Min(q00, q10), Mathf.Min(q01, q11));
            float perpMax = Mathf.Max(Mathf.Max(q00, q10), Mathf.Max(q01, q11));

            int vertIdx = 0;
            for (int y = 0; y <= res; y++)
            {
                // y ekseni: peel direction (dir) boyunca ilerler (dMin -> dMax)
                float normD = (float)y / res;
                float dVal = Mathf.Lerp(dMin, dMax, normD);

                for (int x = 0; x <= res; x++)
                {
                    // x ekseni: katlanma çizgisine (perp) paralel uzanır (perpMin -> perpMax)
                    float normP = (float)x / res;
                    float pVal = Mathf.Lerp(perpMin, perpMax, normP);

                    Vector2 local2D = dir * dVal + perp * pVal;
                    Vector3 pos = new Vector3(local2D.x, local2D.y, 0f);

                    float normX = (local2D.x + halfW) / (halfW * 2.0f);
                    float normY = (local2D.y + halfH) / (halfH * 2.0f);
                    float uvX = Mathf.Lerp(minU, maxU, normX);
                    float uvY = Mathf.Lerp(minV, maxV, normY);
                    Vector2 uv = new Vector2(uvX, uvY);

                    // 1. FRONT SHEET VERTICES (0 ... singleVerts-1)
                    baseVertices[vertIdx] = pos;
                    workingVertices[vertIdx] = pos;
                    workingColors[vertIdx] = new Color(0f, 1f, 0f, 1f); // g=1.0, a=1.0
                    baseUVs[vertIdx] = uv;

                    // 2. BACK SHEET VERTICES (singleVerts ... totalVerts-1)
                    baseVertices[vertIdx + singleVerts] = pos;
                    workingVertices[vertIdx + singleVerts] = pos;
                    workingColors[vertIdx + singleVerts] = new Color(0f, 0f, 0f, 0f); // a=0.0 (Invisible on table)
                    baseUVs[vertIdx + singleVerts] = uv;

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

                    // 1. FRONT SHEET TRIANGLES (Clockwise)
                    baseTriangles[triIdx++] = i0;
                    baseTriangles[triIdx++] = i2;
                    baseTriangles[triIdx++] = i1;

                    baseTriangles[triIdx++] = i1;
                    baseTriangles[triIdx++] = i2;
                    baseTriangles[triIdx++] = i3;

                    // 2. BACK SHEET TRIANGLES (Counter-Clockwise)
                    int b0 = i0 + singleVerts;
                    int b1 = i1 + singleVerts;
                    int b2 = i2 + singleVerts;
                    int b3 = i3 + singleVerts;

                    baseTriangles[triIdx++] = b0;
                    baseTriangles[triIdx++] = b1;
                    baseTriangles[triIdx++] = b2;

                    baseTriangles[triIdx++] = b1;
                    baseTriangles[triIdx++] = b3;
                    baseTriangles[triIdx++] = b2;
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

            float dynamicR = Mathf.Max(rollRadius * Mathf.Lerp(0.72f, 1.18f, Mathf.Sqrt(currentPeelProgress)), 0.12f);
            float R = dynamicR;
            float tCrease = currentPeelProgress;
            float maxCurledDist = tCrease * span;

            int singleVerts = (gridResolution + 1) * (gridResolution + 1);

            for (int i = 0; i < singleVerts; i++)
            {
                Vector3 basePos = baseVertices[i];
                float proj = Vector2.Dot(new Vector2(basePos.x, basePos.y), dir);
                // t: 0 at peel start corner, 1 at opposite corner
                float t = (pMax - proj) / span;

                // Distance past the fold line into the curled flap
                float curlDist = (tCrease - t) * span;

                if (curlDist <= 0f)
                {
                    // 1. MASADA DÜZ KALAN KISIM (Unpeeled on table):
                    // ÖN YÜZ: basePos'ta, g=1, a=1 (%100 Saf Orijinal Görsel, SIFIR gölge)
                    workingVertices[i] = basePos;
                    workingColors[i] = new Color(0f, 1f, 0f, 1f);

                    // ARKA YÜZ: a=0, Z=1.0 (MASADA KESİNLİKLE YOK / TAMAMEN GÖRÜNMEZ)
                    workingVertices[i + singleVerts] = new Vector3(basePos.x, basePos.y, 1.0f);
                    workingColors[i + singleVerts] = new Color(0f, 0f, 0f, 0f);
                }
                else
                {
                    // 2. HAVAYA KALKAN SÖKÜLMÜŞ KISIM (Curled in the air):
                    float alpha = curlDist / R;
                    Vector3 curledPos;

                    if (alpha <= Mathf.PI)
                    {
                        float deltaD = -(curlDist - R * Mathf.Sin(alpha));
                        float zOffset = -R * (1f - Mathf.Cos(alpha));
                        curledPos = new Vector3(basePos.x + deltaD * dir.x, basePos.y + deltaD * dir.y, zOffset);
                    }
                    else
                    {
                        float deltaD = -(2f * curlDist - Mathf.PI * R);
                        float zOffset = -2f * R;
                        curledPos = new Vector3(basePos.x + deltaD * dir.x, basePos.y + deltaD * dir.y, zOffset);
                    }

                    // ÖN YÜZ: Kıvrımın iç tarafında
                    workingVertices[i] = curledPos;
                    workingColors[i] = new Color(0f, 1f, 0f, 1f);

                    // ARKA YÜZ: Kameraya bakan dış yapışkan yüzey (Z slightly in front by 0.002 to avoid z-fighting)
                    workingVertices[i + singleVerts] = new Vector3(curledPos.x, curledPos.y, curledPos.z - 0.002f);

                    // u Koordinatı (Siyah -> Net Beyaz -> Koyu Gri -> Beyaza Geniş Degrade):
                    // 1. alpha: 0 -> 0.50*PI (Zemin temas kenarından tepeye: TAM SİYAH -> NET BEYAZ TEPE)
                    // 2. alpha: 0.50*PI -> PI (Tepe sırtından iniş: NET BEYAZ -> KOYU GRİ)
                    // 3. alpha > PI (Kanat gövdesi: KOYU GRİDEN BEYAZA GENİŞ GRADIENT)
                    float u;
                    if (alpha <= 0.50f * Mathf.PI)
                    {
                        u = Mathf.Lerp(0.0f, 0.20f, alpha / (0.50f * Mathf.PI));
                    }
                    else if (alpha <= Mathf.PI)
                    {
                        u = Mathf.Lerp(0.20f, 0.40f, (alpha - 0.50f * Mathf.PI) / (0.50f * Mathf.PI));
                    }
                    else
                    {
                        float flapLen = Mathf.Max(maxCurledDist - Mathf.PI * R, 0.001f);
                        float flapDist = curlDist - Mathf.PI * R;
                        u = Mathf.Lerp(0.40f, 1.0f, Mathf.Clamp01(flapDist / flapLen));
                    }

                    // Tepe noktasındaki keskin silindirik parlama
                    float apexHighlight = Mathf.Pow(Mathf.Sin(Mathf.Clamp01(alpha / Mathf.PI) * Mathf.PI), 2.5f);

                    workingColors[i + singleVerts] = new Color(u, 0f, apexHighlight, 1f);
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
            if (Mathf.Abs(peelAngle - angle) > 0.01f)
            {
                peelAngle = angle;
                if (isInitialized)
                {
                    BuildMesh();
                }
            }
            DeformMesh();
        }

        /// <summary>
        /// Boşta beklerken köşe göz kırpması / çağırma efekti (Idle corner tease / wink).
        /// </summary>
        public Tween AnimateCornerTease(float targetProgress, float duration = 0.30f)
        {
            return DOTween.To(() => currentPeelProgress, x => PeelProgress = x, targetProgress, duration).SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// 3D SÖKÜLME: 0 -> 1 (Juicy, akıcı ve anında tepki veren Ease.OutCubic eğrisi).
        /// </summary>
        public Tween AnimatePeelOff(float duration = 0.40f)
        {
            return DOTween.To(() => currentPeelProgress, x => PeelProgress = x, 1.0f, duration).SetEase(Ease.OutCubic);
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
