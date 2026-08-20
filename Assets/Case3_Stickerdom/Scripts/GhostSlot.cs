using System;
using UnityEngine;

namespace Stickerdom
{
    public enum StickerType
    {
        Hayvan = 0,
        Meyve = 1,
        Arac = 2,
        Doga = 3,
        Kumsal = 4,
        Enstruman = 5
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public class GhostSlot : MonoBehaviour
    {
        [Header("Slot Identity")]
        [Tooltip("Type/Shape of the sticker matching this slot.")]
        [SerializeField] private StickerType stickerType;

        [Header("State")]
        [SerializeField] private bool isOccupied = false;

        [Header("Visuals")]
        [Tooltip("Sorting order for the placed sticker.")]
        [SerializeField] private int placedSortingOrder = 20;

        [Tooltip("Alpha opacity of ghost outline before sticker placement.")]
        [Range(0f, 1f)] [SerializeField] private float ghostInitialAlpha = 1.0f;

        [Tooltip("Alpha opacity of ghost outline after sticker is placed.")]
        [Range(0f, 1f)] [SerializeField] private float ghostCompletedAlpha = 0.0f;

        private SpriteRenderer spriteRenderer;

        public StickerType StickerType => stickerType;
        public bool IsOccupied => isOccupied;
        public int PlacedSortingOrder => placedSortingOrder;
        public Vector3 TargetPosition => transform.position;
        public Quaternion TargetRotation => transform.rotation;
        public Vector3 TargetScale => transform.localScale;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            UpdateGhostVisual();
        }

        public void SetOccupied(bool occupied)
        {
            isOccupied = occupied;
            UpdateGhostVisual();
        }

        public void OnStickerPlaced(StickerClickable sticker)
        {
            SetOccupied(true);
        }

        private void UpdateGhostVisual()
        {
            if (spriteRenderer == null) return;
            Color c = spriteRenderer.color;
            c.a = isOccupied ? ghostCompletedAlpha : ghostInitialAlpha;
            spriteRenderer.color = c;
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
