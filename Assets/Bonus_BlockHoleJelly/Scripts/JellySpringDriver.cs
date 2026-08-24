using UnityEngine;

namespace Bonus.BlockHoleJelly
{
    /// <summary>
    /// Drives the "Bonus/JellyWobble" shader's per-instance _JellyDir / _JellyAmount
    /// properties with a damped harmonic oscillator (F = -k*x - c*v). Kick() adds an
    /// impulse and lets it ring back to rest over several decaying cycles.
    ///
    /// Deliberately event-driven only (grab / each grid step / release — see
    /// JellyBlockReactor), not continuously live while a block is held: an earlier
    /// version tried a continuous "drag lag" mode that stretched toward a live target the
    /// whole time a block was dragged, but the user found that it never settled and read
    /// as constant fluctuating rather than jelly — they wanted distinct jiggles on
    /// direction changes that ring down, not a perpetual wobble. Kick()-only give exactly
    /// that: each impulse decays to (very near) zero within roughly a second, so the block
    /// only "jiggles" right after something actually happened to it.
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
        [Header("Passive Motion Reactivity")]
        [Tooltip("How strongly the block's own velocity changes (acceleration) kick the spring on top of explicit Kick() calls. JellyBlockReactor disables this while a block is actively being dragged (BlockDraggable's own drag-follow tween changes direction every frame, which fed through here reads as chaotic wobbling rather than jelly) and re-enables it once released. 0 = only explicit Kick() calls matter.")]
        [SerializeField] private float reactivity = 0.006f;
        public bool PassiveReactivityEnabled { get; set; } = true;

        [Header("Spring Tuning")]
        [Tooltip("Higher = snaps back to rest faster / feels stiffer. Controls wobble frequency (how fast it jiggles), not how long it jiggles for — that's damping.")]
        [SerializeField] private float stiffness = 180f;
        [Tooltip("Higher = settles with less oscillation / feels less bouncy. Kept low so it actually wobbles back and forth several times like real jelly instead of one quick elastic snap.")]
        [SerializeField] private float damping = 4f;
        [Tooltip("Clamp on spring displacement so a big impulse can't invert or explode the mesh. Kept below ~0.5 — higher values showed a visible seam/crack near the mesh midline even with the analytic normal recompute (the secondary ripple isn't accounted for in the normal, so it still shows at extreme stretch).")]
        [SerializeField] private float maxJellyAmount = 0.4f;
        [Tooltip("Clamp on springVelocity itself (separate from maxJellyAmount, which only clamps position). A fast diagonal drag fires several grid-step Kicks in quick succession (see JellyBlockReactor.OnGridStep), and Kick() strengths simply add — without this, that stacked velocity can massively overshoot maxJellyAmount's *position* clamp, which keeps the block pinned at max stretch for an extended time while the excess velocity bleeds off (looked like \"too much stretching\" to the user) instead of naturally peaking below the clamp and settling right away. Note peak natural amplitude works out to roughly maxSpringVelocity / sqrt(stiffness) — at stiffness=180 (sqrt≈13.4), 3 -> ~0.22, well clear of maxJellyAmount's 0.4 ceiling, so a stacked-kick drag no longer even approaches the position clamp.")]
        [SerializeField] private float maxSpringVelocity = 3f;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propBlock;

        private Vector3 lastPosition;
        private Vector3 velocity;

        private Vector3 springDir = Vector3.up;
        private float springAmount;
        private float springVelocity;

        private static readonly int JellyDirId = Shader.PropertyToID("_JellyDir");
        private static readonly int JellyAmountId = Shader.PropertyToID("_JellyAmount");
        private static readonly int JellyPivotOffsetId = Shader.PropertyToID("_JellyPivotOffset");

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            propBlock = new MaterialPropertyBlock();
            lastPosition = transform.position;

            // Tell the shader where this mesh's actual center is in local space, so it
            // deforms around that instead of raw object-space origin — matters whenever the
            // mesh's pivot isn't at its own centroid, e.g. BlockHole's multi-cell combined
            // meshes (an L-tetromino's pivot sits at one corner of its footprint, not the
            // middle), which otherwise looked like the block exploding/jumping in size. Set
            // once here (not every frame) since it never changes at runtime for a given mesh.
            var meshFilter = GetComponent<MeshFilter>();
            Vector3 pivotOffset = Vector3.zero;
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                pivotOffset = meshFilter.sharedMesh.bounds.center;
            }
            propBlock.SetVector(JellyPivotOffsetId, pivotOffset);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Passive reactivity: track the block's own acceleration and let sudden
            // changes kick the spring — no gameplay script needs to call Kick() for
            // basic incidental-motion jiggle to work while idle.
            if (reactivity > 0f && PassiveReactivityEnabled)
            {
                Vector3 currentVelocity = (transform.position - lastPosition) / dt;
                Vector3 acceleration = (currentVelocity - velocity) / dt;
                velocity = currentVelocity;
                lastPosition = transform.position;

                if (acceleration.sqrMagnitude > 0.0001f)
                {
                    // Squash into the direction it just got yanked away from.
                    Kick(-acceleration.normalized, acceleration.magnitude * reactivity);
                }
            }
            else
            {
                lastPosition = transform.position;
            }

            // Damped harmonic oscillator: F = -k*x - c*v
            float force = -stiffness * springAmount - damping * springVelocity;
            springVelocity += force * dt;
            springAmount += springVelocity * dt;
            springAmount = Mathf.Clamp(springAmount, -maxJellyAmount, maxJellyAmount);

            // Send the shader the RECTIFIED (always >= 0) amount, not the signed spring
            // value. The underlying spring still swings positive/negative internally (that's
            // what makes it ring down naturally) — but a negative amount stretches the
            // mesh along the *perpendicular* axes instead of springDir (see
            // JellyWobble.shader's squash/stretch), i.e. every other half-cycle visually
            // flips to being stretched sideways instead of toward springDir. From a
            // top-down camera that reads as "just pulsing bigger/smaller" rather than
            // "jiggling toward the direction it was dragged", which is what the user
            // actually wants. Rectifying keeps the stretch always along springDir, pulsing
            // in intensity (touching the rest cube shape between pulses) instead of
            // alternating axis — a clean directional "heartbeat" jiggle.
            propBlock.SetVector(JellyDirId, springDir);
            propBlock.SetFloat(JellyAmountId, Mathf.Abs(springAmount));
            meshRenderer.SetPropertyBlock(propBlock);
        }

        /// <summary>
        /// Adds an explicit impulse — call this from gameplay code (grab start, grid step,
        /// release, snap-into-hole) for a deliberate punch that then rings down on its own.
        /// NOTE: strength is a velocity impulse, not a direct displacement — the resulting
        /// peak wobble amplitude works out to roughly strength / sqrt(stiffness), not
        /// strength itself. Re-derive that if you change stiffness.
        /// </summary>
        public void Kick(Vector3 direction, float strength)
        {
            if (direction.sqrMagnitude < 0.0001f || strength == 0f) return;
            springDir = direction.normalized;
            springVelocity = Mathf.Clamp(springVelocity + strength, -maxSpringVelocity, maxSpringVelocity);
        }
    }
}
