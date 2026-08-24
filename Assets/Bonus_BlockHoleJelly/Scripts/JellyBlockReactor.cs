using UnityEngine;
using DG.Tweening;
using BlockHole;

namespace Bonus.BlockHoleJelly
{
    /// <summary>
    /// Bridges the real BlockHole gameplay flow (BlockDraggable's public UnityEvents) to
    /// JellySpringDriver.Kick(), so the jelly reacts to actual discrete game moments:
    /// grabbing a block, each time it steps to a new grid cell while being dragged,
    /// releasing it normally, or it getting swallowed by a hole (a slow shrink-to-nothing
    /// squish instead of the real BlockFractureEffect shatter — see PlayHoleSquish).
    ///
    /// Deliberately event-driven only, not a continuous per-frame effect — an earlier
    /// version tried a continuous "drag lag" stretch that tracked a live target the whole
    /// time a block was held, but the user found it never settled and read as constant
    /// fluctuating rather than jelly, plus (a separate bug at the time) it could distort
    /// into a diagonal parallelogram. Discrete Kicks on grab/step/release each ring down
    /// on their own via JellySpringDriver's damped oscillator, which is what actually
    /// reads as "jelly, then settles" rather than "always wobbling".
    ///
    /// Wire this up in the Editor (or via UnityEditor.Events.UnityEventTools in an editor
    /// script) by pointing BlockDraggable's `onDragStarted` at OnGrabbed(),
    /// `onGridPositionChanged` at OnGridStep(Vector2Int), and `onDragEnded` at
    /// OnReleased(). `onDroppedInHole` is deliberately NOT used — by the time
    /// BlockDraggable fires it, the block's renderers are already disabled (it fires after
    /// the fracture effect triggers), so a jelly reaction at that point would never be
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
        [Tooltip("Kick each time the block steps to a new grid cell while being dragged — this is what makes the jelly feel present while being carried, without ever being a continuous/perpetual wobble (each step's kick rings down on its own before or as the next one arrives).")]
        [SerializeField] private float gridStepKickStrength = 1.8f;
        [Tooltip("Kick on a normal release/grid-snap, opposite the direction of the last grid step. Same velocity-impulse caveat as the other kick strengths.")]
        [SerializeField] private float releaseKickStrength = 2.6f;

        [Header("Hole-Entry Squish (replaces fracture/shatter)")]
        [Tooltip("Duration of the shrink-to-nothing squish when the block is swallowed by a hole. Kept roughly in sync with BlockDraggable's own holeDropDuration on this instance so the block finishes shrinking right as BlockDraggable disables its renderers — tune both together.")]
        [SerializeField] private float holeSquishDuration = 0.6f;
        [SerializeField] private Ease holeSquishEase = Ease.InQuad;

        private JellySpringDriver spring;
        private BlockDraggable draggable;

        private Vector2Int lastAnchor;
        private Vector3 lastStepDir = Vector3.forward;

        private void Awake()
        {
            spring = GetComponentInChildren<JellySpringDriver>(true);
            draggable = GetComponent<BlockDraggable>();

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
                lastAnchor = draggable.CurrentAnchorGridPos;
            }
        }

        /// <summary>Wire to BlockDraggable.onDragStarted.</summary>
        public void OnGrabbed()
        {
            if (draggable != null)
            {
                lastAnchor = draggable.CurrentAnchorGridPos;
            }

            if (spring == null) return;
            // Passive per-frame reactivity would otherwise re-kick every frame off
            // BlockDraggable's own drag-follow tween jitter, which read as chaotic
            // wobbling rather than jelly — off for the duration of the drag, explicit
            // Kicks (this, OnGridStep, OnReleased) are the only source of jelly while held.
            spring.PassiveReactivityEnabled = false;
            spring.Kick(Vector3.up, grabKickStrength);
        }

        /// <summary>
        /// Wire to BlockDraggable.onGridPositionChanged — fires each time the block steps
        /// to a new grid cell while being dragged. This is the "feel it while carrying"
        /// moment: a Kick per step, in the direction of that step, which rings down on its
        /// own via the spring before (or as) the next step's Kick arrives.
        /// </summary>
        public void OnGridStep(Vector2Int newAnchor)
        {
            Vector2Int delta = newAnchor - lastAnchor;
            lastAnchor = newAnchor;
            if (spring == null || delta == Vector2Int.zero) return;

            Vector3 dir = new Vector3(delta.x, 0f, delta.y);
            spring.Kick(dir, gridStepKickStrength);
            lastStepDir = dir.normalized;
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
                spring.PassiveReactivityEnabled = true;
            }

            if (draggable != null && draggable.IsDroppedInHole)
            {
                PlayHoleSquish();
                return;
            }

            if (spring == null) return;
            spring.Kick(-lastStepDir, releaseKickStrength);
        }

        /// <summary>
        /// Slow shrink-to-nothing squish for a jelly block being swallowed by a hole,
        /// instead of the real BlockHole fracture/shatter (disabled on this instance in
        /// Start via draggable.FractureEffect = null). Runs on the same transform
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
