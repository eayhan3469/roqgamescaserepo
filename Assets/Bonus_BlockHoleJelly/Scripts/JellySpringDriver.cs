using UnityEngine;

namespace Bonus.BlockHoleJelly
{
    /// <summary>
    /// Drives the "Bonus/JellyWobble" shader's per-instance _JellyDir / _JellyAmount
    /// properties. Two modes, switched by JellyBlockReactor:
    ///
    /// - Idle (DragLagEnabled = false): a damped harmonic oscillator (F = -k*x - c*v).
    ///   Kick() adds an impulse and lets it ring back to rest over several decaying
    ///   cycles — this is what makes grab/release/hole-squish moments read as jelly.
    ///   Also has optional passive per-frame acceleration reactivity (see `reactivity`).
    /// - Drag lag (DragLagEnabled = true): while actively held, continuously stretches
    ///   toward a target set every frame via SetDragLagTarget() (JellyBlockReactor feeds
    ///   this from how far the block currently sits from its snapped grid anchor — see
    ///   that script), smoothly filtered (SmoothDamp / exponential Slerp) so per-frame
    ///   noise doesn't read as chaotic spinning. This is what makes the jelly feel
    ///   continuously present *while being carried*, not just at the start/end of a drag.
    ///   Two earlier attempts didn't work: driving this off raw per-frame acceleration
    ///   (like the idle oscillator does) felt erratic so it was disabled during drag
    ///   entirely, which then had no jelly feel at all while dragging; driving it off raw
    ///   drag *velocity* instead mostly read as zero too, because BlockDraggable's own
    ///   grid-snapped elastic-lead movement holds the block nearly stationary within a
    ///   tile between discrete cell-crossing jumps — the lead-offset-from-anchor signal is
    ///   what's actually continuous while a mouse is held off-center.
    ///
    /// Uses a MaterialPropertyBlock so many blocks can all share the single
    /// Mat_JellyWobble Material asset instead of each allocating its own Material
    /// instance (cheaper, and avoids the runtime-Shader.Find + new Material() pattern
    /// that got custom shaders stripped from mobile builds elsewhere in this project —
    /// see roq-case-project memory / commit 02030bf).
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class JellySpringDriver : MonoBehaviour
    {
        [Header("Passive Motion Reactivity (idle mode only)")]
        [Tooltip("How strongly the block's own velocity changes (acceleration) kick the spring while idle (not being dragged). 0 = only explicit Kick() calls matter.")]
        [SerializeField] private float reactivity = 0.006f;

        [Header("Spring Tuning (idle mode)")]
        [Tooltip("Higher = snaps back to rest faster / feels stiffer. Controls wobble frequency (how fast it jiggles), not how long it jiggles for — that's damping.")]
        [SerializeField] private float stiffness = 180f;
        [Tooltip("Higher = settles with less oscillation / feels less bouncy. Kept low so it actually wobbles back and forth several times like real jelly instead of one quick elastic snap.")]
        [SerializeField] private float damping = 4f;
        [Tooltip("Clamp on spring displacement so a big impulse can't invert or explode the mesh. Kept below ~0.5 — higher values showed a visible seam/crack near the mesh midline even with the analytic normal recompute (the secondary ripple isn't accounted for in the normal, so it still shows at extreme stretch).")]
        [SerializeField] private float maxJellyAmount = 0.4f;

        [Header("Drag Lag (continuous feel while held)")]
        [Tooltip("Enable to switch into drag-lag mode (see class doc). JellyBlockReactor sets this on grab and clears it on release, and feeds the target every frame via SetDragLagTarget().")]
        public bool DragLagEnabled;
        [Tooltip("Clamp on the drag-lag amount so JellyBlockReactor can't push it past the safe seam-free range.")]
        [SerializeField] private float dragLagMaxAmount = 0.32f;
        [Tooltip("SmoothDamp time constant for the drag-lag amount chasing its target — lower reacts faster, higher feels more like a sluggish trailing lag.")]
        [SerializeField] private float dragLagAmountSmoothTime = 0.08f;
        [Tooltip("How fast the drag-lag stretch axis turns to follow its target direction (exponential rate, higher = snappier turning).")]
        [SerializeField] private float dragLagDirTurnRate = 15f;

        private Vector3 dragLagTargetDir = Vector3.forward;
        private float dragLagTargetAmount;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propBlock;

        private Vector3 lastPosition;
        private Vector3 velocity;

        private Vector3 springDir = Vector3.up;
        private float springAmount;
        private float springVelocity;

        /// <summary>
        /// Lets an external script (JellyBlockReactor) mute the passive per-frame
        /// reactivity while idle-mode isn't the active mode for another reason (kept
        /// distinct from DragLagEnabled so either can be toggled independently if needed).
        /// Explicit Kick() calls still always work regardless of this flag.
        /// </summary>
        public bool PassiveReactivityEnabled { get; set; } = true;

        private static readonly int JellyDirId = Shader.PropertyToID("_JellyDir");
        private static readonly int JellyAmountId = Shader.PropertyToID("_JellyAmount");

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            propBlock = new MaterialPropertyBlock();
            lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 currentPosition = transform.position;
            Vector3 currentVelocity = (currentPosition - lastPosition) / dt;
            Vector3 acceleration = (currentVelocity - velocity) / dt;
            velocity = currentVelocity;
            lastPosition = currentPosition;

            if (DragLagEnabled)
            {
                // Continuous stretch toward whatever target JellyBlockReactor last set via
                // SetDragLagTarget() — both direction and magnitude are smoothly filtered
                // so per-frame noise in that target doesn't translate into chaotic flips
                // (that's what driving this off raw per-frame acceleration did before, and
                // it read as the block spinning/glitching instead of jiggling).
                float turn = 1f - Mathf.Exp(-dragLagDirTurnRate * dt);
                springDir = Vector3.Slerp(springDir, dragLagTargetDir, turn);
                springAmount = Mathf.SmoothDamp(springAmount, dragLagTargetAmount, ref springVelocity, dragLagAmountSmoothTime, Mathf.Infinity, dt);
            }
            else
            {
                // Passive reactivity: track the block's own acceleration and let sudden
                // changes kick the spring — no gameplay script needs to call Kick() for
                // basic incidental-motion jiggle to work while idle.
                if (reactivity > 0f && PassiveReactivityEnabled && acceleration.sqrMagnitude > 0.0001f)
                {
                    // Squash into the direction it just got yanked away from.
                    Kick(-acceleration.normalized, acceleration.magnitude * reactivity);
                }

                // Damped harmonic oscillator: F = -k*x - c*v
                float force = -stiffness * springAmount - damping * springVelocity;
                springVelocity += force * dt;
                springAmount += springVelocity * dt;
                springAmount = Mathf.Clamp(springAmount, -maxJellyAmount, maxJellyAmount);
            }

            propBlock.SetVector(JellyDirId, springDir);
            propBlock.SetFloat(JellyAmountId, springAmount);
            meshRenderer.SetPropertyBlock(propBlock);
        }

        /// <summary>
        /// Sets the continuous target direction/amount for drag-lag mode — call every
        /// frame while DragLagEnabled is true. Amount is clamped to dragLagMaxAmount here;
        /// callers can pass an unclamped "how hard is this being pulled" value.
        /// </summary>
        public void SetDragLagTarget(Vector3 direction, float amount)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                dragLagTargetDir = direction.normalized;
            }
            dragLagTargetAmount = Mathf.Clamp(amount, 0f, dragLagMaxAmount);
        }

        /// <summary>
        /// Adds an explicit impulse — call this from gameplay code (grab start, release,
        /// snap-into-hole, collision) for a deliberate, stronger punch instead of relying
        /// only on passive per-frame motion tracking. Only meaningful in idle mode (while
        /// DragLagEnabled is true, springVelocity/springAmount are driven by SmoothDamp
        /// every frame instead, which would just overwrite a Kick's contribution).
        /// </summary>
        public void Kick(Vector3 direction, float strength)
        {
            if (direction.sqrMagnitude < 0.0001f || strength == 0f) return;
            springDir = direction.normalized;
            springVelocity += strength;
        }
    }
}
