using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using DG.Tweening;

namespace FitTheShape
{
    [RequireComponent(typeof(Collider))]
    public class ShapeController : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        [Header("Target & Anchor Transforms")]
        [Tooltip("The target segment or hole on the wheel.")]
        [FormerlySerializedAs("targetTransform")]
        [SerializeField] private Transform targetHole;

        [Tooltip("First flight anchor (FrontAnchor_FirstTrans) for anticipation/lift reference.")]
        [SerializeField] private Transform firstAnchor;

        [Tooltip("Last flight anchor (FrontAnchor_LastTrans) for final insertion/fit.")]
        [SerializeField] private Transform lastAnchor;

        [Header("Deck Pedestal Spring Feedback")]
        [Tooltip("Optional reference to the pedestal/button under this shape. Auto-finds closest DeckSlot if null.")]
        [SerializeField] private Transform deckPedestal;

        [Tooltip("How much the button pedestal depresses down on tap.")]
        [SerializeField] private float pedestalPunchDepth = 0.14f;

        [Tooltip("Duration of the springy button press recoil.")]
        [SerializeField] private float pedestalPunchDuration = 0.25f;

        [Header("3-Stage Flight: Arc Fly -> Hover Align -> Snap Plunge")]
        [Tooltip("Height of the parabolic arc towards the camera to clear all drum segments.")]
        [SerializeField] private float arcLiftTowardsCamera = 3.0f;

        [Tooltip("Duration of the flight from deck to overhead alignment position.")]
        [SerializeField] private float flightToHoverDuration = 0.36f;

        [Tooltip("Brief hover pause right above the hole while fully leveled/aligned.")]
        [SerializeField] private float overheadHoverDuration = 0.08f;

        [Tooltip("Fast snappy plunge straight down into the hole ('TAK!' oturma).")]
        [SerializeField] private float snapPlungeDuration = 0.09f;

        [Tooltip("Height above hole for overhead hover alignment.")]
        [SerializeField] private float overheadHoverHeight = 0.70f;

        [Tooltip("Scale multiplier during the mid-air peak.")]
        [SerializeField] private float peakScaleMultiplier = 1.2f;

        [Header("Pneumatic Sinking & Flush Plug Settings")]
        [Tooltip("Height above the hole the shape lands at first (embossed look on impact).")]
        [SerializeField] private float embossedLandingOffset = 0.12f;

        [Tooltip("Thickness scale on Y when touching down in the slot (embossed 3D look).")]
        [SerializeField] private float embossedThickness = 0.35f;

        [Tooltip("Thickness scale on Y when fully seated flush into the hole.")]
        [SerializeField] private float flushPlugThickness = 0.12f;

        [Tooltip("Duration of the pneumatic sinking motion into the hole.")]
        [SerializeField] private float sinkDuration = 0.28f;

        [Tooltip("Easing curve for the pneumatic sinking motion.")]
        [SerializeField] private Ease sinkEase = Ease.OutQuad;

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

        public Transform DeckPedestal
        {
            get => deckPedestal;
            set => deckPedestal = value;
        }

