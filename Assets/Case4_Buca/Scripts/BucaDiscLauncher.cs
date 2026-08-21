using System;
using UnityEngine;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Buca
{
    public class BucaDiscLauncher : MonoBehaviour
    {
        public static BucaDiscLauncher Instance { get; private set; }

        [Header("Disc Reference")]
        [Tooltip("The puck/disc transform to launch.")]
        [SerializeField] private Transform discTransform;

        [Header("Slingshot & Aim Settings")]
        [Tooltip("Maximum drag distance in world units to reach 100% power.")]
        [SerializeField] private float maxDragDistance = 3.5f;

        [Tooltip("Distance from touch center below which the shot is CANCELLED.")]
        [SerializeField] private float cancelThresholdDistance = 0.45f;

        [Tooltip("Maximum launch impulse force at 100% pull.")]
        [SerializeField] private float maxLaunchForce = 34.0f;

        [Tooltip("Minimum launch impulse force.")]
        [SerializeField] private float minLaunchForce = 8.0f;

        [Tooltip("Base forward direction along the launch track.")]
        [SerializeField] private Vector3 baseForwardDir = new Vector3(0f, 0f, 1f);

        [Header("Forward Aim Indicator (At Disc)")]
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private float minIndicatorLength = 1.2f;
        [SerializeField] private float maxIndicatorLength = 5.5f;
        [SerializeField] private Color indicatorColorLow = new Color(0.2f, 0.85f, 1.0f, 0.95f);
        [SerializeField] private Color indicatorColorHigh = new Color(1.0f, 0.85f, 0.2f, 1.0f);

        [Header("Touch Pivot Ring (At Touch Origin)")]
        [SerializeField] private LineRenderer touchOriginRing;
        [SerializeField] private float ringRadius = 0.65f;
        [SerializeField] private Color ringActiveColor = new Color(0.3f, 0.9f, 1.0f, 0.85f);
        [SerializeField] private Color ringCancelColor = new Color(1.0f, 0.35f, 0.35f, 0.5f);

        [Header("Wall Impact Friction Spin Settings (Exact 2-Turn Physics)")]
        [Tooltip("Exact radius of the visual mesh (0.58m from FBX).")]
        [SerializeField] private float discRadius = 0.58f;

        [Tooltip("Base spin rate added on solid wall hit (in degrees/sec).")]
        [SerializeField] private float wallImpactSpinSpeed = 700.0f;

        [Tooltip("Maximum visual spin speed cap in degrees/sec.")]
        [SerializeField] private float maxSpinSpeedCap = 1000.0f;

        [Tooltip("Smooth spin deceleration rate in degrees/sec^2.")]
        [SerializeField] private float spinDeceleration = 320.0f;

        [Header("PhysX Bounce & Friction Properties")]
        [SerializeField] private float dynamicFriction = 0.05f;
        [SerializeField] private float staticFriction = 0.05f;
        [SerializeField] private float bounciness = 0.75f;
        [SerializeField] private float linearDamping = 0.15f;

        [Header("Reset Delay")]
        [SerializeField] private float autoResetDelay = 4.5f;

        private Rigidbody discRb;
        private SphereCollider discCollider;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        private bool isAiming = false;
        private bool isLaunched = false;
        private bool isCanceling = false;
        private Vector3 dragStartWorldPos;
        private Vector3 currentLaunchDir = Vector3.forward;
        private float currentPowerRatio = 0f;
        private Camera mainCam;
        private float resetTimer = 0f;
        private Plane groundPlane;

        // Strict speed cap to prevent wall acceleration
        private float maxAllowedSpeed = 0f;
        private Vector3 preCollisionVelocity = Vector3.zero;

        // Dedicated visual & physical spin integration
        private float currentVisualAngleY = 0f;
        private float currentSpinSpeedY = 0f;

        public bool IsAiming => isAiming;
        public bool IsLaunched => isLaunched;
        public bool IsCanceling => isCanceling;
        public Transform DiscTransform => discTransform;
        public float CurrentSpinSpeedY => currentSpinSpeedY;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(this);

            mainCam = Camera.main;

            FindAndSetupDisc();
            SetupPhysXMaterials();
            SetupAimLine();
            SetupTouchVisualizers();
        }

        private void Start()
        {
            if (discTransform != null)
            {
                discTransform.SetParent(null, true);

                spawnPosition = discTransform.position;
                spawnRotation = discTransform.rotation;
                currentVisualAngleY = spawnRotation.eulerAngles.y;
                groundPlane = new Plane(Vector3.up, spawnPosition);
            }
        }

        private void FindAndSetupDisc()
        {
            if (discTransform == null)
            {
                GameObject discGo = GameObject.Find("disc");
                if (discGo != null) discTransform = discGo.transform;
            }

            if (discTransform == null)
            {
                foreach (var t in FindObjectsOfType<Transform>(true))
                {
                    if (t.name.ToLower() == "disc")
                    {
                        discTransform = t;
                        break;
                    }
                }
            }

            if (discTransform == null)
            {
                Debug.LogWarning("[BucaDiscLauncher] disc Transform not found in scene!");
                return;
            }

            // Attach collision relay component directly to disc so Unity collision messages are 100% delivered!
            if (discTransform.GetComponent<BucaDiscCollisionRelay>() == null)
            {
                discTransform.gameObject.AddComponent<BucaDiscCollisionRelay>();
            }

            discRb = discTransform.GetComponent<Rigidbody>();
            if (discRb == null) discRb = discTransform.gameObject.AddComponent<Rigidbody>();

            discRb.mass = 1.0f;
            discRb.linearDamping = linearDamping;
            discRb.angularDamping = 0.1f;
            discRb.useGravity = false;
            discRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            discRb.interpolation = RigidbodyInterpolation.Interpolate;

            discRb.constraints = RigidbodyConstraints.FreezePositionY |
                                 RigidbodyConstraints.FreezeRotationX |
                                 RigidbodyConstraints.FreezeRotationZ;

            discRb.isKinematic = true;

            discCollider = discTransform.GetComponent<SphereCollider>();
            if (discCollider == null)
            {
                var mc = discTransform.GetComponent<MeshCollider>();
                if (mc != null) Destroy(mc);

                discCollider = discTransform.gameObject.AddComponent<SphereCollider>();
            }

            discCollider.radius = discRadius;
            discCollider.center = new Vector3(0f, 0.25f, 0f);

            PhysicsMaterial puckPhysMat = new PhysicsMaterial("BucaPuckMat")
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Multiply
            };
            discCollider.material = puckPhysMat;

            if (!discTransform.CompareTag("Player"))
            {
                discTransform.gameObject.tag = "Player";
            }
        }

        private void SetupPhysXMaterials()
        {
            PhysicsMaterial wallPhysMat = new PhysicsMaterial("BucaWallMat")
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Multiply
            };

            MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                if (r.gameObject == (discTransform != null ? discTransform.gameObject : null)) continue;

                string lower = r.gameObject.name.ToLower();
                if (lower.Contains("frame") || lower.Contains("level") || lower.Contains("wall") || lower.Contains("track"))
                {
                    Collider col = r.GetComponent<Collider>();
                    if (col != null) col.material = wallPhysMat;
                }
                else if (lower.Contains("obstacle"))
                {
                    Collider col = r.GetComponent<Collider>();
                    if (col != null) col.material = wallPhysMat;
                }
            }
        }

        private void SetupAimLine()
        {
            if (aimLine == null)
            {
                aimLine = GetComponent<LineRenderer>();
                if (aimLine == null) aimLine = gameObject.AddComponent<LineRenderer>();
            }

            aimLine.positionCount = 3;
            aimLine.useWorldSpace = true;
            aimLine.enabled = false;

            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0f, 0.28f);
            widthCurve.AddKey(0.75f, 0.22f);
            widthCurve.AddKey(0.85f, 0.40f);
            widthCurve.AddKey(1f, 0.02f);
            aimLine.widthCurve = widthCurve;
            aimLine.widthMultiplier = 1.0f;

            Material lineMat = new Material(Shader.Find("Sprites/Default"));
            aimLine.material = lineMat;
        }

        private void SetupTouchVisualizers()
        {
            if (touchOriginRing == null)
            {
                GameObject ringGo = new GameObject("TouchOriginRing");
                ringGo.transform.SetParent(transform);
                touchOriginRing = ringGo.AddComponent<LineRenderer>();
            }

            int segments = 36;
            touchOriginRing.positionCount = segments + 1;
            touchOriginRing.loop = true;
            touchOriginRing.useWorldSpace = true;
            touchOriginRing.startWidth = 0.06f;
            touchOriginRing.endWidth = 0.06f;
            touchOriginRing.material = new Material(Shader.Find("Sprites/Default"));
            touchOriginRing.enabled = false;
        }

        private void Update()
        {
            HandleInput();

            if (isLaunched && discTransform != null)
            {
                // Accumulate rotational spin around Y with clean linear damping
                if (Mathf.Abs(currentSpinSpeedY) > 0.5f)
                {
                    currentVisualAngleY += currentSpinSpeedY * Time.deltaTime;
                    currentSpinSpeedY = Mathf.MoveTowards(currentSpinSpeedY, 0f, spinDeceleration * Time.deltaTime);
                }
                else
                {
                    currentSpinSpeedY = 0f;
                }

                // Apply rotation strictly flat in the XZ plane
                Quaternion targetRot = Quaternion.Euler(0f, currentVisualAngleY, 0f);
                discTransform.rotation = targetRot;

                if (discRb != null && !discRb.isKinematic)
                {
                    discRb.MoveRotation(targetRot);
                }

                // Auto reset when stopped or timeout
                resetTimer += Time.deltaTime;
                if (resetTimer > autoResetDelay || (resetTimer > 1.5f && discRb != null && discRb.linearVelocity.magnitude < 0.25f))
                {
                    ResetDisc();
                }
            }
        }

        private void FixedUpdate()
        {
            if (!isLaunched || discRb == null) return;

            Vector3 vel = discRb.linearVelocity;

            // Lock Y velocity strictly to 0
            vel.y = 0f;

            // Strict energy conservation (speed can never exceed maxAllowedSpeed)
            if (vel.magnitude > maxAllowedSpeed)
            {
                vel = vel.normalized * maxAllowedSpeed;
            }

            discRb.linearVelocity = vel;

            // Continuous decay of max speed via linear damping
            maxAllowedSpeed = Mathf.Max(0f, maxAllowedSpeed - 0.2f * Time.fixedDeltaTime);

            // Cache velocity before the next collision step
            preCollisionVelocity = vel;
        }

        private void HandleInput()
        {
            if (isLaunched || discTransform == null) return;

#if ENABLE_INPUT_SYSTEM
            Vector2 screenPos = Vector2.zero;
            bool pointerDown = false;
            bool pointerHeld = false;
            bool pointerUp = false;

            if (Mouse.current != null)
            {
                screenPos = Mouse.current.position.ReadValue();
                pointerDown = Mouse.current.leftButton.wasPressedThisFrame;
                pointerHeld = Mouse.current.leftButton.isPressed;
                pointerUp = Mouse.current.leftButton.wasReleasedThisFrame;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
                pointerDown = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
                pointerHeld = Touchscreen.current.primaryTouch.press.isPressed;
                pointerUp = Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
            }
#else
            Vector2 screenPos = Input.mousePosition;
            bool pointerDown = Input.GetMouseButtonDown(0);
            bool pointerHeld = Input.GetMouseButton(0);
            bool pointerUp = Input.GetMouseButtonUp(0);
#endif

            if (pointerDown)
            {
                if (GetGroundPoint(screenPos, out Vector3 hitPoint))
                {
                    StartAiming(hitPoint);
                }
            }
            else if (pointerHeld && isAiming)
            {
                if (GetGroundPoint(screenPos, out Vector3 hitPoint))
                {
                    UpdateAiming(hitPoint);
                }
            }
            else if (pointerUp && isAiming)
            {
                ReleaseAndLaunch();
            }
        }

        private bool GetGroundPoint(Vector2 screenPos, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return false;

            Ray ray = mainCam.ScreenPointToRay(screenPos);
            if (groundPlane.Raycast(ray, out float enter))
            {
                worldPoint = ray.GetPoint(enter);
                return true;
            }
            return false;
        }

        private void StartAiming(Vector3 worldPoint)
        {
            isAiming = true;
            isCanceling = true;
            dragStartWorldPos = worldPoint;
            currentPowerRatio = 0f;
            currentLaunchDir = baseForwardDir.normalized;

            discTransform.position = spawnPosition;
            discTransform.rotation = spawnRotation;
            currentVisualAngleY = spawnRotation.eulerAngles.y;

            DrawOriginRing(dragStartWorldPos, ringCancelColor);

            if (aimLine != null) aimLine.enabled = false;
        }

        private void UpdateAiming(Vector3 currentWorldPos)
        {
            Vector3 dragVector = currentWorldPos - dragStartWorldPos;
            dragVector.y = 0f;

            float dragDist = dragVector.magnitude;

            // Check cancel zone
            if (dragDist < cancelThresholdDistance)
            {
                isCanceling = true;
                currentPowerRatio = 0f;

                DrawOriginRing(dragStartWorldPos, ringCancelColor);

                if (aimLine != null) aimLine.enabled = false;

                discTransform.rotation = spawnRotation;
                currentVisualAngleY = spawnRotation.eulerAngles.y;
                return;
            }

            // Valid aim outside cancel zone
            isCanceling = false;
            float effectiveDrag = dragDist - cancelThresholdDistance;
            currentPowerRatio = Mathf.Clamp01(effectiveDrag / (maxDragDistance - cancelThresholdDistance));

            // Full 360 aim: pulling backwards aims forward
            Vector3 aimVector = -dragVector;
            currentLaunchDir = aimVector.normalized;

            currentVisualAngleY = Quaternion.LookRotation(currentLaunchDir, Vector3.up).eulerAngles.y;
            discTransform.rotation = Quaternion.Euler(0f, currentVisualAngleY, 0f);

            DrawOriginRing(dragStartWorldPos, ringActiveColor);

            if (aimLine != null)
            {
                aimLine.enabled = true;
                UpdateAimIndicator(currentPowerRatio, currentLaunchDir);
            }
        }

        private void DrawOriginRing(Vector3 center, Color color)
        {
            if (touchOriginRing == null) return;
            touchOriginRing.enabled = true;

            int segments = 36;
            touchOriginRing.positionCount = segments + 1;
            touchOriginRing.startColor = color;
            touchOriginRing.endColor = color;

            float angleStep = 360f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float rad = i * angleStep * Mathf.Deg2Rad;
                Vector3 pt = center + new Vector3(Mathf.Cos(rad) * ringRadius, 0.08f, Mathf.Sin(rad) * ringRadius);
                touchOriginRing.SetPosition(i, pt);
            }
        }

        private void UpdateAimIndicator(float powerRatio, Vector3 direction)
        {
            if (aimLine == null || !aimLine.enabled) return;

            float lineLength = Mathf.Lerp(minIndicatorLength, maxIndicatorLength, powerRatio);

            Vector3 startPos = spawnPosition + direction * 0.45f;
            startPos.y = spawnPosition.y + 0.12f;

            Vector3 midPos = startPos + direction * (lineLength * 0.72f);
            Vector3 tipPos = startPos + direction * lineLength;

            aimLine.positionCount = 3;
            aimLine.SetPosition(0, startPos);
            aimLine.SetPosition(1, midPos);
            aimLine.SetPosition(2, tipPos);

            Color activeColor = Color.Lerp(indicatorColorLow, indicatorColorHigh, powerRatio);
            aimLine.startColor = activeColor;
            aimLine.endColor = new Color(activeColor.r, activeColor.g, activeColor.b, 0.95f);
        }

        private void ReleaseAndLaunch()
        {
            isAiming = false;
            HideVisualizers();

            if (isCanceling || currentPowerRatio <= 0.001f)
            {
                discTransform.position = spawnPosition;
                discTransform.rotation = spawnRotation;
                currentVisualAngleY = spawnRotation.eulerAngles.y;
                Debug.Log("[BucaDiscLauncher] Shot Canceled.");
                return;
            }

            float launchForce = Mathf.Lerp(minLaunchForce, maxLaunchForce, currentPowerRatio);
            isLaunched = true;
            resetTimer = 0f;
            currentSpinSpeedY = 0f;
            maxAllowedSpeed = launchForce;

            discRb.isKinematic = false;
            discRb.useGravity = false;
            discRb.linearVelocity = Vector3.zero;
            discRb.angularVelocity = Vector3.zero;

            Vector3 impulse = currentLaunchDir.normalized * launchForce;
            discRb.AddForce(impulse, ForceMode.Impulse);
            preCollisionVelocity = impulse;
        }

        private void HideVisualizers()
        {
            if (aimLine != null) aimLine.enabled = false;
            if (touchOriginRing != null) touchOriginRing.enabled = false;
        }

        public void ResetDisc()
        {
            isLaunched = false;
            isAiming = false;
            isCanceling = false;
            resetTimer = 0f;
            currentPowerRatio = 0f;
            currentSpinSpeedY = 0f;
            maxAllowedSpeed = 0f;
            preCollisionVelocity = Vector3.zero;

            HideVisualizers();

            if (discRb != null)
            {
                discRb.linearVelocity = Vector3.zero;
                discRb.angularVelocity = Vector3.zero;
                discRb.isKinematic = true;
            }

            if (discTransform != null)
            {
                discTransform.DOKill();
                discTransform.position = spawnPosition;
                discTransform.rotation = spawnRotation;
                currentVisualAngleY = spawnRotation.eulerAngles.y;
            }
        }

        /// <summary>
        /// Called ONLY on initial impact by BucaDiscCollisionRelay!
        /// </summary>
        public void OnDiscCollisionEnter(Collision collision)
        {
            ProcessWallImpactSpin(collision);

            // Block hit
            if (collision.gameObject.TryGetComponent<BucaBlock>(out var block))
            {
                Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : collision.transform.position;
                block.HitByDisc(hitPoint, discRb != null ? discRb.linearVelocity : currentLaunchDir * maxLaunchForce, 1.2f);
            }
            // Obstacle hit
            else if (collision.gameObject.TryGetComponent<BucaObstacle>(out var obstacle))
            {
                obstacle.SendMessage("TriggerHitFeedback", SendMessageOptions.DontRequireReceiver);
            }
        }

        public void OnDiscCollisionStay(Collision collision)
        {
            // Do NOT re-add spin every continuous frame while sliding!
        }

        /// <summary>
        /// Applies a clean rotational spin kick calculated to complete 1.5 - 2 turns on hard hits,
        /// and smoothly decelerate to a complete stop over 1 - 2 seconds.
        /// </summary>
        private void ProcessWallImpactSpin(Collision collision)
        {
            if (collision.contactCount == 0 || discRb == null || !isLaunched) return;
            if (collision.gameObject.name.ToLower().Contains("plane")) return;

            // 1. Natural kinetic energy loss upon impact (absorb 8% per bounce)
            maxAllowedSpeed *= 0.92f;
            if (discRb.linearVelocity.magnitude > maxAllowedSpeed)
            {
                discRb.linearVelocity = discRb.linearVelocity.normalized * maxAllowedSpeed;
            }

            // 2. Find the contact point
            ContactPoint contact = collision.GetContact(0);
            Vector3 contactPoint = contact.point;

            // Outward normal from contact to disc center
            Vector3 outwardNormal = discTransform.position - contactPoint;
            outwardNormal.y = 0f;
            if (outwardNormal.sqrMagnitude < 0.001f) return;
            outwardNormal.Normalize();

            // Extract movement direction before bounce
            Vector3 incomingVel = preCollisionVelocity.sqrMagnitude > 0.5f ? preCollisionVelocity : discRb.linearVelocity;
            incomingVel.y = 0f;
            float speed = incomingVel.magnitude;
            if (speed < 0.2f) return;

            Vector3 moveDir = incomingVel / speed;

            // Cross product:
            // Invert sign to match physical surface friction roll (Clockwise on left wall, Counter-Clockwise on right wall)
            float crossY = Vector3.Cross(moveDir, outwardNormal).y;
            float sideSign = -Mathf.Sign(crossY);

            // Clean speed ratio (scales from 0.25 on soft hits up to 1.0 on max hits)
            float speedRatio = Mathf.Clamp(speed / 20.0f, 0.25f, 1.0f);

            // Glancing grazing factor
            float glancingFactor = 0.4f + 0.6f * Mathf.Clamp01(Mathf.Abs(crossY));
            float spinImpact = wallImpactSpinSpeed * speedRatio * glancingFactor;

            // Add single-hit spin kick, naturally capped
            currentSpinSpeedY += sideSign * spinImpact;
            currentSpinSpeedY = Mathf.Clamp(currentSpinSpeedY, -maxSpinSpeedCap, maxSpinSpeedCap);
        }
    }

    /// <summary>
    /// Attached directly to the disc GameObject to relay all PhysX collision events to BucaDiscLauncher!
    /// </summary>
    public class BucaDiscCollisionRelay : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            if (BucaDiscLauncher.Instance != null)
            {
                BucaDiscLauncher.Instance.OnDiscCollisionEnter(collision);
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (BucaDiscLauncher.Instance != null)
            {
                BucaDiscLauncher.Instance.OnDiscCollisionStay(collision);
            }
        }
    }
}
