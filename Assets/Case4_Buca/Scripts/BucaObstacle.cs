using System;
using UnityEngine;
using DG.Tweening;

namespace Buca
{
    public class BucaObstacle : MonoBehaviour
    {
        [Header("Obstacle Settings")]
        [Tooltip("Deflection impulse applied to disc upon hitting rotating obstacle.")]
        [SerializeField] private float bounceDeflectionForce = 12.0f;

        [Header("Visual Feedback")]
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.2f, 0.2f, 1f);

        private MeshRenderer meshRenderer;
        private Material obstacleMat;
        private Color originalColor;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.material != null)
            {
                obstacleMat = meshRenderer.material;
                originalColor = obstacleMat.color;
            }

            // Ensure collider
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                gameObject.AddComponent<MeshCollider>().convex = true;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Player") || collision.gameObject.name.Contains("disc"))
            {
                OnHitByDisc(collision);
            }
        }

        private void OnHitByDisc(Collision collision)
        {
            // Flash red
            if (obstacleMat != null)
            {
                obstacleMat.DOKill();
                obstacleMat.DOColor(hitFlashColor, 0.08f).OnComplete(() =>
                {
                    obstacleMat.DOColor(originalColor, 0.35f);
                });
            }

            // Apply deflection force
            if (collision.rigidbody != null)
            {
                Vector3 bounceDir = (collision.transform.position - transform.position).normalized;
                bounceDir.y = 0.1f;
                collision.rigidbody.AddForce(bounceDir * bounceDeflectionForce, ForceMode.Impulse);
            }
        }

        private void OnDestroy()
        {
            if (obstacleMat != null)
            {
                obstacleMat.DOKill();
            }
        }
    }
}
