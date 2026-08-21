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
        [SerializeField] private float maxDragDistance = 3.2f;

        [Tooltip("Distance from touch center below which the shot is CANCELLED.")]
        [SerializeField] private float cancelThresholdDistance = 0.45f;

        [Tooltip("Maximum launch speed at full aim.")]
        [SerializeField] private float maxLaunchSpeed = 38.0f;

        [Tooltip("Minimum launch speed at minimum threshold.")]
        [SerializeField] private float minLaunchSpeed = 8.0f;

        [Tooltip("Base forward direction along the launch track.")]
        [SerializeField] private Vector3 baseForwardDir = new Vector3(0f, 0f, 1f);

        [Tooltip("Maximum allowed horizontal aim angle deflection in degrees (+/-).")]
        [SerializeField] private float maxAimAngle = 45.0f;

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

        [Header("Spin & Reset")]
        [SerializeField] private float spinSpeed = 720.0f;
        [SerializeField] private float autoResetDelay = 4.0f;

        private Rigidbody discRb;
        private SphereCollider discCollider;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        private bool isAiming = false;
        private bool isLaunched = false;
        private bool isCanceling = false;
        private Vector3 dragStartWorldPos;
        private Vector3 currentLaunchDir;
        private float currentPowerRatio = 0f;
        private Camera mainCam;
        private float resetTimer = 0f;
        private Plane groundPlane;

        public bool IsAiming => isAiming;
        public bool IsLaunched => isLaunched;
        public bool IsCanceling => isCanceling;
        public Transform DiscTransform => discTransform;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(this);

            mainCam = Camera.main;

            FindAndSetupDisc();
            SetupAimLine();
            SetupTouchVisualizers();
        }

        private void Start()
        {
            if (discTransform != null)
            {
                spawnPosition = discTransform.position;
                spawnRotation = discTransform.rotation;
                groundPlane = new Plane(Vector3.up, spawnPosition);
            }
        }

        private void FindAndSetupDisc()
        {
            if (discTransform == null)
            {
                GameObject discGo = GameObject.Find("disc");
                if (discGo != null)
                {
                    discTransform = discGo.transform;
                }
            }

            if (discTransform == null)
            {
                Debug.LogWarning("[BucaDiscLauncher] disc Transform not found in scene!");
                return;
            }

            // Setup Rigidbody
            discRb = discTransform.GetComponent<Rigidbody>();
            if (discRb == null) discRb = discTransform.gameObject.AddComponent<Rigidbody>();

            discRb.mass = 1.2f;
            discRb.linearDamping = 0.12f;
            discRb.angularDamping = 0.4f;
            discRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            discRb.interpolation = RigidbodyInterpolation.Interpolate;
            discRb.isKinematic = true;

            // Setup SphereCollider
            discCollider = discTransform.GetComponent<SphereCollider>();
            if (discCollider == null)
            {
                var mc = discTransform.GetComponent<MeshCollider>();
                if (mc != null) Destroy(mc);

                discCollider = discTransform.gameObject.AddComponent<SphereCollider>();
                discCollider.radius = 0.45f;
            }

            // Smooth physics material
            PhysicsMaterial discPhysMat = new PhysicsMaterial("BucaDiscMat")
            {
                dynamicFriction = 0.01f,
                staticFriction = 0.01f,
                bounciness = 0.85f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            discCollider.material = discPhysMat;

            if (!discTransform.CompareTag("Player"))
            {
                discTransform.gameObject.tag = "Player";
            }
        }

        private void SetupAimLine()
        {
            if (aimLine == null)
            {
                aimLine = GetComponent<LineRenderer>();
                if (aimLine == null)
                {
                    aimLine = gameObject.AddComponent<LineRenderer>();
                }
            }

            aimLine.positionCount = 3;
            aimLine.useWorldSpace = true;
            aimLine.enabled = false;

            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0f, 0.28f);
            widthCurve.AddKey(0.75f, 0.22f);
            widthCurve.AddKey(0.85f, 0.40f); // Arrow flare
            widthCurve.AddKey(1f, 0.02f);    // Sharp arrow tip
            aimLine.widthCurve = widthCurve;
            aimLine.widthMultiplier = 1.0f;

            Material lineMat = new Material(Shader.Find("Sprites/Default"));
            aimLine.material = lineMat;
        }

        private void SetupTouchVisualizers()
        {
            // Touch Origin Ring (Clean circle at the touch point)
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

            if (isLaunched)
            {
                // Spin disc visually while moving
                if (discRb != null && discRb.linearVelocity.sqrMagnitude > 0.1f)
                {
                    discTransform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
                }

                // Check auto reset if stopped or timeout
                resetTimer += Time.deltaTime;
                if (resetTimer > autoResetDelay || (resetTimer > 1.5f && discRb != null && discRb.linearVelocity.magnitude < 0.2f))
                {
                    ResetDisc();
                }
            }
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

            // Disc stays fixed at spawn position
            discTransform.position = spawnPosition;
            discTransform.rotation = spawnRotation;

            // Show touch origin ring only
            DrawOriginRing(dragStartWorldPos, ringCancelColor);

            if (aimLine != null) aimLine.enabled = false;
        }

        private void UpdateAiming(Vector3 currentWorldPos)
        {
            Vector3 dragVector = currentWorldPos - dragStartWorldPos;
            dragVector.y = 0f;

            float dragDist = dragVector.magnitude;

            // Check if inside cancel zone
            if (dragDist < cancelThresholdDistance)
            {
                isCanceling = true;
                currentPowerRatio = 0f;

                // Color ring red/soft to indicate release will cancel
                DrawOriginRing(dragStartWorldPos, ringCancelColor);

                if (aimLine != null) aimLine.enabled = false;

                discTransform.rotation = spawnRotation;
                return;
            }

            // Valid aim outside cancel zone
            isCanceling = false;
            float effectiveDrag = dragDist - cancelThresholdDistance;
            currentPowerRatio = Mathf.Clamp01(effectiveDrag / (maxDragDistance - cancelThresholdDistance));

            // Aim vector: pulling backwards aims forward
            Vector3 aimVector = -dragVector;
            Vector3 candidateDir = aimVector.normalized;

            float angle = Vector3.SignedAngle(baseForwardDir, candidateDir, Vector3.up);
            float clampedAngle = Mathf.Clamp(angle, -maxAimAngle, maxAimAngle);
            currentLaunchDir = Quaternion.AngleAxis(clampedAngle, Vector3.up) * baseForwardDir.normalized;

            // Update Disc rotation facing the aim
            discTransform.rotation = Quaternion.LookRotation(currentLaunchDir, Vector3.up);

            // Update Touch Origin Ring (Glow active color)
            DrawOriginRing(dragStartWorldPos, ringActiveColor);

            // Update Forward Aim Indicator at the disc
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
                // CANCELED!
                discTransform.position = spawnPosition;
                discTransform.rotation = spawnRotation;
                Debug.Log("[BucaDiscLauncher] Shot Canceled (Released inside cancel zone).");
                return;
            }

            // Fire forward along the chosen aim direction!
            float launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, currentPowerRatio);
            isLaunched = true;
            resetTimer = 0f;

            discRb.isKinematic = false;
            discRb.useGravity = true;
            discRb.linearVelocity = currentLaunchDir.normalized * launchSpeed;
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
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Direct impact on blocks
            if (collision.gameObject.TryGetComponent<BucaBlock>(out var block))
            {
                Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : collision.transform.position;
                block.HitByDisc(hitPoint, discRb != null ? discRb.linearVelocity : currentLaunchDir * maxLaunchSpeed, 1.2f);
            }
        }
    }
}
