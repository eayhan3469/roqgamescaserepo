using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace BlockHole
{
    [RequireComponent(typeof(Collider))]
    public class BlockDraggable : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Shape Identity & Footprint")]
        [Tooltip("Shape type matching HoleTarget.shapeType.")]
        [SerializeField] private BlockShapeType shapeType = BlockShapeType.Single;

        [Tooltip("Relative tile footprint cells occupied by this block from anchor (0,0).")]
        [SerializeField] private List<Vector2Int> footprint = new List<Vector2Int> { Vector2Int.zero };

        [Tooltip("Offset of the visual root relative to the base anchor tile world position.")]
        [SerializeField] private Vector3 rootVisualOffset = Vector3.zero;

        [Tooltip("Current integer anchor grid position of the block.")]
        [SerializeField] private Vector2Int currentAnchorGridPos;

        [Header("Tactile Drag & Sliding Settings")]
        [Tooltip("Speed multiplier at which the visual transform follows the drag cursor.")]
        [SerializeField] private float dragFollowSpeed = 32f;

        [Tooltip("Duration of the grid snap animation on release.")]
        [SerializeField] private float snapDuration = 0.14f;

        [Tooltip("Easing curve for the grid snap animation.")]
        [SerializeField] private Ease snapEase = Ease.OutQuad;

        [Header("Dynamic Tilt & Sway Juice")]
        [Tooltip("Maximum tilt angle in degrees when dragging fast.")]
        [SerializeField] private float maxTiltAngle = 10f;

        [Tooltip("Sensitivity of the tilt response to drag velocity.")]
        [SerializeField] private float tiltSensitivity = 1.4f;

        [Tooltip("Speed at which rotation lerps towards target sway during active drag.")]
        [SerializeField] private float swayLerpSpeed = 16f;

        [Tooltip("Duration of the spring recovery back to zero rotation on release.")]
        [SerializeField] private float swayResetDuration = 0.20f;

        [Header("Instant Mid-Drag Hole Capture")]
        [Tooltip("Threshold distance from hole mouth to trigger instant mid-drag suction.")]
        [SerializeField] private float autoCaptureDistance = 0.45f;

        [Tooltip("Duration of the downward drop into the hole.")]
        [SerializeField] private float holeDropDuration = 0.24f;

        [Tooltip("Y position down the hole shaft where impact occurs.")]
        [SerializeField] private float dropDepthY = -0.45f;

        [Tooltip("Easing curve for falling down the hole.")]
        [SerializeField] private Ease holeDropEase = Ease.InQuad;

        [Header("Fracture Destruction")]
        [Tooltip("Fracture effect component to spawn shattered pieces on impact.")]
        [SerializeField] private BlockFractureEffect fractureEffect;

        [Header("Juice & Visual Feedback")]
        [Tooltip("Height elevation during drag.")]
        [SerializeField] private float dragLiftHeight = 0.40f;

        [Tooltip("Scale pulse multiplier when touched.")]
        [SerializeField] private float touchScaleMultiplier = 1.05f;

        [Header("Events")]
        [SerializeField] private UnityEvent onDragStarted;
        [SerializeField] private UnityEvent<Vector2Int> onGridPositionChanged;
        [SerializeField] private UnityEvent onDragEnded;
        [SerializeField] private UnityEvent onDroppedInHole;

        private Plane boardPlane;
        private Vector3 dragPlaneOffset;
        private Vector3 originalLocalScale;
        private bool isDragging = false;
        private bool isDroppedInHole = false;
        private Tweener snapTween;
        private Tweener swayResetTween;
        private Vector3 currentValidWorldPos;
        private Vector2Int lastValidAnchorGridPos;
        private Collider[] allColliders;
        private HoleTarget cachedMatchingHole;
        private Material blockPrimaryMaterial;
        private Vector3 prevFrameWorldPos;

        public BlockShapeType ShapeType { get => shapeType; set => shapeType = value; }
        public List<Vector2Int> Footprint => footprint;
        public Vector3 RootVisualOffset { get => rootVisualOffset; set => rootVisualOffset = value; }
        public Vector2Int CurrentAnchorGridPos { get => currentAnchorGridPos; set => currentAnchorGridPos = value; }
        public bool IsDragging => isDragging;
        public bool IsDroppedInHole => isDroppedInHole;
        public BlockFractureEffect FractureEffect { get => fractureEffect; set => fractureEffect = value; }

        private void Awake()
        {
            originalLocalScale = transform.localScale;
            allColliders = GetComponentsInChildren<Collider>();
            
            var mr = GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                blockPrimaryMaterial = mr.sharedMaterial;
            }

            if (fractureEffect == null)
            {
                fractureEffect = GetComponent<BlockFractureEffect>();
            }

            Initialize();
        }

        public void Initialize()
        {
            if (GridManager.Instance != null && currentAnchorGridPos == Vector2Int.zero && transform.position != Vector3.zero)
            {
                Vector3 baseWorldPos = transform.position - rootVisualOffset;
                currentAnchorGridPos = GridManager.Instance.WorldToGridPosition(baseWorldPos);
            }

            lastValidAnchorGridPos = currentAnchorGridPos;
            currentValidWorldPos = GetWorldPosForAnchor(currentAnchorGridPos);
            prevFrameWorldPos = transform.position;
            boardPlane = new Plane(Vector3.up, new Vector3(0f, 0.03f, 0f));
        }

        private void Update()
        {
            if (!isDragging || isDroppedInHole) return;

            // Calculate velocity delta on XZ plane
            Vector3 currentPos = transform.position;
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            Vector3 velocity = (currentPos - prevFrameWorldPos) / dt;
            prevFrameWorldPos = currentPos;

            // Tilting around Z axis for X movement, and around X axis for Z movement
            float targetTiltZ = -Mathf.Clamp(velocity.x * tiltSensitivity, -maxTiltAngle, maxTiltAngle);
            float targetTiltX = Mathf.Clamp(velocity.z * tiltSensitivity, -maxTiltAngle, maxTiltAngle);

            Quaternion targetSwayRotation = Quaternion.Euler(targetTiltX, 0f, targetTiltZ);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetSwayRotation, Time.deltaTime * swayLerpSpeed);
        }

        public List<Vector2Int> GetWorldFootprint(Vector2Int anchor)
        {
            List<Vector2Int> list = new List<Vector2Int>(footprint.Count);
            for (int i = 0; i < footprint.Count; i++)
            {
                list.Add(anchor + footprint[i]);
            }
            return list;
        }

        public Vector3 GetWorldPosForAnchor(Vector2Int anchor)
        {
            if (GridManager.Instance == null) return transform.position;
            return GridManager.Instance.GridToWorldPosition(anchor) + rootVisualOffset;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isDroppedInHole) return;

            isDragging = true;
            snapTween?.Kill();
            swayResetTween?.Kill();
            prevFrameWorldPos = transform.position;

            // Audio: Play Pickup Sound
            if (BlockHoleAudioManager.Instance != null)
            {
                BlockHoleAudioManager.Instance.PlayPickupSound();
            }

            // 1. Find and highlight matching HoleTarget
            if (GridManager.Instance != null)
            {
                cachedMatchingHole = GridManager.Instance.GetMatchingHoleForBlock(this);
                if (cachedMatchingHole != null)
                {
                    cachedMatchingHole.SetHighlight(true);
                }
            }

            Camera cam = eventData.pressEventCamera ?? Camera.main;
            Ray ray = cam != null ? cam.ScreenPointToRay(eventData.position) : new Ray(transform.position + Vector3.up * 10f, Vector3.down);
            if (boardPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                dragPlaneOffset = currentValidWorldPos - hitPoint;
            }
            else
            {
                dragPlaneOffset = Vector3.zero;
            }

            transform.DOScale(originalLocalScale * touchScaleMultiplier, 0.1f).SetEase(Ease.OutQuad);
            onDragStarted?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || isDroppedInHole || GridManager.Instance == null) return;

            Camera cam = eventData.pressEventCamera ?? Camera.main;
            Ray ray = cam != null ? cam.ScreenPointToRay(eventData.position) : new Ray(transform.position + Vector3.up * 10f, Vector3.down);
            if (!boardPlane.Raycast(ray, out float enter)) return;

            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 rawDesiredWorldPos = hitPoint + dragPlaneOffset;

            // 1. INSTANT MID-DRAG HOLE CAPTURE CHECK
            if (cachedMatchingHole != null && !cachedMatchingHole.IsFilled)
            {
                Vector2 rawXZ = new Vector2(rawDesiredWorldPos.x, rawDesiredWorldPos.z);
                Vector2 targetXZ = new Vector2(cachedMatchingHole.TargetDropWorldPos.x, cachedMatchingHole.TargetDropWorldPos.z);
                float distToHole = Vector2.Distance(rawXZ, targetXZ);

                float captureLimit = autoCaptureDistance * GridManager.Instance.TileSize;
                if (distToHole <= captureLimit || cachedMatchingHole.IsWithinSnapDistance(transform.position))
                {
                    // Instant capture & suction right during the drag!
                    DropIntoHole(cachedMatchingHole);
                    return;
                }
            }

            // 2. Fluid sliding: Check movement across board allowing passing over holes (ignoreHoles: true)
            Vector3 currentAnchorWorld = GridManager.Instance.GridToWorldPosition(currentAnchorGridPos) + rootVisualOffset;
            Vector3 deltaWorld = rawDesiredWorldPos - currentAnchorWorld;

            float tileSize = GridManager.Instance.TileSize;
            float continuousDeltaX = deltaWorld.x / tileSize;
            float continuousDeltaZ = deltaWorld.z / tileSize;

            Vector2Int proposedAnchor = currentAnchorGridPos;

            // Step along X axis
            int stepX = continuousDeltaX > 0.5f ? 1 : (continuousDeltaX < -0.5f ? -1 : 0);
            if (stepX != 0)
            {
                Vector2Int testAnchor = new Vector2Int(currentAnchorGridPos.x + stepX, currentAnchorGridPos.y);
                if (GridManager.Instance.CanShapeOccupy(GetWorldFootprint(testAnchor), this, true))
                {
                    proposedAnchor.x = testAnchor.x;
                }
            }

            // Step along Y axis
            int stepY = continuousDeltaZ > 0.5f ? 1 : (continuousDeltaZ < -0.5f ? -1 : 0);
            if (stepY != 0)
            {
                Vector2Int testAnchor = new Vector2Int(proposedAnchor.x, currentAnchorGridPos.y + stepY);
                if (GridManager.Instance.CanShapeOccupy(GetWorldFootprint(testAnchor), this, true))
                {
                    proposedAnchor.y = testAnchor.y;
                }
            }

            if (proposedAnchor != currentAnchorGridPos)
            {
                currentAnchorGridPos = proposedAnchor;
                GridManager.Instance.UpdateBlockPosition(this, currentAnchorGridPos);
                onGridPositionChanged?.Invoke(currentAnchorGridPos);
            }

            Vector3 validAnchorWorld = GetWorldPosForAnchor(currentAnchorGridPos);
            currentValidWorldPos = validAnchorWorld;

            // Elastic lead clamped to 0.45 tiles
            float clampRange = tileSize * 0.45f;
            float clampedLeadX = Mathf.Clamp(rawDesiredWorldPos.x - validAnchorWorld.x, -clampRange, clampRange);
            float clampedLeadZ = Mathf.Clamp(rawDesiredWorldPos.z - validAnchorWorld.z, -clampRange, clampRange);

            if (clampedLeadX > 0.05f && !GridManager.Instance.CanShapeOccupy(GetWorldFootprint(currentAnchorGridPos + Vector2Int.right), this, true)) clampedLeadX = 0f;
            if (clampedLeadX < -0.05f && !GridManager.Instance.CanShapeOccupy(GetWorldFootprint(currentAnchorGridPos + Vector2Int.left), this, true)) clampedLeadX = 0f;
            if (clampedLeadZ > 0.05f && !GridManager.Instance.CanShapeOccupy(GetWorldFootprint(currentAnchorGridPos + Vector2Int.up), this, true)) clampedLeadZ = 0f;
            if (clampedLeadZ < -0.05f && !GridManager.Instance.CanShapeOccupy(GetWorldFootprint(currentAnchorGridPos + Vector2Int.down), this, true)) clampedLeadZ = 0f;

            Vector3 targetFollowPos = validAnchorWorld + new Vector3(clampedLeadX, dragLiftHeight, clampedLeadZ);
            transform.position = Vector3.Lerp(transform.position, targetFollowPos, Time.deltaTime * dragFollowSpeed);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isDragging || isDroppedInHole) return;
            isDragging = false;

            snapTween?.Kill();
            swayResetTween?.Kill();

            // Reset sway rotation with a soft overshoot spring
            swayResetTween = transform.DOLocalRotate(Vector3.zero, swayResetDuration).SetEase(Ease.OutBack);

            // 1. Turn off highlight on matching HoleTarget
            if (cachedMatchingHole != null)
            {
                cachedMatchingHole.SetHighlight(false);
            }
            else if (GridManager.Instance != null)
            {
                cachedMatchingHole = GridManager.Instance.GetMatchingHoleForBlock(this);
                if (cachedMatchingHole != null) cachedMatchingHole.SetHighlight(false);
            }

            // 2. Check if dropped over matching HoleTarget
            if (cachedMatchingHole != null && !cachedMatchingHole.IsFilled && cachedMatchingHole.IsWithinSnapDistance(transform.position))
            {
                DropIntoHole(cachedMatchingHole);
                return;
            }

            // 3. Regular floor snap: Play SnapBack tactile sound
            if (BlockHoleAudioManager.Instance != null)
            {
                BlockHoleAudioManager.Instance.PlaySnapBackSound();
            }

            if (GridManager.Instance != null)
            {
                Vector2Int desiredAnchor = currentAnchorGridPos;
                Vector2Int validAnchor = GridManager.Instance.FindNearestValidAnchor(this, desiredAnchor);

                currentAnchorGridPos = validAnchor;
                lastValidAnchorGridPos = validAnchor;
                GridManager.Instance.UpdateBlockPosition(this, currentAnchorGridPos);
            }

            Vector3 snapTargetWorld = GetWorldPosForAnchor(currentAnchorGridPos);
            currentValidWorldPos = snapTargetWorld;

            snapTween = transform.DOMove(snapTargetWorld, snapDuration).SetEase(snapEase);
            transform.DOScale(originalLocalScale, snapDuration).SetEase(snapEase);

            onDragEnded?.Invoke();
        }

        public void DropIntoHole(HoleTarget hole)
        {
            if (hole == null || isDroppedInHole) return;

            isDragging = false;
            isDroppedInHole = true;
            this.enabled = false;
            snapTween?.Kill();
            swayResetTween?.Kill();

            // Turn off highlight immediately
            hole.SetHighlight(false);

            // Audio: Play Drop Swallowed Sound
            if (BlockHoleAudioManager.Instance != null)
            {
                BlockHoleAudioManager.Instance.PlayDropSound();
            }

            if (allColliders != null)
            {
                for (int i = 0; i < allColliders.Length; i++)
                {
                    if (allColliders[i] != null) allColliders[i].enabled = false;
                }
            }

            if (GridManager.Instance != null)
            {
                GridManager.Instance.UnregisterBlock(this);
            }

            hole.SetFilled(true);

            Vector3 holeMouthPos = hole.TargetDropWorldPos;
            Vector3 holeBottomPos = new Vector3(holeMouthPos.x, dropDepthY, holeMouthPos.z);

            Sequence dropSeq = DOTween.Sequence();
            dropSeq.SetTarget(transform);

            // 1. Snap seamlessly to hole mouth & level rotation
            dropSeq.Append(transform.DOMove(holeMouthPos, 0.06f).SetEase(Ease.OutQuad));
            dropSeq.Join(transform.DOScale(originalLocalScale, 0.06f).SetEase(Ease.OutQuad));
            dropSeq.Join(transform.DOLocalRotate(Vector3.zero, 0.06f).SetEase(Ease.OutQuad));

            // 2. Fall into the hole shaft
            dropSeq.Append(transform.DOMove(holeBottomPos, holeDropDuration).SetEase(holeDropEase));

            dropSeq.OnComplete(() =>
            {
                // Hide intact block renderers seamlessly
                var renderers = GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (r != null) r.enabled = false;
                }

                // Audio: Play Shatter Impact Sound
                if (BlockHoleAudioManager.Instance != null)
                {
                    BlockHoleAudioManager.Instance.PlayShatterSound();
                }

                // 1. Trigger Fracture at the EXACT position and rotation of the block
                if (fractureEffect != null)
                {
                    fractureEffect.TriggerFracture(transform.position, transform.rotation, blockPrimaryMaterial);
                }

                // 2. Complete hole event, notify GridManager, and schedule destroy
                hole.OnBlockDropped(this);
                onDroppedInHole?.Invoke();

                if (GridManager.Instance != null)
                {
                    GridManager.Instance.OnBlockSwallowed(this);
                }

                Destroy(gameObject, 0.8f);
            });

            onDragEnded?.Invoke();
        }

        private void OnDestroy()
        {
            snapTween?.Kill();
            swayResetTween?.Kill();
        }
    }
}
