using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using DG.Tweening;

namespace FitTheShape
{
    [RequireComponent(typeof(Collider))]
    public class ShapeController : MonoBehaviour, IPointerClickHandler
    {
        [Header("Target & Anchor Transforms")]
        [Tooltip("The target segment or hole on the wheel.")]
        [FormerlySerializedAs("targetTransform")]
        [SerializeField] private Transform targetHole;

        [Tooltip("First flight anchor (FrontAnchor_FirstTrans) for anticipation/lift reference.")]
        [SerializeField] private Transform firstAnchor;

        [Tooltip("Last flight anchor (FrontAnchor_LastTrans) for final insertion/fit.")]
        [SerializeField] private Transform lastAnchor;

        [Tooltip("Inward offset so the shape sits snugly inside the hole without sticking out.")]
        [SerializeField] private float slotDepthOffset = 0.12f;

        [Header("Continuous Fluid Flight & Spin Settings")]
        [Tooltip("Height of the parabolic arc towards the camera to clear all drum segments.")]
        [SerializeField] private float arcLiftTowardsCamera = 3.0f;

        [Tooltip("Total duration of the continuous fluid flight from deck to hole.")]
        [SerializeField] private float flightDuration = 0.42f;

        [Tooltip("Scale multiplier during the mid-air peak.")]
        [SerializeField] private float peakScaleMultiplier = 1.2f;

        [Tooltip("Easing curve for the continuous flight path.")]
        [SerializeField] private Ease flightEase = Ease.InOutQuad;

        [Tooltip("Easing curve for the mid-air local Y spin.")]
        [SerializeField] private Ease spinEase = Ease.InOutQuad;

        [Header("Golden Star VFX & Polish Feedback")]
        [Tooltip("Golden star particle burst prefab instantiated on landing.")]
        [SerializeField] private GameObject starBurstVfxPrefab;

        [Tooltip("Flight golden star trail prefab attached during movement.")]
        [SerializeField] private GameObject flightTrailPrefab;

        [Tooltip("Insertion friction sparks prefab spawned at the hole rim on landing.")]
        [SerializeField] private GameObject insertionSparksPrefab;

        [Header("Events & Audio")]
        [SerializeField] private AudioSource sfxOnEntered;
        [SerializeField] private UnityEvent OnShapeMoveStarted;
        [SerializeField] private UnityEvent OnShapeEntered;

        private bool isTriggered = false;
        private Collider shapeCollider;
        private Vector3 originalScale;
        private Sequence activeSequence;
        private GameObject activeTrailInstance;

        public Transform TargetHole
        {
            get => targetHole;
            set
            {
                targetHole = value;
                ResolveAnchors();
            }
        }

        public Transform FirstAnchor
        {
            get => firstAnchor;
            set => firstAnchor = value;
        }

        public Transform LastAnchor
        {
            get => lastAnchor;
            set => lastAnchor = value;
        }

        public GameObject StarBurstVfxPrefab
        {
            get => starBurstVfxPrefab;
            set => starBurstVfxPrefab = value;
        }

        public GameObject FlightTrailPrefab
        {
            get => flightTrailPrefab;
            set => flightTrailPrefab = value;
        }

        public GameObject InsertionSparksPrefab
        {
            get => insertionSparksPrefab;
            set => insertionSparksPrefab = value;
        }

        public float FlightDuration
        {
            get => flightDuration;
            set => flightDuration = value;
        }

        public float ArcLiftTowardsCamera
        {
            get => arcLiftTowardsCamera;
            set => arcLiftTowardsCamera = value;
        }

        private void Awake()
        {
            shapeCollider = GetComponent<Collider>();
            originalScale = transform.localScale;
            ResolveAnchors();

            if (starBurstVfxPrefab == null)
            {
                starBurstVfxPrefab = Resources.Load<GameObject>("MiniShapeBurst");
            }
            if (flightTrailPrefab == null)
            {
                flightTrailPrefab = Resources.Load<GameObject>("FlightTrailDust");
            }
            if (insertionSparksPrefab == null)
            {
                insertionSparksPrefab = Resources.Load<GameObject>("InsertionSparks");
            }
        }

        private void OnValidate()
        {
            ResolveAnchors();
        }

        public void ResolveAnchors()
        {
            if (targetHole == null) return;

            Transform searchRoot = targetHole.name.Contains("Segment_") ? targetHole : targetHole.parent;
            if (searchRoot == null) searchRoot = targetHole;

            if (firstAnchor == null)
            {
                firstAnchor = searchRoot.Find("FrontAnchor_FirstTrans");
            }

            if (lastAnchor == null)
            {
                lastAnchor = searchRoot.Find("FrontAnchor_LastTrans");
            }
        }

        private void OnMouseDown()
        {
            TriggerFitSequence();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerFitSequence();
        }

