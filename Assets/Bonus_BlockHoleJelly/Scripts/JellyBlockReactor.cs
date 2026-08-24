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
        [Tooltip("Kick strength for the hole-entry squeeze (same velocity-impulse mechanism as grab/release/grid-step — see JellySpringDriver.Kick). Deliberately the strongest of the four: it is the one moment squeeze-then-relax should read as the most pronounced. Reuses the normal damped oscillator instead of a scripted ramp, so it naturally squeezes in hard and eases back off on its own rather than holding at a flat peak.")]
        [SerializeField] private float holeSquishKickStrength = 5.5f;

        [Header("Hole-Entry Splash VFX")]
        [Tooltip("Burst particle system (a child named 'HoleSplashVFX', set up per block instance) that fires when the block is squeezed into a hole — small colored droplets flung outward from the block's edges, gravity-pulled down, mimicking juice/liquid squirting out under the squeeze. Found automatically in Awake via child name; safe to leave null if a given instance does not have one.")]
        private ParticleSystem holeSplashVFX;

        [Tooltip("Second particle system (a child named 'HoleSplashMistVFX') layered on top of holeSplashVFX — many more, much smaller and shorter-lived flecks scattering more widely, the fine-spray counterpart to the bigger droplets. One particle system alone reads as a handful of discrete blobs, not a juicy squirt; this second finer layer is what actually sells 'juicy'. Played alongside holeSplashVFX, same trigger moment.")]
        private ParticleSystem holeSplashMistVFX;

        [Header("Hole-Entry Camera Punch")]
        [Tooltip("Camera.main.DOShakePosition duration on hole entry — same DOTween call BlockFractureEffect.cs uses for the real shatter shake (Case2_BlockHole/Scripts/BlockFractureEffect.cs), reused here directly rather than reinventing it, since jelly blocks removed that component and lost its shake entirely. Kept in the same ballpark as the real fracture shake (0.14s/0.12/16) but a touch punchier since a splash is the whole payoff moment now, not one of several fracture beats.")]
        [SerializeField] private float cameraShakeDuration = 0.16f;
        [Tooltip("Shake strength (world-unit position offset amplitude).")]
        [SerializeField] private float cameraShakeStrength = 0.16f;
        [Tooltip("Shake vibrato (number of shake cycles over the duration) — higher reads as a sharper rattle, lower as a softer wobble.")]
        [SerializeField] private int cameraShakeVibrato = 18;

        private JellySpringDriver spring;
        private BlockDraggable draggable;

        private Vector2Int lastAnchor;
        private Vector3 lastStepDir = Vector3.forward;

        // Cached at grab time — see PlayHoleSquish for why this must NOT be re-resolved via
        // GridManager.GetMatchingHoleForBlock(draggable) later.
        private BlockHole.HoleTarget cachedHole;

        private void Awake()
        {
            spring = GetComponentInChildren<JellySpringDriver>(true);
            draggable = GetComponent<BlockDraggable>();

            if (spring == null)
            {
                Debug.LogWarning($"JellyBlockReactor on '{name}' found no JellySpringDriver in itself or its children — add one to whichever object holds the real MeshRenderer.", this);
            }

            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "HoleSplashVFX")
                {
                    holeSplashVFX = t.GetComponent<ParticleSystem>();
                }
                else if (t.name == "HoleSplashMistVFX")
                {
                    holeSplashMistVFX = t.GetComponent<ParticleSystem>();
                }
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

                // Cache the matching hole now, while the block is still draggable — see
                // PlayHoleSquish for why re-resolving this later does not work.
                cachedHole = BlockHole.GridManager.Instance != null
                    ? BlockHole.GridManager.Instance.GetMatchingHoleForBlock(draggable)
                    : null;
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
            // Check the hole-drop case FIRST, before touching PassiveReactivityEnabled —
            // it must stay false into PlayHoleSquish(). Re-enabling it here unconditionally
            // (the previous order) meant that while BlockDraggable's own dropSeq was moving
            // the block down into the hole, the passive system saw that real fall
            // acceleration and kept firing its own extra Kicks on top of PlayHoleSquish's
            // deliberate signed bulge-then-squeeze one, scrambling it — traced via Unity MCP:
            // the amount trajectory came out positive-first even though the hole-kick itself
            // measured a correct negative springVelocity immediately after being applied.
            if (draggable != null && draggable.IsDroppedInHole)
            {
                PlayHoleSquish();
                return;
            }

            if (spring != null)
            {
                spring.PassiveReactivityEnabled = true;
            }

            if (spring == null) return;
            spring.Kick(-lastStepDir, releaseKickStrength);
        }

        /// <summary>
        /// Jelly squeeze for a block being swallowed by a hole, instead of the real
        /// BlockHole fracture/shatter (disabled on this instance in Start via
        /// draggable.FractureEffect = null): a single strong Kick(down) through the normal
        /// spring oscillator, so it squeezes in hard and then visibly relaxes/settles back
        /// on its own — same "squeeze then relax" physics as every other Kick moment, just
        /// the strongest one — instead of holding at a flat scripted peak. BlockDraggable's
        /// own DOMove sequence does the actual falling (already in progress by the time this
        /// runs — DropIntoHole calls onDragEnded, which reaches here, after its own drop
        /// tween is already playing) and its own dropSeq.OnComplete disabling renderers is
        /// still what makes the block actually disappear — this only drives the squeeze, see
        /// commit e347f01 for why no scale animation happens here at all.
        ///
        /// Also force-closes every effect around the hole immediately instead of leaving
        /// BlockDraggable's own non-immediate SetHighlight(false) call to fade them out —
        /// that fade was still visibly lingering during the jelly entry, which the user
        /// wanted gone. Two steps, since one alone was not enough: HoleTarget.SetHighlight
        /// (false, true) for the edge glow, PLUS a direct Stop(StopEmittingAndClear) on
        /// every ParticleSystem under the hole — HoleTarget's own SetHighlight only stops
        /// particle systems it cached in Awake AND excludes anything literally named
        /// "EdgeGlowParticles"; live-testing found "RimOutlineParticles" and
        /// "WaterfallParticles" still had 30-100+ live particles right after SetHighlight
        /// (false, true) returned, so those clearly are not the ones it is managing. Purely
        /// additive: only calls public HoleTarget/ParticleSystem API, does not modify
        /// BlockDraggable.cs or HoleTarget.cs.
        /// </summary>
        private void PlayHoleSquish()
        {
            // Deliberately uses cachedHole (captured in OnGrabbed), NOT a fresh
            // GridManager.GetMatchingHoleForBlock(draggable) lookup here — found the hard
            // way that a fresh lookup at this point returns null: BlockDraggable.DropIntoHole
            // calls hole.SetFilled(true) BEFORE firing onDragEnded (which is what reaches
            // this method), and GetMatchingHoleForBlock only matches unfilled holes, so by
            // the time this runs the hole this exact block is falling into no longer counts
            // as a match. Confirmed via Unity MCP: the jelly squeeze and the hole-highlight
            // force-close both happened to look correct anyway (BlockDraggable's own
            // un-immediate SetHighlight(false) call already turns the highlight off, just
            // not instantly), which is what hid this — but the particle-clear silently never
            // ran, since it was gated on that same now-null lookup, leaving up to a few
            // hundred already-emitted RimOutlineParticles/WaterfallParticles frozen in place
            // instead of clearing.
            if (cachedHole != null)
            {
                cachedHole.SetHighlight(false, true);
                var holeParticles = cachedHole.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in holeParticles)
                {
                    if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            // Juicy squirt: a burst of small colored droplets flung outward from the block's
            // edges right as the squeeze begins, gravity-pulled down over their short
            // lifetime — reads as liquid being squeezed out from under pressure, timed with
            // the squeeze kick below rather than the moment it actually vanishes (by then it
            // is below the floor and the splash would not be visible against the board).
            if (holeSplashVFX != null)
            {
                holeSplashVFX.Play();
            }
            if (holeSplashMistVFX != null)
            {
                holeSplashMistVFX.Play();
            }

            // Sustained "being absorbed" sound, running through the whole fall rather than a
            // single impact punctuation. Routed through BlockHoleAudioManager.PlayAbsorbSound
            // (its absorbClip field) instead of a dedicated source on this component — a
            // small, purely-additive extension to the shared Case2 audio manager (new field +
            // method, no existing behavior touched, no-ops if absorbClip is left unassigned)
            // so this scene's clip can live in the same place/pattern as every other jelly SFX
            // cue and be swapped/tested from its Inspector like the rest.
            //
            // Passes draggable.HoleDropDuration (the real, possibly per-instance-overridden
            // fall time) as the sync target, so PlayAbsorbSound stretches/compresses whichever
            // clip is currently assigned to finish exactly when the block visually vanishes,
            // instead of the clip's own fixed natural length running short or lingering after.
            if (BlockHoleAudioManager.Instance != null)
            {
                float fallDuration = draggable != null ? draggable.HoleDropDuration : 0f;
                BlockHoleAudioManager.Instance.PlayAbsorbSound(fallDuration);
            }

            // Camera punch — the same DOTween call BlockFractureEffect.cs uses for the real
            // shatter shake, called directly here since jelly blocks have that component
            // removed and would otherwise have zero camera feedback on hole entry at all.
            // DOComplete() first for the same reason BlockFractureEffect does it: two blocks
            // landing in quick succession would otherwise stack/queue shakes instead of the
            // second one restarting cleanly.
            if (Camera.main != null)
            {
                Camera.main.DOComplete();
                Camera.main.DOShakePosition(cameraShakeDuration, cameraShakeStrength, cameraShakeVibrato);
            }

            if (spring == null) return;

            // Signed (not rectified) so the spring's natural swing tells a real two-phase
            // story instead of always reading as "squeezed inward": a NEGATIVE kick makes
            // springAmount dip negative first (squash along down-axis / bulge sideways — as
            // if the leading edge hit resistance and the still-above-ground top widens from
            // inertia), swing through zero into positive (stretch down / squeeze inward — the
            // top now being pulled through the narrower opening), then relax back to rest.
            // Same spring, same tuning, just letting both halves of one honest oscillation
            // show instead of folding them onto one side.
            spring.RectifyAmount = false;
            spring.Kick(Vector3.down, -holeSquishKickStrength);
        }
    }
}
