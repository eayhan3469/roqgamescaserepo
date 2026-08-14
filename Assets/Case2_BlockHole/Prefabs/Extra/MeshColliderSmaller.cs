using Sirenix.OdinInspector;
using UnityEngine;

namespace _HoleBlock.Scripts.Contexts.Gameplay.Controllers
{
    public class MeshColliderSmaller : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)] private float _scaleFactor = 0.9f;

        [Button]
        public void Smaller()
        {
            Smaller(_scaleFactor, transform);
        }

        public static void Smaller(float scaleFactor, Transform targetTransform)
        {
            foreach (Transform child in targetTransform)
            {
                var meshRenderer = child.GetComponent<MeshRenderer>();
                if (meshRenderer == null) continue;

                var meshFilter = child.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                // Remove ALL existing mesh colliders from this child and its children
                var existingColliders = child.GetComponentsInChildren<MeshCollider>(true);
                for (int i = existingColliders.Length - 1; i >= 0; i--)
                {
                    DestroyImmediate(existingColliders[i]);
                }

                // Remove empty child GameObjects (only Transform, no other components)
                for (int i = child.childCount - 1; i >= 0; i--)
                {
                    var c = child.GetChild(i);
                    if (c.GetComponents<Component>().Length == 1) // only Transform
                    {
                        DestroyImmediate(c.gameObject);
                    }
                }

                // Create a new child with scaled mesh collider
                var colliderObj = new GameObject(child.name + "_MeshCollider");
                colliderObj.transform.SetParent(child);
                colliderObj.transform.localPosition = Vector3.zero;
                colliderObj.transform.localRotation = Quaternion.identity;
                colliderObj.transform.localScale = Vector3.one * scaleFactor;

                var newCollider = colliderObj.AddComponent<MeshCollider>();
                newCollider.sharedMesh = meshFilter.sharedMesh;
                newCollider.convex = true;
            }
        }

        [Button]
        public void RemoveAllColliders()
        {
            RemoveAllColliders(transform);
        }

        public static void RemoveAllColliders(Transform targetTransform)
        {
            foreach (Transform child in targetTransform)
            {
                for (int i = child.childCount - 1; i >= 0; i--)
                {
                    var c = child.GetChild(i);
                    if (c.GetComponent<Collider>() != null)
                    {
                        DestroyImmediate(c.gameObject);
                    }
                }

                var colliders = child.GetComponents<Collider>();
                for (int i = colliders.Length - 1; i >= 0; i--)
                {
                    DestroyImmediate(colliders[i]);
                }
            }
        }
    }
}