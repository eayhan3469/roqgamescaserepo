using System;
using UnityEngine;
using DG.Tweening;

namespace BlockHole
{
    public class BlockFractureEffect : MonoBehaviour
    {
        [Header("Fracture Configuration")]
        [SerializeField] private GameObject fracturedPrefab;

        [Tooltip("Burst outward distance for pieces from the shape center.")]
        [SerializeField] private float minBurstDistance = 0.22f;
        [SerializeField] private float maxBurstDistance = 0.45f;

        [Tooltip("Duration of outward crack explosion.")]
        [SerializeField] private float burstDuration = 0.18f;

        [Tooltip("Delay before chunks start shrinking away.")]
        [SerializeField] private float shrinkDelay = 0.08f;

        [Tooltip("Duration of chunk shrink animation.")]
        [SerializeField] private float shrinkDuration = 0.20f;

        [Header("Juice & Shake")]
        [SerializeField] private float cameraShakeDuration = 0.18f;
        [SerializeField] private float cameraShakeStrength = 0.15f;
        [SerializeField] private int cameraShakeVibrato = 16;

        private static GameObject dustSystemPrefab;
        private static Material smoothDustMaterial;

        public GameObject FracturedPrefab { get => fracturedPrefab; set => fracturedPrefab = value; }

        public void TriggerFracture(Vector3 spawnPos, Quaternion spawnRot, Material blockMaterial)
        {
            if (fracturedPrefab == null) return;

            // 1. Seamless 1:1 instantiation at the exact position and rotation of the intact block
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

            // Disable physics for deterministic DOTween crack burst
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

            // 3. Compute Center of Mass of the fractured pieces to burst outward from the shape's true center
            Vector3 centerOfMass = Vector3.zero;
            int childCount = fracturedInstance.transform.childCount;
            if (childCount == 0) return;

            foreach (Transform child in fracturedInstance.transform)
            {
                centerOfMass += child.localPosition;
            }
            centerOfMass /= childCount;

            // 4. Animate pieces cracking open cleanly with subtle, tight burst
            foreach (Transform child in fracturedInstance.transform)
            {
                child.gameObject.SetActive(true);
                child.localScale = Vector3.one;

                Vector3 fromCenter = child.localPosition - centerOfMass;
                Vector3 outwardDir = fromCenter.sqrMagnitude > 0.001f ? fromCenter.normalized : UnityEngine.Random.insideUnitSphere.normalized;
                
                // Subtle upward bias and clean outward crack
                outwardDir = (outwardDir * 0.75f + Vector3.up * 0.60f + UnityEngine.Random.insideUnitSphere * 0.18f).normalized;

                float burstDist = UnityEngine.Random.Range(minBurstDistance, maxBurstDistance);
                Vector3 burstTargetPos = child.localPosition + outwardDir * burstDist;
                Vector3 fallTargetPos = burstTargetPos + Vector3.down * UnityEngine.Random.Range(0.20f, 0.40f);

                Sequence pieceSeq = DOTween.Sequence();
                pieceSeq.SetTarget(child);

                // Phase 1: Cracks burst outward cleanly
                pieceSeq.Append(child.DOLocalMove(burstTargetPos, burstDuration).SetEase(Ease.OutQuad));
                pieceSeq.Join(child.DOLocalRotate(UnityEngine.Random.insideUnitSphere * 90f, burstDuration, RotateMode.FastBeyond360));

                // Phase 2: Sinks down into the shaft
                pieceSeq.Append(child.DOLocalMove(fallTargetPos, 0.16f).SetEase(Ease.InQuad));
                pieceSeq.Join(child.DOLocalRotate(UnityEngine.Random.insideUnitSphere * 120f, 0.16f, RotateMode.FastBeyond360));

                // Phase 3: Shrinks away smoothly
                pieceSeq.Insert(shrinkDelay, child.DOScale(Vector3.zero, shrinkDuration).SetEase(Ease.InBack));
            }

            // 5. Spawn small, subtle, smooth dust puffs right from the breaking chunks
            SpawnSmoothChunkDust(fracturedInstance.transform, blockColor);

            // 6. Subtle camera micro-shake
            if (Camera.main != null)
            {
                Camera.main.DOComplete();
                Camera.main.DOShakePosition(cameraShakeDuration, cameraShakeStrength, cameraShakeVibrato);
            }

            Destroy(fracturedInstance, burstDuration + shrinkDuration + 0.15f);
        }

        /// <summary>
        /// Kırılan parçaların tam konumlarından ufak ufak çıkan pürüzsüz ve yumuşak toz pufcukları.
        /// </summary>
        private void SpawnSmoothChunkDust(Transform fracturedRoot, Color blockColor)
        {
            if (fracturedRoot == null) return;

            GameObject dustGo = new GameObject("VFX_SmoothChunkDust");
            dustGo.transform.position = fracturedRoot.position;

            ParticleSystem ps = dustGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.duration = 0.50f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.40f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.26f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.gravityModifier = -0.05f; // Gentle upward dust drift

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = false;

            // Soft alpha fade curve for smooth dust
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(Color.Lerp(blockColor, Color.white, 0.4f), 0.35f),
                    new GradientColorKey(blockColor, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.55f, 0.20f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = grad;

            // Size expanding smoothly like soft dust puff
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.35f);
            sizeCurve.AddKey(0.40f, 1.15f);
            sizeCurve.AddKey(1.0f, 1.45f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            if (smoothDustMaterial == null)
            {
                Material circleMat = Resources.Load<Material>("Mat_Particle_Circle");
                if (circleMat != null)
                {
                    smoothDustMaterial = circleMat;
                }
                else
                {
                    Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") 
                                  ?? Shader.Find("Particles/Standard Unlit") 
                                  ?? Shader.Find("Mobile/Particles/Alpha Blended");
                    smoothDustMaterial = new Material(pShader);
                }
            }

            var psRenderer = dustGo.GetComponent<ParticleSystemRenderer>();
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.sharedMaterial = smoothDustMaterial;

            // Emit 1-2 small soft puffs from each chunk's position
            foreach (Transform child in fracturedRoot)
            {
                if (child == null) continue;
                Vector3 chunkPos = child.position + UnityEngine.Random.insideUnitSphere * 0.04f;
                Vector3 driftVel = (UnityEngine.Random.insideUnitSphere * 0.18f) + (Vector3.up * UnityEngine.Random.Range(0.20f, 0.45f));

                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = chunkPos,
                    velocity = driftVel,
                    startColor = Color.Lerp(blockColor, Color.white, 0.35f),
                    startSize = UnityEngine.Random.Range(0.14f, 0.26f),
                    startLifetime = UnityEngine.Random.Range(0.35f, 0.55f)
                };

                ps.Emit(emitParams, 1);
            }

            Destroy(dustGo, 0.8f);
        }
    }
}
