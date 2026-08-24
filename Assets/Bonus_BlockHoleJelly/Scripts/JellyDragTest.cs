using UnityEngine;
using UnityEngine.InputSystem;

namespace Bonus.BlockHoleJelly
{
    /// <summary>
    /// Minimal mouse-drag for the standalone jelly test scene — just enough to yank cubes
    /// around and see JellySpringDriver react. Not the real Block Hole input/drag logic
    /// (that stays in Case2_BlockHole/BlockDraggable.cs, untouched); this is throwaway
    /// scaffolding for previewing the shader/spring feel in isolation.
    ///
    /// Uses the new Input System directly (Mouse.current) rather than the legacy
    /// OnMouseDown/Input.mousePosition — this project has Active Input Handling set to
    /// "Input System Package (New)" only (activeInputHandler: 1 in ProjectSettings), so
    /// UnityEngine.Input calls throw at runtime.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class JellyDragTest : MonoBehaviour
    {
        [SerializeField] private float dragHeight = 0f;

        private static Camera cam;
        private Plane dragPlane;
        private bool dragging;
        private Vector3 dragOffset;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
                {
                    dragPlane = new Plane(Vector3.up, new Vector3(0f, dragHeight, 0f));
                    if (RaycastPlane(mouse, out Vector3 hitPoint))
                    {
                        dragOffset = transform.position - hitPoint;
                    }
                    dragging = true;
                }
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
            }

            if (dragging && RaycastPlane(mouse, out Vector3 dragPoint))
            {
                transform.position = dragPoint + dragOffset;
            }
        }

        private bool RaycastPlane(Mouse mouse, out Vector3 point)
        {
            point = Vector3.zero;
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (dragPlane.Raycast(ray, out float dist))
            {
                point = ray.GetPoint(dist);
                return true;
            }
            return false;
        }
    }
}