        private void Awake()
        {
            shapeCollider = GetComponent<Collider>();
            originalScale = transform.localScale;
            ResolveAnchors();
            ResolvePedestal();

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
            ResolvePedestal();
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

        public void ResolvePedestal()
        {
            if (deckPedestal != null) return;

            GameObject deck = GameObject.Find("Deck");
            if (deck != null)
            {
                float minDist = float.MaxValue;
                Transform closestSlot = null;
                for (int i = 0; i < deck.transform.childCount; i++)
                {
                    Transform child = deck.transform.GetChild(i);
                    if (child.name.StartsWith("DeckSlot"))
                    {
                        float d = Vector3.Distance(transform.position, child.position);
                        if (d < minDist)
                        {
                            minDist = d;
                            closestSlot = child;
                        }
                    }
                }
                if (closestSlot != null && minDist < 2.5f)
                {
                    deckPedestal = closestSlot;
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
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
            ResolvePedestal();

            if (lastAnchor == null)
            {
                Debug.LogWarning($"[ShapeController] Missing lastAnchor on '{gameObject.name}'! TargetHole: {targetHole?.name}", this);
                return;
            }

            if (shapeCollider != null)
            {
                shapeCollider.enabled = false;
            }

            // 0. Deck Pedestal Spring Button Reaction (Yaylı Buton Basış Geri Tepmesi)
            if (deckPedestal != null)
            {
                deckPedestal.DOKill();
                deckPedestal.DOPunchPosition(Vector3.down * pedestalPunchDepth, pedestalPunchDuration, 6, 0.5f);
                deckPedestal.DOPunchScale(new Vector3(0.08f, -0.15f, 0.08f), pedestalPunchDuration, 6, 0.5f);
            }

            // 1. Audio: Whoosh / Launch Sound
            if (FitTheShapeAudioManager.Instance != null)
            {
                FitTheShapeAudioManager.Instance.PlayLaunchSound();
            }

            OnShapeMoveStarted?.Invoke();

            activeSequence?.Kill();
            activeSequence = DOTween.Sequence();

            Camera mainCam = Camera.main;
            Vector3 camUp = mainCam != null ? mainCam.transform.up : Vector3.up;
            Vector3 toCameraDir = mainCam != null ? -mainCam.transform.forward : new Vector3(0f, 0.94f, -0.34f);

            Vector3 startPos = transform.position;
            Vector3 holeNormal = lastAnchor.up;

            // Game ekranında deliğin hemen üstünde tatlı ve dengeli bir mesafede beklemesi için ofset
            Vector3 overheadApproachPos = lastAnchor.position + (camUp * 0.45f) + (holeNormal * 0.22f);
            Vector3 embossedLandingPos = lastAnchor.position + (holeNormal * embossedLandingOffset);
            Vector3 flushSeatedPos = lastAnchor.position;

            StartFlightTrail();

            Vector3 arcGuidePos = firstAnchor != null ? firstAnchor.position : Vector3.Lerp(startPos, overheadApproachPos, 0.5f);
            Vector3 arcMidPoint = Vector3.Lerp(startPos, arcGuidePos, 0.55f) + toCameraDir * arcLiftTowardsCamera;

            Vector3[] arcPath = new Vector3[] {
                startPos,
                arcMidPoint,
                overheadApproachPos
            };

            Quaternion targetRot = lastAnchor.rotation;
            float ySpinDir = UnityEngine.Random.value > 0.5f ? 180f : -180f;
            Vector3 spinAngles = new Vector3(0f, ySpinDir, 0f);

            // AŞAMA 1: Butona basıldığı an deliğin tam üst hizasına kavisli akıcı uçuş başlar
            activeSequence.Append(transform.DOPath(arcPath, flightToHoverDuration, PathType.CatmullRom).SetEase(Ease.OutQuad));

            // Havada sakin 180 derece dönüş ve deliğin üstüne varırken YUVA AÇISIYLA TAM DÜZLEŞME
            Sequence rotSeq = DOTween.Sequence();
            rotSeq.Append(transform.DORotate(spinAngles, flightToHoverDuration * 0.65f, RotateMode.LocalAxisAdd).SetEase(Ease.Linear));
            rotSeq.Append(transform.DORotate(targetRot.eulerAngles, flightToHoverDuration * 0.35f, RotateMode.Fast).SetEase(Ease.OutQuad));
            activeSequence.Join(rotSeq);

            // Boyut ölçeklemesi (Kesinlikle incelme/yassılaşma yok, daima tam ve tok 3D kalınlığını korur)
            Vector3 peakScale = originalScale * peakScaleMultiplier;
            Sequence scaleSeq = DOTween.Sequence();
            scaleSeq.Append(transform.DOScale(peakScale, flightToHoverDuration * 0.45f).SetEase(Ease.OutQuad));
            scaleSeq.Append(transform.DOScale(originalScale, flightToHoverDuration * 0.55f).SetEase(Ease.InQuad));
            activeSequence.Join(scaleSeq);

            // AŞAMA 2: Uçuş bitip deliğin tam üstüne gelindiğinde kısa bir an bekleme (Anticipation)
            activeSequence.AppendInterval(overheadHoverDuration);

            // AŞAMA 3: Deliğe tam 90 derece dikey ve hızlı 'TAK!' diye oturma dalışı
            activeSequence.Append(transform.DOMove(embossedLandingPos, snapPlungeDuration).SetEase(Ease.InQuad));

            // Tam 'TAK!' çarpma anında ses tetikleme
            activeSequence.InsertCallback(flightToHoverDuration + overheadHoverDuration + snapPlungeDuration * 0.3f, () =>
            {
                if (FitTheShapeAudioManager.Instance != null)
                {
                    FitTheShapeAudioManager.Instance.PlaySnapImpactSound();
                }
            });

            // 5. Yuva Ağzına İlk Temas Anı (1. Adım: İlk Çarpma -> Particle & Ripple Anında Patlar) -> (2. Adım: İçeri Süzülüp Düzleşir)
            activeSequence.OnComplete(() =>
            {
                // Gölge objesini kapat
                foreach (Transform child in transform)
                {
                    if (child.name.ToLower().Contains("shadow"))
                    {
                        child.gameObject.SetActive(false);
                    }
                }

                // Segment objesine bağla (böylece gelecekteki tüm Ripple dalgalarında segment ile %100 kilitli hareket eder)
                Transform parentSeg = targetHole != null && targetHole.name.Contains("Segment_") 
                    ? targetHole 
                    : (lastAnchor != null ? lastAnchor.parent : targetHole);

                if (parentSeg != null)
                {
                    transform.SetParent(parentSeg, true);

                    // Segment'in asimetrik (non-uniform) scale'ini (0.87, 1.47, 1.47) ters çarpanla (inverse) dengele!
                    // Böylece dünya koordinatlarında şekil %100 kusursuz 3D oranını korur, ASLA İNCELMEZ VE STRETCH OLMAZ!
                    Vector3 pLossy = parentSeg.lossyScale;
                    float invX = pLossy.x > 0.001f ? 1f / pLossy.x : 1f;
                    float invY = pLossy.y > 0.001f ? 1f / pLossy.y : 1f;
                    float invZ = pLossy.z > 0.001f ? 1f / pLossy.z : 1f;

                    Vector3 localShapeScale = new Vector3(originalScale.x * invX, originalScale.y * invY, originalScale.z * invZ);

                    Vector3 localEmbossedPos = parentSeg.InverseTransformPoint(embossedLandingPos);
                    Vector3 localFlushPos = parentSeg.InverseTransformPoint(flushSeatedPos);
                    Quaternion localRot = Quaternion.Inverse(parentSeg.rotation) * lastAnchor.rotation;

                    transform.localPosition = localEmbossedPos;
                    transform.localRotation = localRot;
                    transform.localScale = localShapeScale;

                    StopFlightTrail();

                    // 🌟 Şekil deliğe girmeye başladığı tam bu anda Hole ve Hole-Cap objelerini kapat!
                    HideHoleCutout(parentSeg);

                    // 🌟 İLK ÇARPMA ANINDA TETİKLENEN EFEKTLER (Anında Reaksiyon):
                    // 1. Sürtünme kıvılcımları
                    SpawnInsertionSparks(embossedLandingPos);

                    // 2. Çok tonlu parlak sarı yıldızlar patlaması
                    SpawnStarBurstVfx(embossedLandingPos);

                    // 3. Başarı parıltı sesi (Sparkle Chime)
                    if (FitTheShapeAudioManager.Instance != null)
                    {
                        FitTheShapeAudioManager.Instance.PlaySuccessSound();
                    }

                    // 4. Çarktaki rezonans süspansiyon dalgası (Ripple)
                    if (WheelReactor.Instance != null)
                    {
                        WheelReactor.Instance.TriggerReaction(lastAnchor, null);
                    }

                    // 🌟 2. AŞAMA: Segment'in içinde pürüzsüzce içeri süzülerek yüzeye kilitlenme
                    transform.DOLocalMove(localFlushPos, sinkDuration).SetEase(sinkEase).OnComplete(() =>
                    {
                        transform.localPosition = localFlushPos;
                        transform.localRotation = localRot;

                        // Şekil deliği doldurup oturduktan sonra Hole objesini kapat ve yüzeyin DÜMDÜZ kalmasını sağla
                        HideHoleCutout(parentSeg);
                        gameObject.SetActive(false);

                        OnShapeEntered?.Invoke();
                    });
                }
            });
        }

        private void HideHoleCutout(Transform segmentRoot)
        {
            if (segmentRoot == null) return;

            // Segment ve altındaki tüm Hole / Hole-Cap objelerini eksiksiz bulup kapat
            Transform[] allTrans = segmentRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTrans)
            {
                if (t == null) continue;
                string cName = t.name.ToLower();
                if (cName.Contains("hole") || cName.Contains("hole-cap") || cName.Contains("cutout"))
                {
                    t.gameObject.SetActive(false);
                }
            }

            if (targetHole != null && targetHole != segmentRoot)
            {
                Transform[] targetTrans = targetHole.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in targetTrans)
                {
                    if (t == null) continue;
                    string cName = t.name.ToLower();
                    if (cName.Contains("hole") || cName.Contains("hole-cap") || cName.Contains("cutout"))
                    {
                        t.gameObject.SetActive(false);
                    }
                }
            }
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
