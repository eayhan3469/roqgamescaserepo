using System;
using UnityEngine;
using UnityEngine.Serialization;
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

        [Tooltip("Maximum launch speed at 100% pull.")]
        [FormerlySerializedAs("maxLaunchSpeed")]
        [SerializeField] private float maxLaunchForce = 48.0f;

        [Tooltip("Minimum launch speed.")]
        [FormerlySerializedAs("minLaunchSpeed")]
        [SerializeField] private float minLaunchForce = 12.0f;

        [Tooltip("Base forward direction along the launch track.")]
        [SerializeField] private Vector3 baseForwardDir = new Vector3(0f, 0f, 1f);

        [Header("Forward Aim Indicator (Pure Triangle)")]
        [SerializeField] private LineRenderer aimLine;
        [SerializeField] private float minIndicatorLength = 1.4f;
        [SerializeField] private float maxIndicatorLength = 6.2f;
        [SerializeField] private float baseConeWidth = 0.95f;
        [SerializeField] private Color indicatorColorLow = new Color(0.15f, 0.85f, 1.0f, 0.85f);
        [SerializeField] private Color indicatorColorHigh = new Color(1.0f, 0.80f, 0.15f, 1.0f);

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
        private float displayedElasticPower = 0f;
        private Camera mainCam;
        private float resetTimer = 0f;
        private Plane groundPlane;

        // Strict speed cap to prevent wall acceleration
        private float maxAllowedSpeed = 0f;
        private Vector3 preCollisionVelocity = Vector3.zero;

        // Dedicated visual & physical spin integration
        private float currentVisualAngleY = 0f;
        private float currentSpinSpeedY = 0f;

        // Procedural Cone / Triangle Mesh indicator
        private MeshFilter aimMeshFilter;
        private MeshRenderer aimMeshRenderer;
        private Mesh aimMesh;

        // Visual Polish Trail & Slipstream effect
        private BucaDiscTrailEffect trailEffect;

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
            SetupAimVisualizers();
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

            // Attach visual polish trail & particle slipstream effect
            trailEffect = discTransform.GetComponent<BucaDiscTrailEffect>();
            if (trailEffect == null)
            {
                trailEffect = discTransform.gameObject.AddComponent<BucaDiscTrailEffect>();
            }

            discRb = discTransform.GetComponent<Rigidbody>();
            if (discRb == null) discRb = discTransform.gameObject.AddComponent<Rigidbody>();

            discRb.mass = 1.5f;
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

        private void SetupAimVisualizers()
        {
            SetupAimMesh();
            SetupAimLine();
            SetupTouchVisualizers();
        }

        private void SetupAimMesh()
        {
            if (aimMeshFilter == null)
            {
                GameObject meshGo = new GameObject("AimConeIndicator");
                meshGo.transform.SetParent(transform);
                aimMeshFilter = meshGo.AddComponent<MeshFilter>();
                aimMeshRenderer = meshGo.AddComponent<MeshRenderer>();

                aimMesh = new Mesh { name = "AimConeMesh" };
                aimMesh.MarkDynamic();
                aimMeshFilter.mesh = aimMesh;

                Material mat = new Material(Shader.Find("Sprites/Default"));
                aimMeshRenderer.material = mat;
                aimMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                aimMeshRenderer.receiveShadows = false;
                aimMeshRenderer.enabled = false;
            }
        }

        private void SetupAimLine()
        {
            if (aimLine == null)
            {
                aimLine = GetComponent<LineRenderer>();
                if (aimLine == null) aimLine = gameObject.AddComponent<LineRenderer>();
            }

            aimLine.positionCount = 2;
            aimLine.useWorldSpace = true;
            aimLine.enabled = false;
            aimLine.startWidth = 0.08f;
            aimLine.endWidth = 0.04f;

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

            if (isAiming && !isCanceling && currentPowerRatio > 0.01f)
            {
                UpdateAimIndicator(displayedElasticPower, currentLaunchDir);
            }

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

                // Auto reset when stopped or timeout (prevent early reset if level is cleared and victory celebration is playing)
                bool isCleared = BucaGameManager.Instance != null && BucaGameManager.Instance.IsLevelCleared;
                if (!isCleared)
                {
                    resetTimer += Time.deltaTime;
                    if (resetTimer > autoResetDelay || (resetTimer > 1.5f && discRb != null && discRb.linearVelocity.magnitude < 0.25f))
                    {
                        ResetDisc();
                    }
                }
                else if (discRb != null && discRb.linearVelocity.magnitude > 0.02f)
                {
                    // Smoothly glide to a stop during victory sequence
                    discRb.linearVelocity = Vector3.Lerp(discRb.linearVelocity, Vector3.zero, Time.deltaTime * 3.5f);
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

            Vector2 screenPos = Vector2.zero;
            bool pointerDown = false;
            bool pointerHeld = false;
            bool pointerUp = false;

#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                screenPos = touch.position.ReadValue();
                pointerDown = touch.press.wasPressedThisFrame;
                pointerHeld = touch.press.isPressed;
                pointerUp = touch.press.wasReleasedThisFrame;
            }

            if (!pointerDown && !pointerHeld && !pointerUp && Mouse.current != null)
            {
                screenPos = Mouse.current.position.ReadValue();
                pointerDown = Mouse.current.leftButton.wasPressedThisFrame;
                pointerHeld = Mouse.current.leftButton.isPressed;
                pointerUp = Mouse.current.leftButton.wasReleasedThisFrame;
            }
#else
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                screenPos = t.position;
                pointerDown = t.phase == TouchPhase.Began;
                pointerHeld = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
                pointerUp = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            }
            else
            {
                screenPos = Input.mousePosition;
                pointerDown = Input.GetMouseButtonDown(0);
                pointerHeld = Input.GetMouseButton(0);
                pointerUp = Input.GetMouseButtonUp(0);
            }
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
            displayedElasticPower = 0f;
            currentLaunchDir = baseForwardDir.normalized;

            discTransform.position = spawnPosition;
            discTransform.rotation = spawnRotation;
            currentVisualAngleY = spawnRotation.eulerAngles.y;

            DrawOriginRing(dragStartWorldPos, ringCancelColor);

            if (aimLine != null) aimLine.enabled = false;
            if (aimMeshRenderer != null) aimMeshRenderer.enabled = false;
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
                displayedElasticPower = 0f;

                DrawOriginRing(dragStartWorldPos, ringCancelColor);

                if (aimLine != null) aimLine.enabled = false;
                if (aimMeshRenderer != null) aimMeshRenderer.enabled = false;

                discTransform.rotation = spawnRotation;
                currentVisualAngleY = spawnRotation.eulerAngles.y;
                return;
            }

            // Valid aim outside cancel zone
            isCanceling = false;
            float effectiveDrag = dragDist - cancelThresholdDistance;
            currentPowerRatio = Mathf.Clamp01(effectiveDrag / (maxDragDistance - cancelThresholdDistance));

            // Elastic tension easing curve (eases in smoothly, resists near max like rubber)
            float elasticTarget = 1f - Mathf.Pow(1f - currentPowerRatio, 2.0f);
            displayedElasticPower = Mathf.Lerp(displayedElasticPower, elasticTarget, Time.deltaTime * 24.0f);

            // Full 360 aim: pulling backwards aims forward
            Vector3 aimVector = -dragVector;
            currentLaunchDir = aimVector.normalized;

            currentVisualAngleY = Quaternion.LookRotation(currentLaunchDir, Vector3.up).eulerAngles.y;
            discTransform.rotation = Quaternion.Euler(0f, currentVisualAngleY, 0f);

            DrawOriginRing(dragStartWorldPos, ringActiveColor);

            UpdateAimIndicator(displayedElasticPower, currentLaunchDir);
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

        private void UpdateAimIndicator(float elasticPower, Vector3 direction)
        {
            UpdateAimMesh(elasticPower, direction);
            UpdateAimSpineLine(elasticPower, direction);
        }

        private void UpdateAimMesh(float elasticPower, Vector3 direction)
        {
            if (aimMesh == null || aimMeshRenderer == null) return;
            aimMeshRenderer.enabled = true;

            float length = Mathf.Lerp(minIndicatorLength, maxIndicatorLength, elasticPower);
            float baseW = Mathf.Lerp(0.50f, baseConeWidth, elasticPower);

            Vector3 startPos = spawnPosition + direction * 0.40f;
            startPos.y = spawnPosition.y + 0.035f;

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;

            // 3-Vertex Pure Geometric Triangle Wedge (Düz Üçgen)
            Vector3[] vertices = new Vector3[3];
            // Base at puck (Left and Right)
            vertices[0] = startPos - right * (baseW * 0.5f);
            vertices[1] = startPos + right * (baseW * 0.5f);
            // Apex / Pointed Tip (Forward)
            vertices[2] = startPos + direction * length;

            int[] triangles = new int[]
            {
                0, 2, 1
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 1.0f)
            };

            Color currentColor = Color.Lerp(indicatorColorLow, indicatorColorHigh, elasticPower);

            Color[] colors = new Color[3];
            colors[0] = new Color(currentColor.r, currentColor.g, currentColor.b, 0.45f);
            colors[1] = new Color(currentColor.r, currentColor.g, currentColor.b, 0.45f);
            colors[2] = Color.Lerp(currentColor, Color.white, 0.75f); // Bright luminous apex tip

            aimMesh.Clear();
            aimMesh.vertices = vertices;
            aimMesh.triangles = triangles;
            aimMesh.uv = uvs;
            aimMesh.colors = colors;
            aimMesh.RecalculateNormals();
        }

        private void UpdateAimSpineLine(float elasticPower, Vector3 direction)
        {
            if (aimLine == null) return;
            aimLine.enabled = true;

            float length = Mathf.Lerp(minIndicatorLength, maxIndicatorLength, elasticPower);
            Vector3 startPos = spawnPosition + direction * 0.42f;
            startPos.y = spawnPosition.y + 0.045f;
            Vector3 tipPos = startPos + direction * length;

            aimLine.positionCount = 2;
            aimLine.SetPosition(0, startPos);
            aimLine.SetPosition(1, tipPos);

            Color activeColor = Color.Lerp(indicatorColorLow, indicatorColorHigh, elasticPower);
            Color spineColor = Color.Lerp(activeColor, Color.white, 0.65f);
            aimLine.startColor = new Color(spineColor.r, spineColor.g, spineColor.b, 0.35f);
            aimLine.endColor = new Color(spineColor.r, spineColor.g, spineColor.b, 0.95f);
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

            Vector3 impulse = currentLaunchDir.normalized * (launchForce * (discRb != null ? discRb.mass : 1.0f));
            discRb.AddForce(impulse, ForceMode.Impulse);
            preCollisionVelocity = currentLaunchDir.normalized * launchForce;

            if (trailEffect != null)
            {
                trailEffect.ClearTrails();
                trailEffect.SetEmitting(true);
            }

            if (BucaAudioManager.Instance != null)
            {
                BucaAudioManager.Instance.PlayLaunchSound(currentPowerRatio);
            }
        }

        private void HideVisualizers()
        {
            if (aimLine != null) aimLine.enabled = false;
            if (aimMeshRenderer != null) aimMeshRenderer.enabled = false;
            if (touchOriginRing != null) touchOriginRing.enabled = false;
        }

        public void ResetDisc()
        {
            isLaunched = false;
            isAiming = false;
            isCanceling = false;
            resetTimer = 0f;
            currentPowerRatio = 0f;
            displayedElasticPower = 0f;
            currentSpinSpeedY = 0f;
            maxAllowedSpeed = 0f;
            preCollisionVelocity = Vector3.zero;

            HideVisualizers();

            if (trailEffect != null)
            {
                trailEffect.ClearTrails();
            }

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
            // Block hit: Solid penetration with heavy momentum transfer
            if (collision.gameObject.TryGetComponent<BucaBlock>(out var block))
            {
                Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : collision.transform.position;
                Vector3 incomingVel = preCollisionVelocity.sqrMagnitude > 0.5f ? preCollisionVelocity : (discRb != null ? discRb.linearVelocity : currentLaunchDir * maxLaunchForce);

                // Disc carries heavy momentum through lighter cubes
                if (discRb != null)
                {
                    discRb.linearVelocity *= 0.85f;
                    maxAllowedSpeed *= 0.85f;
                }

                block.HitByDisc(hitPoint, incomingVel, 1.25f);
            }
            // Obstacle hit
            else if (collision.gameObject.TryGetComponent<BucaObstacle>(out var obstacle))
            {
                ProcessWallImpactSpin(collision, true);
                obstacle.SendMessage("TriggerHitFeedback", SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                ProcessWallImpactSpin(collision, false);
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
        private void ProcessWallImpactSpin(Collision collision, bool isObstacle = false)
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

            if (trailEffect != null)
            {
                trailEffect.TriggerWallBounceSpark(contactPoint, outwardNormal);
            }

            if (BucaAudioManager.Instance != null)
            {
                if (isObstacle)
                {
                    BucaAudioManager.Instance.PlayObstacleDeflectSound(speedRatio);
                }
                else
                {
                    BucaAudioManager.Instance.PlayWallBounceSound(speedRatio);
                }
            }
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
