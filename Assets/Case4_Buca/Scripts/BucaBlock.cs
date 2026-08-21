using System;
using UnityEngine;
using DG.Tweening;

namespace Buca
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class BucaBlock : MonoBehaviour
    {
        [Header("Block Settings")]
        [Tooltip("Mass of the block.")]
        [SerializeField] private float mass = 1.5f;

        [Tooltip("Minimum impact velocity to knock down.")]
        [SerializeField] private float minImpactVelocity = 2.0f;

        [Tooltip("Explosion/scatter impulse multiplier when hit directly by disc.")]
        [SerializeField] private float scatterImpulse = 18.0f;

        [Tooltip("Upward modifier for explosive tumbling.")]
        [SerializeField] private float upwardModifier = 1.2f;

        [Header("Visual Feedback")]
        [Tooltip("Flash color on impact.")]
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.9f, 0.4f, 1f);

        private Rigidbody rb;
        private BoxCollider boxCollider;
        private MeshRenderer meshRenderer;
        private Material blockMat;
        private Color originalColor;
        private bool isHit = false;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        public bool IsHit => isHit;
        public Rigidbody Rb => rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            boxCollider = GetComponent<BoxCollider>();
            meshRenderer = GetComponent<MeshRenderer>();

            initialPosition = transform.position;
            initialRotation = transform.rotation;

            SetupRigidbody();

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
            rb.linearDamping = 0.5f;
            rb.angularDamping = 1.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Start sleeping/stable so blocks stack perfectly without jitter
            rb.isKinematic = true;
        }

        /// <summary>
        /// Awakens the block physics and applies explosive scatter impulse.
        /// </summary>
        public void HitByDisc(Vector3 hitPoint, Vector3 hitVelocity, float forceMultiplier = 1.0f)
        {
            if (isHit) return;
            isHit = true;

            // Enable dynamic physics
            rb.isKinematic = false;
            rb.useGravity = true;

            // Apply impact impulse + random torque for satisfying tumble
            float totalForce = Mathf.Max(hitVelocity.magnitude * forceMultiplier, scatterImpulse);
            Vector3 forceDir = (transform.position - hitPoint).normalized + Vector3.up * upwardModifier;
            rb.AddForceAtPosition(forceDir * totalForce, hitPoint, ForceMode.Impulse);

            Vector3 randomTorque = new Vector3(
                UnityEngine.Random.Range(-30f, 30f),
                UnityEngine.Random.Range(-30f, 30f),
                UnityEngine.Random.Range(-30f, 30f)
            );
            rb.AddTorque(randomTorque, ForceMode.Impulse);

            // Flash visual feedback
            if (blockMat != null)
            {
                blockMat.DOKill();
                blockMat.DOColor(hitFlashColor, 0.08f).OnComplete(() =>
                {
                    blockMat.DOColor(originalColor, 0.4f);
                });
            }

            // Chain reaction: awaken immediately touching blocks
            AwakenNeighborBlocks();
        }

        private void AwakenNeighborBlocks()
        {
            Collider[] neighbors = Physics.OverlapSphere(transform.position, 1.8f);
            foreach (var col in neighbors)
            {
                if (col.TryGetComponent<BucaBlock>(out var neighborBlock) && neighborBlock != this)
                {
                    if (neighborBlock.rb.isKinematic)
                    {
                        neighborBlock.rb.isKinematic = false;
                        neighborBlock.rb.useGravity = true;
                        neighborBlock.rb.AddExplosionForce(scatterImpulse * 0.5f, transform.position, 2.5f, 0.5f, ForceMode.Impulse);
                    }
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // If hit by another fast-moving dynamic block
            if (collision.relativeVelocity.magnitude > minImpactVelocity && rb.isKinematic)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        public void ResetBlock()
        {
            isHit = false;
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
            if (blockMat != null)
            {
                blockMat.DOKill();
            }
        }
    }
}
