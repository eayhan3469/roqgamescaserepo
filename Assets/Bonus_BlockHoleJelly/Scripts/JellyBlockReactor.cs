using UnityEngine;
using DG.Tweening;
using BlockHole;

namespace Bonus.BlockHoleJelly
{
    /// <summary>
    /// Bridges the real BlockHole gameplay flow (BlockDraggable's public UnityEvents) to
    /// the jelly reactions, so they trigger on actual game moments — grabbing a block
    /// (JellySpringDriver.Kick + switches it into drag-lag mode so it stretches
    /// continuously while held), releasing it normally (Kick, back to idle spring mode),
    /// or it getting swallowed by a hole (a slow shrink-to-nothing squish instead of the
    /// real BlockFractureEffect shatter — see PlayHoleSquish).
    ///
    /// Wire this up in the Editor (or via UnityEditor.Events.UnityEventTools in an editor
    /// script) by pointing BlockDraggable's `onDragStarted` at OnGrabbed() and
    /// `onDragEnded` at OnReleased(). `onDroppedInHole` is deliberately NOT used — by the
    /// time BlockDraggable fires it, the block's renderers are already disabled (it fires
    /// after the fracture effect triggers), so a jelly reaction at that point would never be
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
        [Tooltip("Small pop when the block is first grabbed. NOTE: Kick() strength is a velocity impulse, not a direct displacement — the resulting peak wobble amplitude works out to roughly strength / sqrt(JellySpringDriver.stiffness), so these numbers look big compared to the ~0.1-0.4 amplitude range they actually produce. Re-tune together with stiffness if you change either.")]
        [SerializeField] private float grabKickStrength = 1.4f;
        [Tooltip("Kick on a normal release/grid-snap, opposite the direction it was just dragged. Same velocity-impulse caveat as grabKickStrength above.")]
        [SerializeField] private float releaseKickStrength = 2.6f;

        [Header("Continuous Drag Feel")]
        [Tooltip("Jelly amount at maximum elastic lead (i.e. the mouse pulling the block as far as BlockDraggable's own clamp allows within its current grid cell). Scales down to 0 as the block sits exactly on its anchor. This is what makes the jelly feel continuously present while being carried, not just at grab/release — see JellySpringDriver's class doc for why it's driven by lead-from-anchor distance rather than raw drag velocity.")]
        [SerializeField] private float dragPullMaxAmount = 0.3f;

        [Header("Hole-Entry Squish (replaces fracture/shatter)")]
        [Tooltip("Duration of the shrink-to-nothing squish when the block is swallowed by a hole. Kept roughly in sync with BlockDraggable's own holeDropDuration on this instance so the block finishes shrinking right as BlockDraggable disables its renderers — tune both together.")]
        [SerializeField] private float holeSquishDuration = 0.6f;
        [SerializeField] private Ease holeSquishEase = Ease.InQuad;

        private JellySpringDriver spring;
        private BlockDraggable draggable;

        private Vector3 lastDragPos;
        private Vector3 lastDragMoveDir = Vector3.forward;

        // Which cardinal axis (+/-X or +/-Z) the drag-lag stretch is currently locked to —
        // see the big comment in Update() for why this has to be cardinal-only, not the
        // raw diagonal lead direction.
        private Vector3 dragLeadAxis = Vector3.right;

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

        private void Start()
        {
            // Jelly blocks squish into the hole instead of shattering (see OnReleased), so
            // BlockDraggable's own fracture-piece spawn needs to be off. The primary fix is
            // that the BlockFractureEffect component is simply removed from these instances
            // in the scene — this Start()-time null-out is a defensive fallback in case one
            // ever gets re-added. It has to run in Start(), not Awake(): BlockDraggable's own
            // Awake() does `if (fractureEffect == null) fractureEffect =
            // GetComponent<BlockFractureEffect>()`, which would silently re-fill a null set
            // here in Awake() if it happened to run after ours (Unity doesn't guarantee
            // Awake order between components) — Start() runs only after every Awake, so this
            // is guaranteed to be the last word. Only affects this instance's field, not the
            // shared BlockDraggable/BlockFractureEffect scripts or the real Case2 prefabs.
            if (draggable != null)
            {
                draggable.FractureEffect = null;
            }
        }

