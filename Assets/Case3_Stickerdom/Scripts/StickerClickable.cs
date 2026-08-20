using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Stickerdom
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class StickerClickable : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        [Header("Sticker Identity")]
        [Tooltip("Type of this sticker matching GhostSlot.stickerType.")]
        [SerializeField] private StickerType stickerType;

        [Header("Target Ghost Slot Reference")]
        [Tooltip("Direct reference to the matching GhostSlot on the album sheet.")]
        [SerializeField] private GhostSlot targetGhostSlot;

        [Header("Peel-Off Feedback")]
        [Tooltip("Duration of the initial peel-off pop scale.")]
        [SerializeField] private float peelDuration = 0.15f;

        [Tooltip("Amount of punch scale during peel-off.")]
        [SerializeField] private float peelPunchAmount = 0.15f;

        [Header("Flight Animation Settings")]
        [Tooltip("Duration of the flight from tray to album sheet.")]
        [SerializeField] private float flightDuration = 0.55f;

        [Tooltip("Height of the flight arc curve midpoint.")]
        [SerializeField] private float flightArcHeight = 2.0f;

        [Tooltip("Easing curve for the flight path.")]
        [SerializeField] private Ease flightEase = Ease.OutQuad;

        [Tooltip("Sorting order while airborne above other sprites.")]
        [SerializeField] private int flightSortingOrder = 100;

        [Header("Stamp Impact Feedback")]
        [Tooltip("Squash & stretch punch scale upon sticking onto the page.")]
        [SerializeField] private Vector3 stampPunchScale = new Vector3(0.2f, -0.1f, 0f);

        [Tooltip("Duration of the stamp squash & stretch.")]
        [SerializeField] private float stampPunchDuration = 0.25f;

        [Header("Events")]
        [SerializeField] private UnityEvent onPeelStarted;
        [SerializeField] private UnityEvent onStickerPlaced;

        private Collider2D col2D;
        private SpriteRenderer spriteRenderer;
        private bool isFlying = false;
        private bool isPlaced = false;

        public StickerType StickerType => stickerType;
        public GhostSlot TargetGhostSlot { get => targetGhostSlot; set => targetGhostSlot = value; }
        public bool IsFlying => isFlying;
        public bool IsPlaced => isPlaced;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            col2D = GetComponent<Collider2D>();
            
            // Ensure BoxCollider2D matches sprite bounds perfectly
            if (col2D == null)
            {
                col2D = gameObject.AddComponent<BoxCollider2D>();
            }

            if (col2D is BoxCollider2D boxCol && spriteRenderer != null && spriteRenderer.sprite != null)
            {
                boxCol.size = spriteRenderer.sprite.rect.size / spriteRenderer.sprite.pixelsPerUnit;
            }

            FindMatchingGhostSlotIfNull();
        }

        private void Start()
        {
            FindMatchingGhostSlotIfNull();
        }

        private void Update()
        {
            if (isPlaced || isFlying) return;

            // Bulletproof Direct Input System Pointer Check (Works on Mouse & Touch without EventSystem dependencies)
#if ENABLE_INPUT_SYSTEM
            bool isPressed = false;
            Vector2 screenPos = Vector2.zero;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPos = Mouse.current.position.ReadValue();
                isPressed = true;
            }
            else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
                isPressed = true;
            }

            if (isPressed)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 worldPos3D = cam.ScreenToWorldPoint(screenPos);
                    Vector2 worldPos2D = new Vector2(worldPos3D.x, worldPos3D.y);

                    if (col2D != null && col2D.OverlapPoint(worldPos2D))
                    {
                        TriggerTapToPlace();
                    }
                }
            }
#endif
        }

        // EventSystem / Raycaster Fallbacks
        public void OnPointerDown(PointerEventData eventData)
        {
            TriggerTapToPlace();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerTapToPlace();
        }

        public void TriggerTapToPlace()
        {
            if (isPlaced || isFlying) return;

            FindMatchingGhostSlotIfNull();
            if (targetGhostSlot == null)
            {
                Debug.LogWarning($"[StickerClickable] No matching GhostSlot found for {gameObject.name} ({stickerType})!");
                return;
            }

            if (targetGhostSlot.IsOccupied)
            {
                Debug.LogWarning($"[StickerClickable] Target GhostSlot for {gameObject.name} is already occupied!");
                return;
            }

            isFlying = true;

            // 1. Disable collider immediately to prevent duplicate clicks
            if (col2D != null)
            {
                col2D.enabled = false;
            }

            // 2. Elevate sorting order so it flies above all album layers
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = flightSortingOrder;
            }

            onPeelStarted?.Invoke();

            // 3. Construct Flight Tween Sequence
            Sequence flySeq = DOTween.Sequence();
            flySeq.SetTarget(transform);

            // Step A: Tactile Peel-off pop
            flySeq.Append(transform.DOPunchScale(Vector3.one * peelPunchAmount, peelDuration, 10, 1));

            // Step B: Elevated Arc Flight + Rotation + Scale sync
            Vector3 startPos = transform.position;
            Vector3 endPos = targetGhostSlot.TargetPosition;
            // Elevate midpoint along flight path
            Vector3 midPeakPos = (startPos + endPos) * 0.5f + Vector3.up * flightArcHeight + Vector3.back * 0.5f;

            Vector3[] arcPath = new Vector3[] { startPos, midPeakPos, endPos };

            flySeq.Append(transform.DOPath(arcPath, flightDuration, PathType.CatmullRom).SetEase(flightEase));
            flySeq.Join(transform.DORotateQuaternion(targetGhostSlot.TargetRotation, flightDuration).SetEase(Ease.OutQuad));
            flySeq.Join(transform.DOScale(targetGhostSlot.TargetScale, flightDuration).SetEase(Ease.OutQuad));

            // Step C: On Target Arrival -> Precise Snap & Stamp Impact Squash
            flySeq.OnComplete(() =>
            {
                isFlying = false;
                isPlaced = true;

                transform.position = endPos;
                transform.rotation = targetGhostSlot.TargetRotation;
                transform.localScale = targetGhostSlot.TargetScale;

                // Match slot sorting order (e.g. 20)
                if (spriteRenderer != null)
                {
                    spriteRenderer.sortingOrder = targetGhostSlot.PlacedSortingOrder;
                }

                // Tactile Stamp Squash & Stretch
                transform.DOPunchScale(stampPunchScale, stampPunchDuration, 10, 1);

                // Notify target slot
                targetGhostSlot.OnStickerPlaced(this);
                onStickerPlaced?.Invoke();
            });
        }

        private void FindMatchingGhostSlotIfNull()
        {
            if (targetGhostSlot != null) return;

            GhostSlot[] allSlots = FindObjectsOfType<GhostSlot>();
            foreach (var slot in allSlots)
            {
                if (slot != null && slot.StickerType == this.stickerType)
                {
                    targetGhostSlot = slot;
                    break;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto match type by name in editor
            string lower = gameObject.name.ToLower();
            if (lower.Contains("hayvan")) stickerType = StickerType.Hayvan;
            else if (lower.Contains("meyve")) stickerType = StickerType.Meyve;
            else if (lower.Contains("arac")) stickerType = StickerType.Arac;
            else if (lower.Contains("doga")) stickerType = StickerType.Doga;
            else if (lower.Contains("kumsal")) stickerType = StickerType.Kumsal;
            else if (lower.Contains("enstruman")) stickerType = StickerType.Enstruman;
        }
#endif
    }
}
