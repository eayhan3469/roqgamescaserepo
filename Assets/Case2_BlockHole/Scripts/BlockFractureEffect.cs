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
        [SerializeField] private float maxBurstDistance = 0.48f;

        [Tooltip("Duration of outward crack explosion.")]
        [SerializeField] private float burstDuration = 0.18f;

        [Tooltip("Delay before chunks start shrinking away.")]
        [SerializeField] private float shrinkDelay = 0.08f;

        [Tooltip("Duration of chunk shrink animation.")]
        [SerializeField] private float shrinkDuration = 0.20f;

        [Header("Juice & Shake")]
        [SerializeField] private float cameraShakeDuration = 0.20f;
        [SerializeField] private float cameraShakeStrength = 0.16f;
        [SerializeField] private int cameraShakeVibrato = 18;

        public GameObject FracturedPrefab { get => fracturedPrefab; set => fracturedPrefab = value; }

        public void TriggerFracture(Vector3 spawnPos, Quaternion spawnRot, Material blockMaterial)
        {
            if (fracturedPrefab == null) return;

            // 1. Seamless 1:1 instantiation at the exact position and rotation of the intact block
            GameObject fracturedInstance = Instantiate(fracturedPrefab, spawnPos, spawnRot);
            fracturedInstance.SetActive(true);

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

            // 4. Animate pieces cracking open and blasting outward from the shape's center
            foreach (Transform child in fracturedInstance.transform)
            {
                child.gameObject.SetActive(true);
                child.localScale = Vector3.one; // Full 100% scale forming solid intact block on frame 0

                Vector3 fromCenter = child.localPosition - centerOfMass;
                Vector3 outwardDir = fromCenter.sqrMagnitude > 0.001f ? fromCenter.normalized : Random.insideUnitSphere.normalized;
                
                // Add upward bias and slight chaotic jitter
                outwardDir = (outwardDir * 0.75f + Vector3.up * 0.65f + Random.insideUnitSphere * 0.20f).normalized;

                float burstDist = Random.Range(minBurstDistance, maxBurstDistance);
                Vector3 burstTargetPos = child.localPosition + outwardDir * burstDist;
                Vector3 fallTargetPos = burstTargetPos + Vector3.down * Random.Range(0.20f, 0.40f);

                Sequence pieceSeq = DOTween.Sequence();
                pieceSeq.SetTarget(child);

                // Phase 1: Cracks burst outward from shape center
                pieceSeq.Append(child.DOLocalMove(burstTargetPos, burstDuration).SetEase(Ease.OutQuad));
                pieceSeq.Join(child.DOLocalRotate(Random.insideUnitSphere * 90f, burstDuration, RotateMode.FastBeyond360));

                // Phase 2: Fall back slightly
                pieceSeq.Append(child.DOLocalMove(fallTargetPos, 0.16f).SetEase(Ease.InQuad));
                pieceSeq.Join(child.DOLocalRotate(Random.insideUnitSphere * 120f, 0.16f, RotateMode.FastBeyond360));

                // Phase 3: Shrink away smoothly
                pieceSeq.Insert(shrinkDelay, child.DOScale(Vector3.zero, shrinkDuration).SetEase(Ease.InBack));
            }

            // 5. Punchy camera shake
            if (Camera.main != null)
            {
                Camera.main.DOComplete();
                Camera.main.DOShakePosition(cameraShakeDuration, cameraShakeStrength, cameraShakeVibrato);
            }

            Destroy(fracturedInstance, burstDuration + shrinkDuration + 0.15f);
        }
    }
}