        private void Update()
        {
            bool isDragging = draggable != null && draggable.IsDragging;

            // Keep JellySpringDriver's mode in sync every frame as a safety net (OnGrabbed/
            // OnReleased below already set it immediately on the actual transition, so this
            // is mostly a fallback in case IsDragging ever changes some other way).
            if (spring != null)
            {
                spring.DragLagEnabled = isDragging;
                spring.PassiveReactivityEnabled = !isDragging;
            }

            if (isDragging)
            {
                // Feed the continuous drag-lag target from how far the block currently
                // sits from its snapped grid anchor (BlockDraggable clamps this "lead" to
                // ~0.45 tile itself) rather than raw velocity — see JellySpringDriver's
                // class doc for why: the grid-snapped elastic-lead movement holds the
                // block nearly stationary within a tile between discrete cell jumps, so
                // velocity reads as ~0 most of the time even while a mouse is actively
                // holding it off-center.
                if (spring != null && draggable != null && BlockHole.GridManager.Instance != null)
                {
                    Vector3 anchorWorldPos = draggable.GetWorldPosForAnchor(draggable.CurrentAnchorGridPos);
                    Vector3 lead = transform.position - anchorWorldPos;
                    lead.y = 0f;
                    float leadMag = lead.magnitude;

                    float clampRange = BlockHole.GridManager.Instance.TileSize * 0.45f;
                    float normalizedPull = clampRange > 0.0001f ? Mathf.Clamp01(leadMag / clampRange) : 0f;

                    // Lock the stretch axis to a cardinal (+/-X or +/-Z) instead of the raw
                    // diagonal lead direction. A cube stretched along an arbitrary diagonal
                    // axis reads as a lopsided parallelogram/rhomboid, and — worse — as the
                    // player's cursor wanders even slightly within the cell, that diagonal
                    // angle keeps changing frame to frame, which looked like the block's
                    // corners darting off in different directions each frame (reported by
                    // the user). Locking to X/Z keeps every frame's shape a clean rectangular
                    // stretch, and collapses the direction to only 4 possible states instead
                    // of a continuous angle, which is also inherently far less jittery.
                    // Hysteresis (need >20% larger, not just >) stops it flapping back and
                    // forth right at a 45-degree lead.
                    if (leadMag > 0.02f)
                    {
                        float absX = Mathf.Abs(lead.x);
                        float absZ = Mathf.Abs(lead.z);
                        bool currentIsX = Mathf.Abs(dragLeadAxis.x) > 0.5f;
                        bool preferX = currentIsX ? absX >= absZ * 0.8f : absX > absZ * 1.2f;
                        dragLeadAxis = preferX
                            ? new Vector3(Mathf.Sign(lead.x), 0f, 0f)
                            : new Vector3(0f, 0f, Mathf.Sign(lead.z));
                    }

                    spring.SetDragLagTarget(dragLeadAxis, normalizedPull * dragPullMaxAmount);
                }

                // Track the most recent drag movement direction too, for OnReleased()'s
                // normal-release Kick (which wants a direction even the instant the lead
                // happens to be back near zero).
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
            spring.DragLagEnabled = true;
            spring.Kick(Vector3.up, grabKickStrength);
        }

        /// <summary>
        /// Wire to BlockDraggable.onDragEnded — fires both on a normal release/snap and at
        /// the start of a hole-drop (see BlockDraggable.DropIntoHole); IsDroppedInHole tells
        /// the two apart.
        /// </summary>
        public void OnReleased()
        {
            if (spring != null)
            {
                // Switch back to idle spring mode *before* the Kick below, so the impulse
                // actually drives the oscillator instead of being immediately overwritten
                // by drag-lag's SmoothDamp on the same frame.
                spring.DragLagEnabled = false;
            }

            if (draggable != null && draggable.IsDroppedInHole)
            {
                PlayHoleSquish();
                return;
            }

            if (spring == null) return;
            Vector3 dir = -lastDragMoveDir;
            spring.Kick(dir, releaseKickStrength);
        }

        /// <summary>
        /// Slow shrink-to-nothing squish for a jelly block being swallowed by a hole,
        /// instead of the real BlockHole fracture/shatter (disabled on this instance in
        /// Awake via draggable.FractureEffect = null). Runs on the same transform
        /// BlockDraggable is already moving down into the hole shaft, so it reads as
        /// "melting into the hole" rather than falling then popping.
        ///
        /// BlockDraggable.DropIntoHole already queues its own brief DOScale(originalScale,
        /// 0.06s) on this same transform (snapping the drag-pickup scale bump back to
        /// normal) right before calling onDragEnded — starting our shrink immediately would
        /// fight that tween for those 0.06s. Delay by the same amount instead of killing
        /// it, so ours cleanly takes over right after rather than racing it.
        /// </summary>
        private void PlayHoleSquish()
        {
            DOTween.Sequence()
                .SetTarget(transform)
                .AppendInterval(0.06f)
                .Append(transform.DOScale(Vector3.zero, holeSquishDuration).SetEase(holeSquishEase));
        }
    }
}