        public void TriggerFitSequence()
        {
            if (isTriggered) return;
            isTriggered = true;

            ResolveAnchors();

            if (lastAnchor == null)
            {
                Debug.LogWarning($"[ShapeController] Missing lastAnchor on '{gameObject.name}'! TargetHole: {targetHole?.name}", this);
                return;
            }

            if (shapeCollider != null)
            {
                shapeCollider.enabled = false;
            }

            // 1. Audio: Whoosh / Launch Sound (Anında tetikleme)
            if (FitTheShapeAudioManager.Instance != null)
            {
                FitTheShapeAudioManager.Instance.PlayLaunchSound();
            }

            OnShapeMoveStarted?.Invoke();

            activeSequence?.Kill();
            activeSequence = DOTween.Sequence();

            Vector3 startPos = transform.position;
            Vector3 sunkenTargetPos = lastAnchor.position - (lastAnchor.up * slotDepthOffset);

            StartFlightTrail();

            Camera mainCam = Camera.main;
            Vector3 toCameraDir = mainCam != null ? -mainCam.transform.forward : new Vector3(0f, 0.94f, -0.34f);
            
            Vector3 arcGuidePos = firstAnchor != null ? firstAnchor.position : Vector3.Lerp(startPos, sunkenTargetPos, 0.5f);
            Vector3 arcMidPoint = Vector3.Lerp(startPos, arcGuidePos, 0.6f) + toCameraDir * arcLiftTowardsCamera;

            Vector3[] continuousPath = new Vector3[] {
                startPos,
                arcMidPoint,
                sunkenTargetPos
            };

            // 1. Kesintisiz Akıcı Uçuş
            activeSequence.Append(transform.DOPath(continuousPath, flightDuration, PathType.CatmullRom).SetEase(flightEase));

            // 2. Havada Uçarken SADECE Kendi Local Y Ekseninde 1 Tam Tur (360°) Spin
            activeSequence.Join(transform.DORotate(new Vector3(0f, 360f, 0f), flightDuration, RotateMode.LocalAxisAdd).SetEase(spinEase));

            // 3. Havada hafif scale büyümesi ve inişte tam orijinal scale'e oturması
            Sequence scaleSeq = DOTween.Sequence();
            scaleSeq.Append(transform.DOScale(originalScale * peakScaleMultiplier, flightDuration * 0.45f).SetEase(Ease.OutQuad));
            scaleSeq.Append(transform.DOScale(originalScale, flightDuration * 0.55f).SetEase(Ease.InQuad));
            activeSequence.Join(scaleSeq);

            // 4. Snap Sesini tam temas anından bir kare önce (0 gecikme hissi) tetikle
            activeSequence.InsertCallback(Mathf.Max(0f, flightDuration - 0.02f), () =>
            {
                if (FitTheShapeAudioManager.Instance != null)
                {
                    FitTheShapeAudioManager.Instance.PlaySnapImpactSound();
                    FitTheShapeAudioManager.Instance.PlaySuccessSound();
                }
            });

            // 5. Tam yuvasına indiğinde
            activeSequence.OnComplete(() =>
            {
                transform.position = sunkenTargetPos;
                transform.rotation = lastAnchor.rotation;
                transform.localScale = originalScale;

                StopFlightTrail();

                // 1. Yuvaya giriş sürtünme kıvılcımı (Insertion Friction Sparks)
                SpawnInsertionSparks(sunkenTargetPos);

                // 2. Çok tonlu parlak sarı yıldızlar + minik kıvılcım tozları patlaması
                SpawnStarBurstVfx(sunkenTargetPos);

                if (sfxOnEntered != null)
                {
                    sfxOnEntered.Play();
                }

                // Dalgayı şekil referansıyla birlikte başlat
                if (WheelReactor.Instance != null)
                {
                    WheelReactor.Instance.TriggerReaction(lastAnchor, transform);
                }

                OnShapeEntered?.Invoke();
            });
        }

        private void StartFlightTrail()
        {
            if (flightTrailPrefab == null) return;

            activeTrailInstance = Instantiate(flightTrailPrefab, transform.position, Quaternion.identity, transform);
            
            ParticleSystem[] psList = activeTrailInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                ps.Play();
            }
        }

        private void StopFlightTrail()
        {
            if (activeTrailInstance == null) return;

            activeTrailInstance.transform.SetParent(null, true);
            ParticleSystem[] psList = activeTrailInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            Destroy(activeTrailInstance, 1.5f);
            activeTrailInstance = null;
        }

        private void SpawnInsertionSparks(Vector3 spawnPos)
        {
            if (insertionSparksPrefab == null) return;

            Quaternion outwardRot = Quaternion.LookRotation(lastAnchor != null ? lastAnchor.up : Vector3.up);
            GameObject sparksInstance = Instantiate(insertionSparksPrefab, spawnPos, outwardRot);
            ParticleSystem ps = sparksInstance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }
            Destroy(sparksInstance, 1.5f);
        }

        private void SpawnStarBurstVfx(Vector3 spawnPos)
        {
            if (starBurstVfxPrefab == null) return;

            Vector3 upwardDir = (Vector3.up * 0.78f + (lastAnchor != null ? lastAnchor.up : Vector3.forward) * 0.40f).normalized;
            Quaternion sprayRot = Quaternion.LookRotation(upwardDir);

            GameObject starBurstInstance = Instantiate(starBurstVfxPrefab, spawnPos, sprayRot);

            ParticleSystem[] psList = starBurstInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                ps.Play();
            }

            Destroy(starBurstInstance, 2.0f);
        }

        private void OnDestroy()
        {
            activeSequence?.Kill();
            if (activeTrailInstance != null)
            {
                Destroy(activeTrailInstance);
            }
        }
    }
}
