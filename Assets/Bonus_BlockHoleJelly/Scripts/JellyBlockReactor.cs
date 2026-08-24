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
        [Tooltip("Kick each time the block steps to a new grid cell while being dragged — this is what makes the jelly feel present while being carried, without ever being a continuous/perpetual wobble (each step's kick rings down on its own before or as the next one arrives). Kept lower than grab/release: a fast diagonal drag fires several of these in quick succession, and JellySpringDriver.maxSpringVelocity only caps the aggregate — this keeps each individual contribution modest so a burst of steps doesn't read as excessive corner-stretch even under that cap.")]
        [SerializeField] private float gridStepKickStrength = 1.3f;
        [Tooltip("Kick on a normal release/grid-snap, opposite the direction of the last grid step. Same velocity-impulse caveat as the other kick strengths.")]
        [SerializeField] private float releaseKickStrength = 2.6f;

        [Header("Hole-Entry Squish (replaces fracture/shatter)")]
        [Tooltip("Duration of the jelly squeeze (stretching down / squashing sideways via the shader, like being pushed through a narrower opening) as the block falls into the hole. Kept roughly in sync with BlockDraggable's own holeDropDuration on this instance so the squeeze runs for the whole visible fall.")]
        [SerializeField] private float holeSquishDuration = 0.6f;
        [Tooltip("Peak jelly amount reached right as it disappears — how pinched/stretched it looks at the deepest point of the fall.")]
        [SerializeField] private float holeSquishPeakAmount = 0.45f;
        [SerializeField] private Ease holeSquishSqueezeEase = Ease.InQuad;

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

            RemoveDragOutline();
        }

        /// <summary>
        /// BlockDraggable's drag-highlight outline is a static duplicate of the block's
        /// REST-pose mesh (built once in BlockDraggable.Awake() via SetupOutline, extruded
        /// along normals by the BlockOutline shader) — it doesn't run through the jelly
        /// vertex shader, so once a block is squashed/stretched the outline visibly stops
        /// matching the block's actual silhouette, which looks broken. BlockDraggable
        /// toggles it via a hardcoded SetOutlineActive(true/false) call inside
        /// OnPointerDown/OnPointerUp/DropIntoHole — there's no event to hook to suppress
        /// just that call — so the least invasive per-instance fix is to strip the
        /// MeshRenderer off each "OutlineMesh" child right after BlockDraggable creates
        /// them. SetOutlineActive still runs and SetActive()s the (now empty) objects
        /// harmlessly every drag; there's just nothing left on them to render.
        /// </summary>
        private void RemoveDragOutline()
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "OutlineMesh") continue;
                var outlineRenderer = child.GetComponent<MeshRenderer>();
                if (outlineRenderer != null) Destroy(outlineRenderer);
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
        /// Jelly squeeze for a block being swallowed by a hole, instead of the real
        /// BlockHole fracture/shatter (disabled on this instance in Start via
        /// draggable.FractureEffect = null): stretches downward and squashes sideways via
        /// the jelly shader, growing more pinched the whole way down, while BlockDraggable's
        /// own DOMove sequence does the actual falling (already in progress by the time this
        /// runs — DropIntoHole calls onDragEnded, which reaches here, after its own drop
        /// tween is already playing). No scale animation here at all — an earlier version
        /// added an artificial DOScale(zero) shrink on top, but the user found that
        /// combination looked fake ("cok fazla kuculuyor ve yapay bir goruntu oluyor... scale
        /// kuculmek yerine gercekten asagi dogru dusmeli"): shrinking in place while also
        /// falling reads as "vanishing", not "falling in". What actually makes the block
        /// disappear is BlockDraggable's own dropSeq.OnComplete disabling its renderers once
        /// the fall finishes — real motion, not a faked shrink — so this only needs to drive
        /// the squeeze, not try to hide the block itself.
        ///
        /// BlockDraggable.DropIntoHole also queues its own brief DOScale(originalScale,
        /// 0.06s) on this same transform (snapping the drag-pickup scale bump back to
        /// normal) right before calling onDragEnded — waiting 0.06s before touching anything
        /// here avoids fighting that for those first few frames.
        /// </summary>
        private void PlayHoleSquish()
        {
            if (spring == null) return;

            // Take the shader over from the spring's own oscillator for this scripted
            // sequence — otherwise JellySpringDriver's LateUpdate would keep overwriting our
            // ForceJellyState calls with its own (by-now-irrelevant) decaying kick state.
            spring.enabled = false;

            float amount = 0f;
            DOTween.Sequence()
                .SetTarget(transform)
                .AppendInterval(0.06f)
                .Append(DOTween.To(
                    () => amount,
                    x => { amount = x; spring.ForceJellyState(Vector3.down, amount); },
                    holeSquishPeakAmount,
                    Mathf.Max(holeSquishDuration - 0.06f, 0.05f)
                ).SetEase(holeSquishSqueezeEase));
        }
    }
}
