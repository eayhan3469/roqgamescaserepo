using UnityEngine;
using BlockHole;

namespace Bonus.BlockHoleJelly
{
    /// <summary>
    /// Bridges the real BlockHole gameplay flow (BlockDraggable's public UnityEvents) to
    /// JellySpringDriver.Kick(), so the jelly reacts to actual game moments — grabbing a
    /// block, releasing it, or it getting sucked into a hole — instead of only the passive
    /// per-frame motion tracking JellySpringDriver already does on its own.
    ///
    /// Wire this up in the Editor (or via UnityEditor.Events.UnityEventTools in an editor
    /// script) by pointing BlockDraggable's `onDragStarted` at OnGrabbed() and
    /// `onDragEnded` at OnReleased(). `onDroppedInHole` is deliberately NOT used — by the
    /// time BlockDraggable fires it, the block's renderers are already disabled (it fires
    /// after the fracture effect triggers), so a jelly kick at that point would never be
    /// visible. `onDragEnded` already fires at the *start* of a hole-drop too (see
    /// BlockDraggable.DropIntoHole), which is the moment that's actually visible — this
    /// script tells the two cases apart via BlockDraggable.IsDroppedInHole.
    ///
    /// Does NOT modify BlockDraggable.cs or the original Case2_BlockHole prefabs/scripts —
    /// this is a separate companion component added only to the bonus scene's block
    /// instances (as a local prefab-instance override, or a plain scene GameObject), kept
    /// case-independent per the bonus branch's own rule.
    ///
    /// Lives on the same GameObject as BlockDraggable (the root), but the actual
    /// JellySpringDriver can be one level down: single-cell blocks render straight off the
    /// root's own MeshRenderer, but multi-cell blocks (BlockShapeType with a >1-cell
    /// footprint) delegate their combined visible mesh to a runtime-spawned child object
    /// (e.g. root "Block_Block-L" -> child "Block-L" holds the real MeshFilter/MeshRenderer;
    /// the root's own MeshRenderer exists but has no MeshFilter and renders nothing). This
    /// looks in children too so it finds the driver wherever it actually ended up.
    /// </summary>
    public class JellyBlockReactor : MonoBehaviour
    {
        [Header("Kick Strengths")]
        [Tooltip("Small pop when the block is first grabbed.")]
        [SerializeField] private float grabKickStrength = 0.12f;
        [Tooltip("Kick on a normal release/grid-snap, opposite the direction it was just dragged.")]
        [SerializeField] private float releaseKickStrength = 0.22f;
        [Tooltip("Bigger kick when release instead means it's being sucked into a hole.")]
        [SerializeField] private float holeDropKickStrength = 0.45f;

        private JellySpringDriver spring;
        private BlockDraggable draggable;

        private Vector3 lastDragPos;
        private Vector3 lastDragMoveDir = Vector3.forward;

        private void Awake()
        {
            spring = GetComponentInChildren<JellySpringDriver>(true);
            draggable = GetComponent<BlockDraggable>();
            lastDragPos = transform.position;

            if (spring == null)
            {
                Debug.LogWarning($"JellyBlockReactor on '{name}' found no JellySpringDriver in itself or its children — add one to whichever object holds the real MeshRenderer.", this);
            }
        }

        private void Update()
        {
            bool isDragging = draggable != null && draggable.IsDragging;

            // Mute JellySpringDriver's passive per-frame reactivity while actively being
            // dragged — the drag-follow tween changes direction every frame (mouse jitter,
            // Lerp catch-up), and letting that keep re-kicking the spring made the block
            // wobble chaotically the whole time it was held instead of a clean jelly feel.
            // Only the deliberate grab/release Kicks below should read as "jelly" while
            // dragging; BlockDraggable's own tilt/sway already sells the drag-carry feel.
            if (spring != null)
            {
                spring.PassiveReactivityEnabled = !isDragging;
            }

            // Track the most recent drag movement direction while actively dragging, so
            // OnReleased() knows which way to squash back into on a normal release.
            if (isDragging)
            {
                Vector3 delta = transform.position - lastDragPos;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    lastDragMoveDir = delta.normalized;
                }
            }
            lastDragPos = transform.position;
        }

        /// <summary>Wire to BlockDraggable.onDragStarted.</summary>
        public void OnGrabbed()
        {
            if (spring == null) return;
            spring.Kick(Vector3.up, grabKickStrength);
        }

        /// <summary>
        /// Wire to BlockDraggable.onDragEnded — fires both on a normal release/snap and at
        /// the start of a hole-drop (see BlockDraggable.DropIntoHole); IsDroppedInHole tells
        /// the two apart.
        /// </summary>
        public void OnReleased()
        {
            if (spring == null) return;

            if (draggable != null && draggable.IsDroppedInHole)
            {
                spring.Kick(Vector3.down, holeDropKickStrength);
            }
            else
            {
                Vector3 dir = -lastDragMoveDir;
                spring.Kick(dir, releaseKickStrength);
            }
        }
    }
}
