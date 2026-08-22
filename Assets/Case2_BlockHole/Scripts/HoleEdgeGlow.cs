using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockHole
{
    public class HoleEdgeGlow : MonoBehaviour
    {
        [Header("Glow Appearance")]
        [SerializeField] private Color glowColor = Color.cyan;
        [SerializeField] private float heightOffset = 0.045f;

        [Header("Per-Meter Uniform Density Settings")]
        [Tooltip("Number of rim outline particles per meter length of perimeter per second.")]
        [SerializeField] private float rimParticlesPerMeter = 90f;

        [Tooltip("Number of downward waterfall cascade particles per meter length of perimeter per second.")]
        [SerializeField] private float cascadeParticlesPerMeter = 75f;

        [Tooltip("Base size of the glowing rim outline particles.")]
        [SerializeField] private float rimParticleBaseSize = 0.22f;

        [Tooltip("Base size of the downward waterfall cascade particles.")]
        [SerializeField] private float cascadeParticleBaseSize = 0.16f;

        [Tooltip("Downward plunging speed of the waterfall stream into the hole.")]
        [SerializeField] private float downwardFlowSpeed = 2.2f;

        [Tooltip("Inward suction pulling speed towards the hole center.")]
        [SerializeField] private float inwardSuctionSpeed = 0.45f;

        private ParticleSystem rimParticleSystem;
        private ParticleSystem cascadeParticleSystem;
        private Material softParticleMaterial;
        private static Texture2D softGlowTexture;
        private bool isGlowing = false;
        private Vector3[] cachedWorldPoints;
        private Vector3 holeCenterWorldPos;
        private float totalPerimeterLength = 4.0f;
        private float rimEmitTimer = 0f;
        private float cascadeEmitTimer = 0f;

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
            get => null;
            set { }
        }

        private void Awake()
        {
            // Remove any LineRenderer
            LineRenderer lr = GetComponent<LineRenderer>();
            if (lr != null)
            {
                if (Application.isPlaying) Destroy(lr);
                else DestroyImmediate(lr);
            }

            InitializeParticleSystems();
        }

        public void InitializeRenderer()
        {
            LineRenderer lr = GetComponent<LineRenderer>();
            if (lr != null)
            {
                if (Application.isPlaying) Destroy(lr);
                else DestroyImmediate(lr);
            }

            InitializeParticleSystems();
        }

        public void InitializeParticleSystems()
        {
            if (softGlowTexture == null)
            {
                softGlowTexture = CreateGaussianSoftGlowTexture();
            }

            if (softParticleMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                             ?? Shader.Find("Particles/Standard Unlit") 
                             ?? Shader.Find("Mobile/Particles/Additive");

                softParticleMaterial = new Material(shader);
                softParticleMaterial.name = "Mat_GaussianSoftGlow_Instance";
                softParticleMaterial.SetFloat("_Surface", 1f); // Transparent
                softParticleMaterial.SetFloat("_Blend", 1f);   // Additive
                softParticleMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                softParticleMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                softParticleMaterial.SetInt("_ZWrite", 0);
                softParticleMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 150;

                if (softParticleMaterial.HasProperty("_BaseMap"))
                {
                    softParticleMaterial.SetTexture("_BaseMap", softGlowTexture);
                }
                if (softParticleMaterial.HasProperty("_MainTex"))
                {
                    softParticleMaterial.SetTexture("_MainTex", softGlowTexture);
                }
                softParticleMaterial.mainTexture = softGlowTexture;
            }

            // 1. Setup Rim Outline Particle System
            if (rimParticleSystem == null)
            {
                Transform child = transform.Find("RimOutlineParticles");
                GameObject psGo = child != null ? child.gameObject : new GameObject("RimOutlineParticles");
                psGo.transform.SetParent(transform, false);

                rimParticleSystem = psGo.GetComponent<ParticleSystem>();
                if (rimParticleSystem == null)
                {
                    rimParticleSystem = psGo.AddComponent<ParticleSystem>();
                }
                rimParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = rimParticleSystem.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.05f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.04f); // Hovers on the rim
                main.startSize = new ParticleSystem.MinMaxCurve(rimParticleBaseSize * 0.8f, rimParticleBaseSize * 1.3f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
                main.gravityModifier = 0f;

                var emission = rimParticleSystem.emission;
                emission.enabled = false;

                var shape = rimParticleSystem.shape;
                shape.enabled = false;

                var colorOverLifetime = rimParticleSystem.colorOverLifetime;
                colorOverLifetime.enabled = true;

                var sizeOverLifetime = rimParticleSystem.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                AnimationCurve sizeCurve = new AnimationCurve();
                sizeCurve.AddKey(0.0f, 0.35f);
                sizeCurve.AddKey(0.25f, 1.25f);
                sizeCurve.AddKey(0.75f, 0.95f);
                sizeCurve.AddKey(1.0f, 0.05f);
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

                var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
                psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                psRenderer.sharedMaterial = softParticleMaterial;
            }

            // 2. Setup Waterfall Cascade Particle System
            if (cascadeParticleSystem == null)
            {
                Transform child = transform.Find("WaterfallParticles");
                GameObject psGo = child != null ? child.gameObject : new GameObject("WaterfallParticles");
                psGo.transform.SetParent(transform, false);

                cascadeParticleSystem = psGo.GetComponent<ParticleSystem>();
                if (cascadeParticleSystem == null)
                {
                    cascadeParticleSystem = psGo.AddComponent<ParticleSystem>();
                }
                cascadeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = cascadeParticleSystem.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.60f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                main.startSize = new ParticleSystem.MinMaxCurve(cascadeParticleBaseSize * 0.75f, cascadeParticleBaseSize * 1.25f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
                main.gravityModifier = 0.38f; // Accelerates down into the hole depth

                var emission = cascadeParticleSystem.emission;
                emission.enabled = false;

                var shape = cascadeParticleSystem.shape;
                shape.enabled = false;

                var colorOverLifetime = cascadeParticleSystem.colorOverLifetime;
                colorOverLifetime.enabled = true;

                var sizeOverLifetime = cascadeParticleSystem.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                AnimationCurve sizeCurve = new AnimationCurve();
                sizeCurve.AddKey(0.0f, 1.1f);
                sizeCurve.AddKey(0.40f, 1.2f);
                sizeCurve.AddKey(0.75f, 0.60f);
                sizeCurve.AddKey(1.0f, 0.10f);
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

                var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
                psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                psRenderer.sharedMaterial = softParticleMaterial;
            }

            ApplyColor();
        }

        private static Texture2D CreateGaussianSoftGlowTexture()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "Tex_ProceduralGaussianSoftGlow";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDist = size * 0.5f;

            Color[] colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    // Silky smooth Gaussian radial falloff (0 hard edges)
                    float alpha = Mathf.Exp(-dist * dist * 3.8f);
                    if (dist >= 1.0f) alpha = 0.0f;
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private void ApplyColor()
        {
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(glowColor * 1.15f, 0.20f),
                    new GradientColorKey(glowColor * 0.95f, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.78f, 0.20f),
                    new GradientAlphaKey(0.65f, 0.60f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );

            if (rimParticleSystem != null)
            {
                var col = rimParticleSystem.colorOverLifetime;
                col.enabled = true;
                col.color = grad;
            }

            if (cascadeParticleSystem != null)
            {
                var col = cascadeParticleSystem.colorOverLifetime;
                col.enabled = true;
                col.color = grad;
            }
        }

        public void BuildPerimeterFromCells(List<Vector2Int> footprintCells, Vector3 originWorldPos, float tileSize = 1.0f)
        {
            if (footprintCells == null || footprintCells.Count == 0) return;

            InitializeParticleSystems();

            // 1. Collect all boundary edges
            HashSet<Vector2Int> cellSet = new HashSet<Vector2Int>(footprintCells);
            List<Segment> perimeterSegments = new List<Segment>();

            Vector3 centerAccum = Vector3.zero;
            foreach (var cell in footprintCells)
            {
                float minX = cell.x * tileSize;
                float maxX = (cell.x + 1) * tileSize;
                float minZ = cell.y * tileSize;
                float maxZ = (cell.y + 1) * tileSize;

                centerAccum += new Vector3(originWorldPos.x + (minX + maxX) * 0.5f, heightOffset, originWorldPos.z + (minZ + maxZ) * 0.5f);

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

            holeCenterWorldPos = centerAccum / footprintCells.Count;

            // 2. Chain segments into an ordered closed polygon loop
            List<Vector2> orderedPoints = ChainSegments(perimeterSegments);

            // 3. Convert to world positions with height offset
            cachedWorldPoints = new Vector3[orderedPoints.Count];
            totalPerimeterLength = 0f;

            for (int i = 0; i < orderedPoints.Count; i++)
            {
                cachedWorldPoints[i] = new Vector3(
                    originWorldPos.x + orderedPoints[i].x,
                    heightOffset,
                    originWorldPos.z + orderedPoints[i].y
                );
            }

            for (int i = 0; i < cachedWorldPoints.Length; i++)
            {
                Vector3 pA = cachedWorldPoints[i];
                Vector3 pB = cachedWorldPoints[(i + 1) % cachedWorldPoints.Length];
                totalPerimeterLength += Vector3.Distance(pA, pB);
            }
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

            if (active)
            {
                if (rimParticleSystem != null)
                {
                    rimParticleSystem.Play();
                }
                if (cascadeParticleSystem != null)
                {
                    cascadeParticleSystem.Play();
                }

                // Initial burst proportional to perimeter size
                int initialBurst = Mathf.RoundToInt(totalPerimeterLength * 16f);
                EmitRimParticles(initialBurst);
                EmitCascadeParticles(initialBurst / 2);
            }
            else
            {
                if (rimParticleSystem != null)
                {
                    rimParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                if (cascadeParticleSystem != null)
                {
                    cascadeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private void Update()
        {
            if (!isGlowing || cachedWorldPoints == null || cachedWorldPoints.Length < 2) return;

            // 1. Emit Rim Particles with uniform density per meter
            float rimRate = Mathf.Max(rimParticlesPerMeter * totalPerimeterLength, 50f);
            float rimInterval = 1.0f / rimRate;
            rimEmitTimer += Time.deltaTime;

            if (rimEmitTimer >= rimInterval)
            {
                int count = Mathf.FloorToInt(rimEmitTimer / rimInterval);
                rimEmitTimer -= count * rimInterval;
                EmitRimParticles(Mathf.Clamp(count, 1, 35));
            }

            // 2. Emit Waterfall Cascade Particles with uniform density per meter
            float cascadeRate = Mathf.Max(cascadeParticlesPerMeter * totalPerimeterLength, 40f);
            float cascadeInterval = 1.0f / cascadeRate;
            cascadeEmitTimer += Time.deltaTime;

            if (cascadeEmitTimer >= cascadeInterval)
            {
                int count = Mathf.FloorToInt(cascadeEmitTimer / cascadeInterval);
                cascadeEmitTimer -= count * cascadeInterval;
                EmitCascadeParticles(Mathf.Clamp(count, 1, 30));
            }
        }

        /// <summary>
        /// Delik kenar dudaklarında sabit durup parıldayan ve deliğin outline'ını oluşturan partiküller.
        /// </summary>
        private void EmitRimParticles(int count)
        {
            if (cachedWorldPoints == null || cachedWorldPoints.Length < 2 || rimParticleSystem == null) return;

            int numSegments = cachedWorldPoints.Length;

            for (int i = 0; i < count; i++)
            {
                int segIdx = UnityEngine.Random.Range(0, numSegments);
                Vector3 pA = cachedWorldPoints[segIdx];
                Vector3 pB = cachedWorldPoints[(segIdx + 1) % numSegments];

                float t = UnityEngine.Random.value;
                Vector3 spawnPos = Vector3.Lerp(pA, pB, t);
                spawnPos += UnityEngine.Random.insideUnitSphere * 0.012f;
                spawnPos.y = heightOffset + 0.025f;

                Vector3 toCenter = (holeCenterWorldPos - spawnPos);
                toCenter.y = 0f;
                toCenter = toCenter.normalized;

                // Kenarda sabit asılı kalan nazik titreşim
                Vector3 velocity = (toCenter * 0.04f) + (UnityEngine.Random.insideUnitSphere * 0.02f);

                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = spawnPos,
                    velocity = velocity,
                    startColor = glowColor * 1.15f,
                    startSize = UnityEngine.Random.Range(rimParticleBaseSize * 0.8f, rimParticleBaseSize * 1.25f),
                    startLifetime = UnityEngine.Random.Range(0.65f, 1.05f)
                };

                rimParticleSystem.Emit(emitParams, 1);
            }
        }

        /// <summary>
        /// Delik kenarlarından deliğin dibine doğru şelale gibi dökülen akıcı partiküller.
        /// </summary>
        private void EmitCascadeParticles(int count)
        {
            if (cachedWorldPoints == null || cachedWorldPoints.Length < 2 || cascadeParticleSystem == null) return;

            int numSegments = cachedWorldPoints.Length;

            for (int i = 0; i < count; i++)
            {
                int segIdx = UnityEngine.Random.Range(0, numSegments);
                Vector3 pA = cachedWorldPoints[segIdx];
                Vector3 pB = cachedWorldPoints[(segIdx + 1) % numSegments];

                float t = UnityEngine.Random.value;
                Vector3 spawnPos = Vector3.Lerp(pA, pB, t);
                spawnPos += UnityEngine.Random.insideUnitSphere * 0.015f;
                spawnPos.y = heightOffset + 0.02f;

                Vector3 toCenter = (holeCenterWorldPos - spawnPos);
                toCenter.y = 0f;
                toCenter = toCenter.normalized;

                // Doğrudan aşağıya ve içeriye doğru güçlü şelale süzülmesi
                Vector3 velocity = (Vector3.down * UnityEngine.Random.Range(downwardFlowSpeed * 0.8f, downwardFlowSpeed * 1.25f))
                                 + (toCenter * inwardSuctionSpeed)
                                 + (UnityEngine.Random.insideUnitSphere * 0.035f);

                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = spawnPos,
                    velocity = velocity,
                    startColor = glowColor * 1.15f,
                    startSize = UnityEngine.Random.Range(cascadeParticleBaseSize * 0.75f, cascadeParticleBaseSize * 1.25f),
                    startLifetime = UnityEngine.Random.Range(0.35f, 0.60f)
                };

                cascadeParticleSystem.Emit(emitParams, 1);
            }
        }

        private void OnDestroy()
        {
            if (softParticleMaterial != null)
            {
                if (Application.isPlaying) Destroy(softParticleMaterial);
                else DestroyImmediate(softParticleMaterial);
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
