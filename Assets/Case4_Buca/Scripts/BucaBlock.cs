using System;
using UnityEngine;
using DG.Tweening;

namespace Buca
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class BucaBlock : MonoBehaviour
    {
        [Header("Block Physics Settings")]
        [Tooltip("Mass of the block (lighter mass allows crisp toppling and scattering).")]
        [SerializeField] private float mass = 0.85f;

        [Tooltip("Minimum impact velocity to knock down.")]
        [SerializeField] private float minImpactVelocity = 1.2f;

        [Tooltip("Base impulse force applied when hit directly by disc.")]
        [SerializeField] private float scatterImpulse = 14.0f;

        [Tooltip("Upward modifier for gentle lift (keeps blocks grounded and tumbling).")]
        [SerializeField] private float upwardModifier = 0.10f;

        [Tooltip("Forward momentum bias (0 = purely contact normal, 1 = purely disc direction).")]
        [SerializeField] private float forwardMomentumRatio = 0.75f;

        [Header("Visual & Juice Feedback")]
        [Tooltip("Flash color on impact.")]
        [SerializeField] private Color hitFlashColor = new Color(1.0f, 0.95f, 0.4f, 1f);

        [Tooltip("Tactile squash & punch scale on hit.")]
        [SerializeField] private float punchScaleAmount = 0.18f;

        private Rigidbody rb;
        private BoxCollider boxCollider;
        private MeshRenderer meshRenderer;
        private Material blockMat;
        private Color originalColor;
        private bool isHit = false;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Vector3 initialScale;
        private float lastClatterSoundTime = 0f;

        public bool IsHit => isHit;
        public Rigidbody Rb => rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            boxCollider = GetComponent<BoxCollider>();
            meshRenderer = GetComponent<MeshRenderer>();

            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialScale = transform.localScale;

            SetupRigidbody();
            SetupPhysXMaterial();

            if (meshRenderer != null && meshRenderer.material != null)
            {
                blockMat = meshRenderer.material;
                originalColor = blockMat.color;
            }
        }

        private void SetupRigidbody()
        {
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

            rb.mass = mass;
            rb.linearDamping = 0.45f;
            rb.angularDamping = 1.0f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Start sleeping/stable so blocks stack cleanly without jitter
            rb.isKinematic = true;
        }

        private void SetupPhysXMaterial()
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                PhysicsMaterial blockPhysMat = new PhysicsMaterial("BucaBlockPhysMat")
                {
                    dynamicFriction = 0.40f,
                    staticFriction = 0.45f,
                    bounciness = 0.10f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Multiply
                };
                boxCollider.material = blockPhysMat;
            }
        }

        /// <summary>
        /// Awakens the block physics with realistic directional momentum and satisfying tumbling.
        /// </summary>
        public void HitByDisc(Vector3 hitPoint, Vector3 hitVelocity, float forceMultiplier = 1.25f)
        {
            if (isHit) return;
            isHit = true;

            // Enable dynamic physics
            rb.isKinematic = false;
            rb.useGravity = true;

            // Calculate directional momentum: blend puck forward travel dir + contact scatter normal
            Vector3 puckDir = hitVelocity.sqrMagnitude > 0.01f ? hitVelocity.normalized : Vector3.forward;
            Vector3 contactScatterDir = (transform.position - hitPoint);
            contactScatterDir.y = 0f;
            if (contactScatterDir.sqrMagnitude > 0.001f) contactScatterDir.Normalize();
            else contactScatterDir = puckDir;

            // Blend forward penetration momentum with outward scatter
            Vector3 combinedDir = Vector3.Lerp(contactScatterDir, puckDir, forwardMomentumRatio);
            combinedDir.y = upwardModifier; // Controlled slight lift to clear ground friction
            combinedDir.Normalize();

            // Total impulse force scaled realistically to hit velocity
            float speedRatio = Mathf.Clamp01(hitVelocity.magnitude / 25.0f);
            float totalForce = Mathf.Lerp(10.0f, 22.0f, speedRatio) * forceMultiplier;

            // Apply force at the point of contact to cause natural tipping leverage
            rb.AddForceAtPosition(combinedDir * totalForce, hitPoint, ForceMode.Impulse);

            // Natural torque around impact point + slight random tumble
            Vector3 impactOffset = hitPoint - transform.position;
            Vector3 leverageTorque = Vector3.Cross(impactOffset, combinedDir) * (totalForce * 0.6f);
            Vector3 randomTorque = new Vector3(
                UnityEngine.Random.Range(-8f, 8f),
                UnityEngine.Random.Range(-5f, 5f),
                UnityEngine.Random.Range(-8f, 8f)
            );
            rb.AddTorque(leverageTorque + randomTorque, ForceMode.Impulse);

            // Trigger juice effects (Micro Screen Shake, Sound, VFX)
            if (BucaJuiceManager.Instance != null)
            {
                BucaJuiceManager.Instance.TriggerImpactJuice(hitPoint, combinedDir, speedRatio);
            }

            // Tactile Squash & Stretch on impact
            transform.DOKill();
            transform.DOPunchScale(new Vector3(punchScaleAmount, -punchScaleAmount, punchScaleAmount), 0.14f, 7, 0.6f);

            // Flash visual feedback
            if (blockMat != null)
            {
                blockMat.DOKill();
                blockMat.DOColor(hitFlashColor, 0.06f).OnComplete(() =>
                {
                    blockMat.DOColor(originalColor, 0.22f);
                });
            }

            // Chain reaction: awaken immediately touching neighbor blocks with forward domino push
            AwakenNeighborBlocks(combinedDir, totalForce);
        }

        private void AwakenNeighborBlocks(Vector3 pushDirection, float sourceForce)
        {
            Collider[] neighbors = Physics.OverlapSphere(transform.position, 1.5f);
            foreach (var col in neighbors)
            {
                if (col.TryGetComponent<BucaBlock>(out var neighborBlock) && neighborBlock != this)
                {
                    if (neighborBlock.rb.isKinematic)
                    {
                        neighborBlock.rb.isKinematic = false;
                        neighborBlock.rb.useGravity = true;
                        neighborBlock.isHit = true;

                        // Domino forward push
                        Vector3 dominoPush = pushDirection * (sourceForce * 0.45f);
                        dominoPush.y = 0.04f;
                        neighborBlock.rb.AddForce(dominoPush, ForceMode.Impulse);

                        Vector3 slightTorque = new Vector3(
                            UnityEngine.Random.Range(-6f, 6f),
                            UnityEngine.Random.Range(-4f, 4f),
                            UnityEngine.Random.Range(-6f, 6f)
                        );
                        neighborBlock.rb.AddTorque(slightTorque, ForceMode.Impulse);
                    }
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactSpeed = collision.relativeVelocity.magnitude;

            // If hit by another fast-moving dynamic block, awaken naturally
            if (impactSpeed > minImpactVelocity && rb.isKinematic)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                isHit = true;
            }

            // Block clatter sound feedback when tumbling and hitting ground/other blocks
            if (impactSpeed > 1.0f && Time.time - lastClatterSoundTime > 0.10f)
            {
                lastClatterSoundTime = Time.time;
                if (BucaAudioManager.Instance != null)
                {
                    float clatterVol = Mathf.Clamp01(impactSpeed / 10.0f);
                    BucaAudioManager.Instance.PlayBlockClatterSound(clatterVol);
                }
            }
        }

        public void ResetBlock()
        {
            isHit = false;
            transform.DOKill();
            transform.localScale = initialScale;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            transform.position = initialPosition;
            transform.rotation = initialRotation;

            if (blockMat != null)
            {
                blockMat.DOKill();
                blockMat.color = originalColor;
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
            if (blockMat != null)
            {
                blockMat.DOKill();
            }
        }
    }
}
