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

        [Header("3D Peel Mesh Controller")]
        [SerializeField] private StickerPeel3DMesh peelMesh3D;

        [Header("Tactile Peel-Off Settings")]
        [Tooltip("Duration of the true 3D corner peel-off animation (0 to 1).")]
        [SerializeField] private float peelDuration = 0.46f;

        [Tooltip("Corner lift tilt angle strength in degrees.")]
        [SerializeField] private float peelTiltStrength = 14.0f;

        [Header("Flight Animation Settings")]
        [Tooltip("Duration of the flight while rolled in the air.")]
        [SerializeField] private float flightDuration = 0.55f;

        [Tooltip("Jump arc height multiplier during flight.")]
        [SerializeField] private float flightJumpPower = 1.6f;

        [Tooltip("Easing curve for the flight path.")]
        [SerializeField] private Ease flightEase = Ease.InOutQuad;

        [Tooltip("Sorting order while airborne above other sprites.")]
        [SerializeField] private int flightSortingOrder = 100;

        [Header("Reverse Stick & Shine Feedback")]
        [Tooltip("Duration of 3D reverse unroll onto page (1 to 0).")]
        [SerializeField] private float stickDuration = 0.42f;

        [Tooltip("Duration of the diagonal shine ray passing over the sticker upon landing.")]
        [SerializeField] private float shineRayDuration = 0.50f;

        [Header("VFX Prefabs")]
        [SerializeField] private GameObject peelVfxPrefab;
        [SerializeField] private GameObject attachVfxPrefab;
        [SerializeField] private GameObject sparkleVfxPrefab;

        [Header("Events")]
        [SerializeField] private UnityEvent onPeelStarted;
        [SerializeField] private UnityEvent onStickerPlaced;

        private Collider2D col2D;
        private SpriteRenderer spriteRenderer;
        private Vector3 originalLocalScale;
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
            originalLocalScale = transform.localScale;

            if (peelMesh3D == null)
            {
                peelMesh3D = GetComponent<StickerPeel3DMesh>() ?? gameObject.AddComponent<StickerPeel3DMesh>();
            }

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

            // Direct Input System Pointer Check (Mouse & Touch)
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
            if (peelMesh3D != null)
            {
                peelMesh3D.UpdateSortingOrder(flightSortingOrder);
            }

            onPeelStarted?.Invoke();

            // 3. Spawn Peel Dust Particles at the sticker corner
            SpawnVFX(peelVfxPrefab, transform.position);

            // 4. Calculate corner peel tilt
            float chosenAngle = peelMesh3D != null ? peelMesh3D.PickRandomCornerAngle() : 45f;
            float rad = chosenAngle * Mathf.Deg2Rad;
            Vector3 peelTiltAngles = new Vector3(-Mathf.Sin(rad) * peelTiltStrength, Mathf.Cos(rad) * peelTiltStrength, -6f);

            // 5. Construct Sequence: 3D Peel Off -> Flight (Rolled in Air) -> 3D Reverse Stick On Landing
            Vector3 targetPos = targetGhostSlot.TargetPosition;
            Vector3 targetRotEuler = targetGhostSlot.transform.eulerAngles;
            Vector3 targetScale = targetGhostSlot.TargetScale;

            Sequence masterSequence = DOTween.Sequence();
            masterSequence.SetTarget(transform);

            // PHASE 1: SÖKÜLME (0 -> 1) - Silky Smooth 3D corner curl with Ease.InOutSine (0.46s)
            if (peelMesh3D != null)
            {
                masterSequence.Append(peelMesh3D.AnimatePeelOff(peelDuration));
            }
            masterSequence.Join(transform.DOLocalRotate(peelTiltAngles, peelDuration).SetEase(Ease.InOutSine));

            // PHASE 2: UÇUŞ (0.55s) - Sticker havada rulo/arkası dönük haliyle hedef yuvaya doğru uçar
            masterSequence.Append(transform.DOJump(targetPos, flightJumpPower, 1, flightDuration).SetEase(flightEase));
            masterSequence.Join(transform.DORotate(targetRotEuler, flightDuration).SetEase(Ease.OutCubic));
            masterSequence.Join(transform.DOScale(targetScale, flightDuration).SetEase(Ease.OutCubic));

            // PHASE 3: GERİ YAPIŞTIRMA (1 -> 0) (0.42s) - Silky Smooth 3D unroll onto album sheet
            masterSequence.AppendCallback(() =>
            {
                if (targetGhostSlot != null)
                {
                    targetGhostSlot.HideGhost();
                }
            });

            if (peelMesh3D != null)
            {
                masterSequence.Append(peelMesh3D.AnimateReverseUnroll(stickDuration));
            }

            // PHASE 4: Shine Ray Sweep & Sparkles (0.50s)
            masterSequence.OnComplete(OnStickerLanded);
        }

        private void OnStickerLanded()
        {
            isFlying = false;
            isPlaced = true;

            if (targetGhostSlot != null)
            {
                transform.position = targetGhostSlot.TargetPosition;
                transform.rotation = targetGhostSlot.TargetRotation;
                transform.localScale = targetGhostSlot.TargetScale;

                // Match slot sorting order (30)
                if (spriteRenderer != null)
                {
                    spriteRenderer.sortingOrder = targetGhostSlot.PlacedSortingOrder;
                }
                if (peelMesh3D != null)
                {
                    peelMesh3D.UpdateSortingOrder(targetGhostSlot.PlacedSortingOrder);
                }

                // Smooth Light Ray / Shine Sweep over the placed sticker
                if (peelMesh3D != null)
                {
                    peelMesh3D.AnimateShineRay(shineRayDuration);
                }

                // Spawn Sparkle & Attach Burst VFX
                SpawnVFX(attachVfxPrefab, transform.position);
                SpawnVFX(sparkleVfxPrefab, transform.position);

                // Notify target slot
                targetGhostSlot.OnStickerPlaced(this);
            }

            onStickerPlaced?.Invoke();
        }

        private void SpawnVFX(GameObject vfxPrefab, Vector3 pos)
        {
            if (vfxPrefab == null) return;
            GameObject vfxInstance = Instantiate(vfxPrefab, pos, Quaternion.identity);
            Destroy(vfxInstance, 2.0f);
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
