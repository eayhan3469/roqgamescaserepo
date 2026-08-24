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

        [Tooltip("1. Zemin temas çizgisi (Tam Siyah) sabit fiziksel genişliği (0.00 - 2.00).")]
        [Range(0.00f, 2.00f)] [SerializeField] private float creasePosition = 0.4f;

        [Tooltip("2. Tepe beyaz parlama noktasının katlanma çizgisine sabit uzaklığı (0.00 - 2.00).")]
        [Range(0.00f, 2.00f)] [SerializeField] private float peakPosition = 0.7f;

        [Tooltip("3. Koyu gri çukur noktasının katlanma çizgisine sabit uzaklığı (0.00 - 2.00).")]
        [Range(0.00f, 2.00f)] [SerializeField] private float dipPosition = 0.989f;

        [Tooltip("4. Beyaz gövdeye geçiş bitiş noktasının sabit uzaklığı (0.00 - 2.00).")]
        [Range(0.00f, 2.00f)] [SerializeField] private float tailPosition = 1.253f;

        [Tooltip("Tepe noktasındaki parlama şiddeti.")]
        [Range(0.0f, 3.0f)] [SerializeField] private float apexGlossIntensity = 0.05f;

        [Tooltip("Degrade yönünü tersine çevir (Gerektiğinde tek tıkla ters çevirebilirsiniz).")]
        [SerializeField] private bool invertGradientDirection = false;

        [Header("🌑 GROUND & CONTACT AMBIENT OCCLUSION (ZEMİN VE TEMAS GÖLGESİ)")]
        [Tooltip("Zemine/masaya düşen temas gölgesinin açılan kısma doğru genişliği (0.00 - 2.00).")]
        [Range(0.00f, 2.00f)] [SerializeField] private float groundAOWidth = 0.60f;

        [Tooltip("Zemin gölgesinin koyuluk/yoğunluk şiddeti (0.00: Gölge yok, 1.00: Tam koyu).")]
        [Range(0.00f, 1.00f)] [SerializeField] private float groundAOStrength = 0.70f;

        [Tooltip("Zemin gölgesinin renk tonu.")]
        [SerializeField] private Color groundAOColor = new Color(0.0f, 0.0f, 0.0f, 0.75f);

        [Tooltip("Kıvrımın masadaki düz kısma vurduğu temas gölgesinin genişliği (0.00 - 1.00).")]
        [Range(0.00f, 1.00f)] [SerializeField] private float underFoldAOWidth = 0.25f;

        [Tooltip("Temas gölgesinin koyuluk/yoğunluk şiddeti (0.00: Gölge yok, 1.00: Tam koyu).")]
        [Range(0.00f, 1.00f)] [SerializeField] private float underFoldAOStrength = 0.50f;

        [Tooltip("Temas gölgesinin renk tonu.")]
        [SerializeField] private Color underFoldAOColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);

        [Header("📐 PEEL MECHANICS & 3D MESH")]
        [Tooltip("Cylinder roll radius (smaller = tighter curl, larger = looser curve).")]
        [Range(0.15f, 0.80f)] [SerializeField] private float rollRadius = 0.20f;

        [Tooltip("Direction angle in degrees from which the corner curls up.")]
        [Range(0f, 360f)] [SerializeField] private float peelAngle = 184.0f;

        [Range(12, 64)] [SerializeField] private int gridResolution = 36;

        [Tooltip("Backside adhesive color tint.")]
        [SerializeField] private Color backSideColor = Color.white;

        private SpriteRenderer spriteRenderer;
        private GameObject meshHolder;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh deformedMesh;

        private GameObject shadowHolder;
        private MeshFilter shadowFilter;
        private MeshRenderer shadowRenderer;
        private Mesh shadowMesh;
        private Color[] shadowColors;
        private Material shadowMat;

        private Vector3[] baseVertices;
        private Vector3[] workingVertices;
        private Color[] workingColors;
        private Vector2[] baseUVs;
        private int[] baseTriangles;

        private float currentPeelProgress = 0f;
        private float currentShineProgress = -0.5f;
        private float currentShadowOpacity = 1.0f;
        private bool isPeelingOff = false;
        private float lastSparkleTime = 0f;
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
        private static readonly int PropUnderFoldAOWidth = Shader.PropertyToID("_UnderFoldAOWidth");
        private static readonly int PropUnderFoldAOStrength = Shader.PropertyToID("_UnderFoldAOStrength");
        private static readonly int PropUnderFoldAOColor = Shader.PropertyToID("_UnderFoldAOColor");

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

        private void Reset()
        {
            ResetToUserDefaults();
        }

        [ContextMenu("🔥 Ayarları Varsayılana Sıfırla (Reset To Dialed Defaults)")]
        public void ResetToUserDefaults()
        {
            creasePosition = 0.4f;
            peakPosition = 0.7f;
            dipPosition = 0.989f;
            tailPosition = 1.253f;
            apexGlossIntensity = 0.05f;
            rollRadius = 0.20f;
            peelAngle = 184f;
            gridResolution = 36;
            creaseColor = Color.black;
            peakColor = Color.white;
            midDipColor = new Color(0.70f, 0.72f, 0.78f, 1.0f);
            endToneColor = Color.white;
            tipColor = Color.white;
            backSideColor = Color.white;
            invertGradientDirection = false;

            ApplyMaterialProperties();
            if (isInitialized)
            {
                RebuildGridMesh();
                DeformMesh();
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
                RebuildGridMesh();
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
                targetMat.SetFloat(PropUnderFoldAOWidth, underFoldAOWidth);
                targetMat.SetFloat(PropUnderFoldAOStrength, underFoldAOStrength);
                targetMat.SetColor(PropUnderFoldAOColor, underFoldAOColor);
            }

            if (shadowMat != null)
            {
                shadowMat.SetColor(PropColor, groundAOColor);
            }
        }

        private void BuildMesh()
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null) return;

            if (meshHolder == null)
            {
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
                dynamicMat.SetTexture(PropMainTex, spriteRenderer.sprite.texture);
                if (dynamicMat.HasProperty("_BaseMap")) dynamicMat.SetTexture("_BaseMap", spriteRenderer.sprite.texture);
                dynamicMat.mainTexture = spriteRenderer.sprite.texture;
                dynamicMat.SetColor(PropColor, spriteRenderer.color);
                dynamicMat.SetFloat(PropShineProgress, currentShineProgress);
                ApplyMaterialProperties();

                meshRenderer.material = dynamicMat;
                meshRenderer.sortingOrder = spriteRenderer.sortingOrder;

                // 2. Create Child GameObject for Ground AO Shadow Mesh on Table
                shadowHolder = new GameObject($"{gameObject.name}_GroundShadow");
                shadowHolder.transform.SetParent(transform, false);
                shadowHolder.transform.localPosition = new Vector3(0, 0, 0.002f);
                shadowHolder.transform.localRotation = Quaternion.identity;
                shadowHolder.transform.localScale = Vector3.one;

                shadowFilter = shadowHolder.AddComponent<MeshFilter>();
                shadowRenderer = shadowHolder.AddComponent<MeshRenderer>();

                shadowMesh = new Mesh { name = $"{gameObject.name}_GroundShadowMesh" };
                shadowFilter.mesh = shadowMesh;

                Shader shadowShader = Shader.Find("Custom/StickerGroundShadow") ?? Shader.Find("Sprites/Default");
                shadowMat = new Material(shadowShader);
                shadowMat.name = $"Mat_{gameObject.name}_GroundShadow";
                if (spriteRenderer != null && spriteRenderer.sprite != null)
                {
                    shadowMat.SetTexture(PropMainTex, spriteRenderer.sprite.texture);
                }
                shadowMat.SetColor(PropColor, groundAOColor);
                shadowRenderer.material = shadowMat;
                if (spriteRenderer != null)
                {
                    shadowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                    shadowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
                }

                // Disable original SpriteRenderer so the 3D curling mesh renders
                spriteRenderer.enabled = false;
            }

            RebuildGridMesh();
            isInitialized = true;
        }

        public void RebuildGridMesh()
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null) return;

            Sprite sprite = spriteRenderer.sprite;
            Vector2 spriteSize = sprite.rect.size / sprite.pixelsPerUnit;
            float halfW = spriteSize.x * 0.5f;
            float halfH = spriteSize.y * 0.5f;

            int res = gridResolution;
            int singleVerts = (res + 1) * (res + 1);
            int totalVerts = singleVerts * 2;
            int singleTris = res * res * 6;
            int totalTris = singleTris * 2;

            if (baseVertices == null || baseVertices.Length != totalVerts)
            {
                baseVertices = new Vector3[totalVerts];
                workingVertices = new Vector3[totalVerts];
                workingColors = new Color[totalVerts];
                baseUVs = new Vector2[totalVerts];
                baseTriangles = new int[totalTris];
            }

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
                float normD = (float)y / res;
                float dVal = Mathf.Lerp(dMin, dMax, normD);

                for (int x = 0; x <= res; x++)
                {
                    float normP = (float)x / res;
                    float pVal = Mathf.Lerp(perpMin, perpMax, normP);

                    Vector2 local2D = dir * dVal + perp * pVal;
                    Vector3 pos = new Vector3(local2D.x, local2D.y, 0f);

                    float normX = (local2D.x + halfW) / (halfW * 2.0f);
                    float normY = (local2D.y + halfH) / (halfH * 2.0f);
                    float uvX = Mathf.Lerp(minU, maxU, normX);
                    float uvY = Mathf.Lerp(minV, maxV, normY);
                    Vector2 uv = new Vector2(uvX, uvY);

                    baseVertices[vertIdx] = pos;
                    workingVertices[vertIdx] = pos;
                    workingColors[vertIdx] = new Color(0f, 1f, 0f, 1f);
                    baseUVs[vertIdx] = uv;

                    baseVertices[vertIdx + singleVerts] = pos;
                    workingVertices[vertIdx + singleVerts] = pos;
                    workingColors[vertIdx + singleVerts] = new Color(0f, 0f, 0f, 0f);
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

                    baseTriangles[triIdx++] = i0;
                    baseTriangles[triIdx++] = i2;
                    baseTriangles[triIdx++] = i1;

                    baseTriangles[triIdx++] = i1;
                    baseTriangles[triIdx++] = i2;
                    baseTriangles[triIdx++] = i3;

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

            if (deformedMesh == null)
            {
                deformedMesh = new Mesh();
                deformedMesh.name = $"{gameObject.name}_DeformedMesh";
            }

            deformedMesh.Clear();
            deformedMesh.vertices = workingVertices;
            deformedMesh.colors = workingColors;
            deformedMesh.uv = baseUVs;
            deformedMesh.triangles = baseTriangles;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateBounds();

            if (meshFilter != null)
            {
                meshFilter.mesh = deformedMesh;
            }

            if (shadowMesh == null && shadowHolder != null)
            {
                shadowMesh = new Mesh();
                shadowMesh.name = $"{gameObject.name}_GroundShadowMesh";
                if (shadowFilter != null) shadowFilter.mesh = shadowMesh;
            }

            shadowColors = new Color[singleVerts];
            if (shadowMesh != null)
            {
                Vector3[] shadowVerts = new Vector3[singleVerts];
                Vector2[] shadowUVs = new Vector2[singleVerts];
                int[] shadowTris = new int[singleTris];

                for (int i = 0; i < singleVerts; i++)
                {
                    shadowVerts[i] = baseVertices[i];
                    shadowUVs[i] = baseUVs[i];
                    shadowColors[i] = Color.clear;
                }

                int sTriIdx = 0;
                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        int i0 = y * (res + 1) + x;
                        int i1 = i0 + 1;
                        int i2 = (y + 1) * (res + 1) + x;
                        int i3 = i2 + 1;

                        shadowTris[sTriIdx++] = i0;
                        shadowTris[sTriIdx++] = i2;
                        shadowTris[sTriIdx++] = i1;

                        shadowTris[sTriIdx++] = i1;
                        shadowTris[sTriIdx++] = i2;
                        shadowTris[sTriIdx++] = i3;
                    }
                }

                shadowMesh.Clear();
                shadowMesh.vertices = shadowVerts;
                shadowMesh.uv = shadowUVs;
                shadowMesh.triangles = shadowTris;
                shadowMesh.colors = shadowColors;
                shadowMesh.RecalculateBounds();
            }
        }

        public void UpdateSortingOrder(int order)
        {
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = order;
            }
            if (shadowRenderer != null)
            {
                shadowRenderer.sortingOrder = order - 1;
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

            float R = Mathf.Max(rollRadius, 0.05f);
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
                    // Zemin Temas Ambient Occlusion Gölgesi (Kıvrımın masadaki düz kısma vurduğu temas gölgesi)
                    float ao = 0f;
                    if (underFoldAOWidth > 0.001f && -curlDist < underFoldAOWidth && currentPeelProgress > 0.001f)
                    {
                        float tAO = 1.0f - (-curlDist / underFoldAOWidth);
                        ao = Mathf.Pow(Mathf.Clamp01(tAO), 1.6f) * currentShadowOpacity;
                    }

                    // ÖN YÜZ: basePos'ta, r=ao (Contact AO), g=1 (Front Sheet)
                    workingVertices[i] = basePos;
                    workingColors[i] = new Color(ao, 1f, 0f, 1f);

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

                    // u Koordinatı: Katlanma çizgisinden olan net fiziksel uzaklık (Söküldükçe ASLA kaymaz, sabit kalır!)
                    float u = curlDist;

                    // Tepe noktasındaki keskin silindirik parlama
                    float apexHighlight = Mathf.Pow(Mathf.Sin(Mathf.Clamp01(alpha / Mathf.PI) * Mathf.PI), 2.5f);

                    workingColors[i + singleVerts] = new Color(u, 0f, apexHighlight, 1f);
                }

                // ZEMİNE DÜŞEN AMBIENT OCCLUSION GÖLGESİ (Ground Shadow):
                float shadowAlpha = 0f;
                if (currentPeelProgress > 0.001f && groundAOWidth > 0.001f && currentShadowOpacity > 0.001f)
                {
                    if (curlDist >= 0f && curlDist <= groundAOWidth)
                    {
                        // Sökülen ve havaya kalkan kanadın masaya vuran gölgesi
                        float tNorm = 1.0f - (curlDist / groundAOWidth);
                        shadowAlpha = Mathf.Pow(Mathf.Clamp01(tNorm), 1.3f) * groundAOStrength * currentShadowOpacity;
                    }
                    else if (curlDist < 0f && -curlDist <= groundAOWidth * 0.35f)
                    {
                        // Katlanma dikişinin zemin temas gölgesi
                        float tNorm = 1.0f - (-curlDist / (groundAOWidth * 0.35f));
                        shadowAlpha = Mathf.Pow(Mathf.Clamp01(tNorm), 1.6f) * groundAOStrength * currentShadowOpacity;
                    }
                }
                if (shadowColors != null && i < shadowColors.Length)
                {
                    shadowColors[i] = new Color(groundAOColor.r, groundAOColor.g, groundAOColor.b, shadowAlpha * groundAOColor.a);
                }
            }

            deformedMesh.vertices = workingVertices;
            deformedMesh.colors = workingColors;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateBounds();

            if (shadowMesh != null && shadowColors != null)
            {
                shadowMesh.colors = shadowColors;
            }

            if (isPeelingOff && currentPeelProgress > 0.05f && currentPeelProgress < 0.98f)
            {
                if (Time.time - lastSparkleTime > 0.022f)
                {
                    lastSparkleTime = Time.time;
                    EmitSparklesAlongFoldLine();
                }
            }
        }

        private void EmitSparklesAlongFoldLine()
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null) return;
            if (StickerVFXManager.Instance == null) return;

            Vector2 spriteSize = spriteRenderer.sprite.rect.size / spriteRenderer.sprite.pixelsPerUnit;
            float hw = spriteSize.x * 0.5f;
            float hh = spriteSize.y * 0.5f;

            float rad = peelAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 perp = new Vector2(-dir.y, dir.x);

            float p00 = Vector2.Dot(new Vector2(-hw, -hh), dir);
            float p10 = Vector2.Dot(new Vector2(hw, -hh), dir);
            float p01 = Vector2.Dot(new Vector2(-hw, hh), dir);
            float p11 = Vector2.Dot(new Vector2(hw, hh), dir);

            float pMax = Mathf.Max(Mathf.Max(p00, p10), Mathf.Max(p01, p11));
            float pMin = Mathf.Min(Mathf.Min(p00, p10), Mathf.Min(p01, p11));
            float span = Mathf.Max(pMax - pMin, 0.001f);

            float foldProj = pMax - currentPeelProgress * span;
            Vector2 centerOnFold = foldProj * dir;

            for (int s = 0; s < 2; s++)
            {
                float w = UnityEngine.Random.Range(-span * 0.45f, span * 0.45f);
                Vector2 pt = centerOnFold + perp * w;

                if (pt.x >= -hw && pt.x <= hw && pt.y >= -hh && pt.y <= hh)
                {
                    Vector3 worldPos = transform.TransformPoint(new Vector3(pt.x, pt.y, 0.005f));
                    StickerVFXManager.Instance.EmitPeelSparkle(worldPos);
                }
            }
        }

        public float PeelAngle => peelAngle;

        public void SetShadowOpacity(float opacity)
        {
            currentShadowOpacity = Mathf.Clamp01(opacity);
            DeformMesh();
        }

        /// <summary>
        /// Overall opacity of the curled mesh's main tint color (independent of the disabled
        /// source SpriteRenderer, which this component's own mesh/material replace once built —
        /// tweening SpriteRenderer.color after BuildMesh() has no visual effect). Used by
        /// StickerLevelManager to fade the purple package's torn-off top flap out once it has
        /// visibly peeled open, so it dissolves away instead of popping out instantly.
        /// </summary>
        public void SetAlpha(float alpha)
        {
            if (dynamicMat == null) return;
            Color c = dynamicMat.GetColor(PropColor);
            c.a = Mathf.Clamp01(alpha);
            dynamicMat.SetColor(PropColor, c);
        }

        public float PickRandomCornerAngle()
        {
            float chosen = CornerAngles[UnityEngine.Random.Range(0, CornerAngles.Length)];
            SetPeelAngle(chosen);
            return chosen;
        }

        /// <summary>
        /// Same as <see cref="PickRandomCornerAngle()"/> but picks only from a restricted subset
        /// of corners — used by StickerClickable to keep stickers near the edge of the screen
        /// (leftmost/rightmost in the waiting row) from picking a corner whose curl bulges
        /// outward past the fold line toward the screen edge and off-screen. Falls back to the
        /// full default corner set if <paramref name="allowedAngles"/> is null or empty.
        /// </summary>
        public float PickRandomCornerAngle(float[] allowedAngles)
        {
            float[] pool = (allowedAngles != null && allowedAngles.Length > 0) ? allowedAngles : CornerAngles;
            float chosen = pool[UnityEngine.Random.Range(0, pool.Length)];
            SetPeelAngle(chosen);
            return chosen;
        }

        public void SetPeelAngle(float angle)
        {
            peelAngle = angle;
            if (isInitialized)
            {
                RebuildGridMesh();
            }
            DeformMesh();
        }

        /// <summary>
        /// Sökülmenin başladığı köşenin gerçek dünya pozisyonunu döndürür.
        /// </summary>
        public Vector3 GetPeelCornerWorldPosition()
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null) return transform.position;

            Vector2 spriteSize = spriteRenderer.sprite.rect.size / spriteRenderer.sprite.pixelsPerUnit;
            float hw = spriteSize.x * 0.5f;
            float hh = spriteSize.y * 0.5f;

            float rad = peelAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // Sökülmenin başladığı köşe (projeksiyonun maksimum olduğu köşe)
            Vector2[] corners = new Vector2[]
            {
                new Vector2(-hw, -hh),
                new Vector2(hw, -hh),
                new Vector2(-hw, hh),
                new Vector2(hw, hh)
            };

            Vector2 bestCorner = corners[0];
            float maxProj = Vector2.Dot(bestCorner, dir);
            for (int i = 1; i < corners.Length; i++)
            {
                float p = Vector2.Dot(corners[i], dir);
                if (p > maxProj)
                {
                    maxProj = p;
                    bestCorner = corners[i];
                }
            }

            return transform.TransformPoint(bestCorner);
        }

        /// <summary>
        /// Boşta beklerken köşe göz kırpması / çağırma efekti (Idle corner tease / wink).
        /// </summary>
        public Tween AnimateCornerTease(float targetProgress, float duration = 0.30f)
        {
            currentShadowOpacity = 1f;
            return DOTween.To(() => currentPeelProgress, x => PeelProgress = x, targetProgress, duration).SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// 3D SÖKÜLME: 0 -> 1 (Juicy, akıcı ve pürüzsüz Ease.OutQuad eğrisi).
        /// </summary>
        public Tween AnimatePeelOff(float duration = 0.40f)
        {
            isPeelingOff = true;
            currentShadowOpacity = 1f;
            PeelProgress = 0f;
            lastSparkleTime = 0f;
            return DOTween.To(() => currentPeelProgress, x => PeelProgress = x, 1.0f, duration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => isPeelingOff = false);
        }

        /// <summary>
        /// 3D GERİ YAPIŞTIRMA (UNROLL): 1 -> 0 (Ultra-smooth Ease.InOutSine ile kadifemsi sayfaya açılma).
        /// </summary>
        public Tween AnimateReverseUnroll(float duration = 0.42f)
        {
            currentShadowOpacity = 1f;
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
            isPeelingOff = false;
            PeelProgress = 0f;
            currentShineProgress = -0.5f;
            if (dynamicMat != null) dynamicMat.SetFloat(PropShineProgress, currentShineProgress);
        }

        private void OnDestroy()
        {
            if (deformedMesh != null) Destroy(deformedMesh);
            if (dynamicMat != null) Destroy(dynamicMat);
            if (shadowMesh != null) Destroy(shadowMesh);
            if (shadowMat != null) Destroy(shadowMat);
        }
    }
}
