using System;
using UnityEngine;
using DG.Tweening;

namespace Stickerdom
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class StickerPeelController : MonoBehaviour
    {
        private static readonly float[] CornerAngles = new float[] { 45f, 135f, 225f, 315f };

        [Header("Peel Configuration")]
        [Tooltip("Direction angle in degrees from which the corner curls up.")]
        [Range(0f, 360f)] [SerializeField] private float peelAngle = 45.0f;

        [Tooltip("Crease roll width.")]
        [Range(0.01f, 0.4f)] [SerializeField] private float rollRadius = 0.10f;

        [Tooltip("Backside adhesive color.")]
        [SerializeField] private Color backSideColor = new Color(0.93f, 0.93f, 0.94f, 1.0f);

        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propBlock;

        private static readonly int PropPeelProgress = Shader.PropertyToID("_PeelProgress");
        private static readonly int PropPeelAngle = Shader.PropertyToID("_PeelAngle");
        private static readonly int PropRollRadius = Shader.PropertyToID("_RollRadius");
        private static readonly int PropBackSideColor = Shader.PropertyToID("_BackSideColor");

        private float currentPeelProgress = 0f;

        public float PeelProgress
        {
            get => currentPeelProgress;
            set
            {
                currentPeelProgress = value;
                ApplyProperties();
            }
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            propBlock = new MaterialPropertyBlock();
            ApplyInitialShaderSettings();
        }

        private void ApplyInitialShaderSettings()
        {
            if (spriteRenderer == null) return;

            spriteRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(PropPeelAngle, peelAngle);
            propBlock.SetFloat(PropRollRadius, rollRadius);
            propBlock.SetColor(PropBackSideColor, backSideColor);
            propBlock.SetFloat(PropPeelProgress, currentPeelProgress);
            spriteRenderer.SetPropertyBlock(propBlock);
        }

        public void ApplyProperties()
        {
            if (spriteRenderer == null) return;

            spriteRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(PropPeelAngle, peelAngle);
            propBlock.SetFloat(PropPeelProgress, currentPeelProgress);
            spriteRenderer.SetPropertyBlock(propBlock);
        }

        /// <summary>
        /// Selects a random corner angle (45°, 135°, 225°, 315°) for peeling.
        /// </summary>
        public float PickRandomCornerAngle()
        {
            float chosen = CornerAngles[UnityEngine.Random.Range(0, CornerAngles.Length)];
            SetPeelAngle(chosen);
            return chosen;
        }

        public void SetPeelAngle(float angle)
        {
            peelAngle = angle;
            ApplyProperties();
        }

        /// <summary>
        /// SÖKÜLME: 0 -> 1.05 (Sticker köşeden başlanarak rulo şeklinde sökülür, arkası öne katlanarak ayrılır).
        /// </summary>
        public Tween AnimatePeelOff(float duration = 0.24f)
        {
            return DOTween.To(() => currentPeelProgress, x => PeelProgress = x, 1.05f, duration).SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// Sökülmeyi sıfırlar.
        /// </summary>
        public void ResetPeel()
        {
            currentPeelProgress = 0f;
            ApplyProperties();
        }
    }
}
