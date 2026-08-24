using UnityEngine;

namespace Bonus.BlockHoleJelly
{
    /// <summary>
    /// Drives the "Bonus/JellyWobble" shader's per-instance _JellyDir / _JellyAmount
    /// properties with a damped spring that reacts to the object's own acceleration —
    /// sudden stops or direction changes (a drag being yanked, a snap into a hole, a
    /// collision) squash-stretch it like gelatin, then it settles back to rest.
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
        [Tooltip("How strongly the block's own velocity changes (acceleration) kick the spring. 0 = only explicit Kick() calls matter.")]
        [SerializeField] private float reactivity = 0.02f;

        [Header("Spring Tuning")]
        [Tooltip("Higher = snaps back to rest faster / feels stiffer.")]
        [SerializeField] private float stiffness = 180f;
        [Tooltip("Higher = settles with less oscillation / feels less bouncy.")]
        [SerializeField] private float damping = 12f;
        [Tooltip("Clamp on spring displacement so a big impulse can't invert or explode the mesh.")]
        [SerializeField] private float maxJellyAmount = 0.6f;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propBlock;

        private Vector3 lastPosition;
        private Vector3 velocity;

        private Vector3 springDir = Vector3.up;
        private float springAmount;
        private float springVelocity;

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

            // Passive reactivity: track the block's own acceleration and let sudden
            // changes kick the spring — no gameplay script needs to call Kick() for
            // basic drag-and-drop jiggle to work.
            if (reactivity > 0f)
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

            propBlock.SetVector(JellyDirId, springDir);
            propBlock.SetFloat(JellyAmountId, springAmount);
            meshRenderer.SetPropertyBlock(propBlock);
        }

        /// <summary>
        /// Adds an explicit impulse — call this from gameplay code (grab start, release,
        /// snap-into-hole, collision) for a deliberate, stronger punch instead of relying
        /// only on passive per-frame motion tracking.
        /// </summary>
        public void Kick(Vector3 direction, float strength)
        {
            if (direction.sqrMagnitude < 0.0001f || strength == 0f) return;
            springDir = direction.normalized;
            springVelocity += strength;
        }
    }
}
