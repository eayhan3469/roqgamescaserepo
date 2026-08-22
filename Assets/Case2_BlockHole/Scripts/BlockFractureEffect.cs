using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace BlockHole
{
    public class BlockFractureEffect : MonoBehaviour
    {
        [Header("Fracture Configuration")]
        [SerializeField] private GameObject fracturedPrefab;

        [Header("Varied Chunk Sizes (İrili Ufaklı Parça Boyutları)")]
        [SerializeField] private float largeChunkSize = 0.55f;
        [SerializeField] private float mediumChunkSize = 0.36f;
        [SerializeField] private float smallChipSize = 0.20f;

        [Header("Tactile Pop & Hop (Videodaki Gibi Doğal Zıplama)")]
        [Tooltip("Upward pop height above the hole mouth.")]
        [SerializeField] private float minPopHeight = 0.35f;
        [SerializeField] private float maxPopHeight = 0.65f;

        [Tooltip("Duration of the upward pop.")]
        [SerializeField] private float popDuration = 0.18f;

        [Tooltip("Duration of the fall back down into the hole.")]
        [SerializeField] private float fallDuration = 0.26f;

        [Header("Outside Floor Stray Pieces (Dışarıda Kalan Parçalar)")]
        [Tooltip("Fraction of pieces that bounce onto the outside floor.")]
        [Range(0.10f, 0.35f)]
        [SerializeField] private float outsidePieceRatio = 0.20f;

        [Tooltip("Distance outside the hole rim for stray floor pieces.")]
        [SerializeField] private float outsideScatterDist = 0.55f;

        [Tooltip("Time stray pieces sit on the floor before fading away.")]
        [SerializeField] private float floorStayDuration = 0.35f;

        [Tooltip("Duration of the smooth fade/dissolve on the floor.")]
        [SerializeField] private float floorFadeDuration = 0.28f;

        [Header("Juice & Camera Micro-Shake")]
        [SerializeField] private float cameraShakeDuration = 0.14f;
        [SerializeField] private float cameraShakeStrength = 0.12f;
        [SerializeField] private int cameraShakeVibrato = 16;

        private static Material smoothDustMaterial;

        public GameObject FracturedPrefab { get => fracturedPrefab; set => fracturedPrefab = value; }

        public void TriggerFracture(Vector3 spawnPos, Quaternion spawnRot, Material blockMaterial)
        {
            if (fracturedPrefab == null) return;

            // 1. Instantiate the fractured model at the shallow bottom of the hole cup
            GameObject fracturedInstance = Instantiate(fracturedPrefab, spawnPos, spawnRot);
            fracturedInstance.SetActive(true);

            Color blockColor = Color.white;
            if (blockMaterial != null)
            {
                if (blockMaterial.HasProperty("_BaseColor")) blockColor = blockMaterial.GetColor("_BaseColor");
                else if (blockMaterial.HasProperty("_Color")) blockColor = blockMaterial.color;
            }

            // 2. Apply matching block material to all debris pieces
            Renderer[] renderers = fracturedInstance.GetComponentsInChildren<Renderer>(true);
            if (blockMaterial != null)
            {
                foreach (var rend in renderers)
                {
                    if (rend != null)
                    {
                        rend.enabled = true;
                        rend.gameObject.SetActive(true);
                        rend.material = blockMaterial;
                    }
                }
            }

            // Disable physics for deterministic, clean DOTween animations
            var rbs = fracturedInstance.GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rbs)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            var cols = fracturedInstance.GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                col.enabled = false;
            }

            int childCount = fracturedInstance.transform.childCount;
            if (childCount == 0) return;

            // 3. Collect child transforms and record initial local positions
            List<Transform> allChildren = new List<Transform>(childCount);
            List<Vector3> initialLocalPositions = new List<Vector3>(childCount);
            foreach (Transform child in fracturedInstance.transform)
            {
                allChildren.Add(child);
                initialLocalPositions.Add(child.localPosition);
            }

            // 4. Select a few small stray pieces that bounce outside onto the adjacent floor tiles
            int strayCount = Mathf.Clamp(Mathf.RoundToInt(childCount * outsidePieceRatio), 2, 4);
            HashSet<int> strayIndices = new HashSet<int>();
            while (strayIndices.Count < strayCount && strayIndices.Count < childCount)
            {
                strayIndices.Add(UnityEngine.Random.Range(0, childCount));
            }

            // 5. Animate pieces: Natural pop/hop and fall back down, stray pieces settle on floor and fade away
            for (int i = 0; i < childCount; i++)
            {
                Transform child = allChildren[i];
                Vector3 initialLocalPos = initialLocalPositions[i];
                child.gameObject.SetActive(true);

                // Varied Size Distribution (İrili Ufaklı Parçalar)
                MeshFilter mf = child.GetComponent<MeshFilter>();
                Vector3 unscaledBounds = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : Vector3.one;
                float maxDim = Mathf.Max(unscaledBounds.x, unscaledBounds.y, unscaledBounds.z);
                if (maxDim < 0.05f) maxDim = 1.0f;

                float targetSize;
                int sizeMod = i % 4;
                if (sizeMod == 0) targetSize = UnityEngine.Random.Range(largeChunkSize * 0.90f, largeChunkSize * 1.15f);
                else if (sizeMod == 3) targetSize = UnityEngine.Random.Range(smallChipSize * 0.85f, smallChipSize * 1.20f);
                else targetSize = UnityEngine.Random.Range(mediumChunkSize * 0.85f, mediumChunkSize * 1.15f);

                float scaleFactor = targetSize / maxDim;
                child.localScale = Vector3.one * scaleFactor;

                Sequence pieceSeq = DOTween.Sequence();
                pieceSeq.SetTarget(child);

                float popHeight = UnityEngine.Random.Range(minPopHeight, maxPopHeight);
                float curPopDuration = popDuration + UnityEngine.Random.Range(-0.02f, 0.02f);

                if (strayIndices.Contains(i))
                {
                    // STRAY FLOOR PIECE: Pops up, lands on the adjacent floor tile, stays briefly, and smoothly fades away
                    Vector2 randDir = UnityEngine.Random.insideUnitCircle.normalized;
                    Vector3 floorOffset = new Vector3(randDir.x, 0f, randDir.y) * outsideScatterDist;
                    Vector3 apexPos = initialLocalPos + floorOffset * 0.5f + Vector3.up * popHeight;
                    Vector3 floorLandingPos = initialLocalPos + floorOffset;
                    floorLandingPos.y = 0.05f; // Settled on floor surface

                    // Phase 1: Pop up
                    pieceSeq.Append(child.DOLocalMove(apexPos, curPopDuration).SetEase(Ease.OutQuad));
                    pieceSeq.Join(child.DOLocalRotate(UnityEngine.Random.insideUnitSphere * 90f, curPopDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));

                    // Phase 2: Land on floor
                    pieceSeq.Append(child.DOLocalMove(floorLandingPos, curPopDuration).SetEase(Ease.InQuad));
                    pieceSeq.Join(child.DOLocalRotate(UnityEngine.Random.insideUnitSphere * 60f, curPopDuration, RotateMode.FastBeyond360).SetEase(Ease.InQuad));

                    // Phase 3: Sit on floor
                    pieceSeq.AppendInterval(floorStayDuration);

                    // Phase 4: Smoothly shrink & fade away into the floor
                    pieceSeq.Append(child.DOScale(Vector3.zero, floorFadeDuration).SetEase(Ease.InQuad));
                    pieceSeq.Join(child.DOLocalMoveY(0f, floorFadeDuration).SetEase(Ease.InQuad));
                }
                else
                {
                    // IN-HOLE PIECE: Pops up slightly in place, and falls straight back down into the hole shaft
                    Vector3 apexPos = initialLocalPos + Vector3.up * popHeight + UnityEngine.Random.insideUnitSphere * 0.04f;
                    Vector3 fallTargetPos = initialLocalPos + Vector3.down * 0.85f;
                    float curFallDuration = fallDuration + UnityEngine.Random.Range(-0.02f, 0.03f);

                    // Phase 1: Natural pop up
                    pieceSeq.Append(child.DOLocalMove(apexPos, curPopDuration).SetEase(Ease.OutQuad));
                    pieceSeq.Join(child.DOLocalRotate(UnityEngine.Random.insideUnitSphere * 60f, curPopDuration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));

                    // Phase 2: Fall back down into the hole
                    pieceSeq.Append(child.DOLocalMove(fallTargetPos, curFallDuration).SetEase(Ease.InQuad));
                    pieceSeq.Join(child.DOLocalRotate(UnityEngine.Random.insideUnitSphere * 90f, curFallDuration, RotateMode.FastBeyond360).SetEase(Ease.InQuad));
                    pieceSeq.Join(child.DOScale(Vector3.zero, curFallDuration * 0.85f).SetEase(Ease.InQuad));
                }
            }

            // 6. Spawn soft dust puffs matching the exact shape footprint and shard positions
            SpawnSubtleDust(allChildren, spawnPos, blockColor);

            // 7. Subtle camera micro-shake
            if (Camera.main != null)
            {
                Camera.main.DOComplete();
                Camera.main.DOShakePosition(cameraShakeDuration, cameraShakeStrength, cameraShakeVibrato);
            }

            // 8. Cleanup after stray floor pieces have finished fading
            float totalLifetime = (popDuration * 2) + floorStayDuration + floorFadeDuration + 0.15f;
            Destroy(fracturedInstance, totalLifetime);
        }

        [Header("Smash Dust VFX Prefab")]
        [Tooltip("Optional dust / smash explosion VFX prefab.")]
        [SerializeField] private GameObject dustExplosionPrefab;

        private static Texture2D softMistyTexture;

        /// <summary>
        /// Kırılma anında bloğun tüm geometrisi boyunca (L, T, Kare, Çizgi vb.) uçuşan parçaların arasından çıkan soft duman.
        /// </summary>
        private void SpawnSubtleDust(List<Transform> shards, Vector3 spawnPos, Color blockColor)
        {
            if (shards == null || shards.Count == 0) return;

            GameObject vfxGo = new GameObject("VFX_SoftMistyDust");
            vfxGo.transform.position = spawnPos;

            ParticleSystem ps = vfxGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.duration = 1.60f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.20f, 1.60f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.20f, 0.50f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.gravityModifier = -0.010f; // Hafif ve dengeli süzülme

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = false; // Parçacıkların her birinin konumundan tek tek emit ediyoruz

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(Color.Lerp(blockColor, Color.white, 0.50f), 0.15f),
                    new GradientColorKey(blockColor, 0.55f),
                    new GradientColorKey(Color.white, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.70f, 0.10f), // Hızlıca belirir
                    new GradientAlphaKey(0.55f, 0.40f), // Parçalar yok olurken havada asılı kalır
                    new GradientAlphaKey(0.35f, 0.75f), // Delik kapandıktan sonra kısa süre asılı kalır
                    new GradientAlphaKey(0.12f, 0.90f), // Tatlıca söner
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.65f);
            sizeCurve.AddKey(0.30f, 1.15f);
            sizeCurve.AddKey(0.65f, 1.45f);
            sizeCurve.AddKey(1.0f, 1.75f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            if (smoothDustMaterial == null)
            {
                Shader pShader = Shader.Find("BlockHole/SoftParticleDust")
                              ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                smoothDustMaterial = new Material(pShader);
                smoothDustMaterial.name = "Mat_SoftMistyDust_Instance";
            }

            var psRenderer = vfxGo.GetComponent<ParticleSystemRenderer>();
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.sharedMaterial = smoothDustMaterial;

            // Bloğun L, T, Çizgi, Kare vb. tüm geometrisi boyunca dumanı birbirine kaynaşmış (kesintisiz ve organik) şekilde yay
            Vector3 centerPos = spawnPos;
            for (int i = 0; i < shards.Count; i++)
            {
                if (shards[i] == null) continue;
                Vector3 shardWorld = shards[i].position;
                Vector3 offsetDir = (shardWorld - centerPos);
                offsetDir.y = 0f;

                int puffsPerShard = (i % 2 == 0) ? 2 : 1;
                for (int p = 0; p < puffsPerShard; p++)
                {
                    ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
                    // Karolar arası ayrımı yok eden organik yayılım (seamless blending)
                    Vector3 jitter = new Vector3(
                        UnityEngine.Random.Range(-0.28f, 0.28f),
                        UnityEngine.Random.Range(-0.05f, 0.10f),
                        UnityEngine.Random.Range(-0.28f, 0.28f)
                    );
                    ep.position = new Vector3(shardWorld.x, 0.18f, shardWorld.z) + jitter;
                    ep.velocity = Vector3.up * UnityEngine.Random.Range(0.18f, 0.42f) 
                                + offsetDir.normalized * UnityEngine.Random.Range(0.06f, 0.22f) 
                                + UnityEngine.Random.insideUnitSphere * 0.08f;
                    ep.startSize = UnityEngine.Random.Range(0.35f, 0.52f);
                    ep.rotation = UnityEngine.Random.Range(0f, 360f);
                    ep.startLifetime = UnityEngine.Random.Range(1.20f, 1.60f);
                    ep.startColor = Color.Lerp(blockColor, Color.white, UnityEngine.Random.Range(0.25f, 0.55f));
                    ps.Emit(ep, 1);
                }
            }

            Destroy(vfxGo, 1.8f);
        }

        private static Texture2D CreateSoftGaussianTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] cols = new Color[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(Mathf.Exp(-r * r * 4.5f));
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }
    }
}
