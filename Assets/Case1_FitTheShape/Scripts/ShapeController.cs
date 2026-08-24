using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

        [Header("Pneumatic Sinking & Flush Plug Settings")]
        [Tooltip("Height above the hole the shape lands at first (embossed look on impact).")]
        [SerializeField] private float embossedLandingOffset = 0.12f;

        [Tooltip("Duration of the pneumatic sinking motion into the hole.")]
        [SerializeField] private float sinkDuration = 0.20f;

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
        private bool isSeated = false;
        private Collider shapeCollider;
        private Vector3 originalScale;
        private Vector3 initialDeckPosition;
        private Quaternion initialDeckRotation;
        private Transform initialParent;
        private Sequence activeSequence;
        private GameObject activeTrailInstance;
        private static Material neonFlashMaterial;
        private static List<ShapeController> allTrackedShapes = new List<ShapeController>();

        public bool IsSeated => isSeated;

        public Transform TargetHole
        {
            get => targetHole;
            set
            {
                targetHole = value;
                ResolveAnchors();
            }
        }

        private void Awake()
        {
            EnsureCollider();
            originalScale = transform.localScale;
            initialDeckPosition = transform.position;
            initialDeckRotation = transform.rotation;
            initialParent = transform.parent;

            if (!allTrackedShapes.Contains(this))
            {
                allTrackedShapes.Add(this);
            }

            ResolveAnchors();
            ResolvePedestal();
        }

        private void Start()
        {
            EnsureCollider();
            ResolveAnchors();
            ResolvePedestal();
        }

        private void EnsureCollider()
        {
            if (shapeCollider == null)
            {
                shapeCollider = GetComponent<Collider>();
            }

            if (shapeCollider == null)
            {
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    MeshCollider mc = gameObject.AddComponent<MeshCollider>();
                    mc.convex = true;
                    shapeCollider = mc;
                }
                else
                {
                    shapeCollider = gameObject.AddComponent<BoxCollider>();
                }
            }
            else
            {
                shapeCollider.enabled = true;
            }
        }

        public void ResolveAnchors()
        {
            if (targetHole == null)
            {
                // Auto-find matching column segment on Drum by closest X coordinate
                GameObject drum = GameObject.Find("Drum");
                if (drum != null)
                {
                    float minDist = float.MaxValue;
                    Transform closestSeg = null;
                    for (int i = 0; i < drum.transform.childCount; i++)
                    {
                        Transform child = drum.transform.GetChild(i);
                        if (child.name.StartsWith("Segment_") && child.name.EndsWith("_r0"))
                        {
                            float dx = Mathf.Abs(transform.position.x - child.position.x);
                            if (dx < minDist)
                            {
                                minDist = dx;
                                closestSeg = child;
                            }
                        }
                    }
                    if (closestSeg != null)
                    {
                        targetHole = closestSeg;
                    }
                }
            }

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
                if (closestSlot != null && minDist < 3.5f)
                {
                    deckPedestal = closestSlot;
                }
            }
        }

        private void Update()
        {
            if (isTriggered) return;

            bool pointerPressed = false;
            Vector2 screenPos = Vector2.zero;

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                pointerPressed = true;
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                pointerPressed = true;
                screenPos = Mouse.current.position.ReadValue();
            }

            if (pointerPressed)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(screenPos);
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        if (hit.transform == transform || hit.transform.IsChildOf(transform))
                        {
                            TriggerFitSequence();
                        }
                    }
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
                string holeName = targetHole != null ? targetHole.name : "null";
                Debug.LogWarning($"[ShapeController] Missing lastAnchor on '{gameObject.name}'! TargetHole: {holeName}", this);
                isTriggered = false;
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

            // 🤹 SQUASH & STRETCH (Aşama 1: Fırlarken dikey esneme -> Havada tok doğal boyutuna kavuşma)
            Vector3 launchStretch = new Vector3(originalScale.x * 0.90f, originalScale.y * 1.22f, originalScale.z * 0.90f);
            Sequence scaleSeq = DOTween.Sequence();
            scaleSeq.Append(transform.DOScale(launchStretch, flightToHoverDuration * 0.35f).SetEase(Ease.OutQuad));
            scaleSeq.Append(transform.DOScale(originalScale, flightToHoverDuration * 0.65f).SetEase(Ease.InOutQuad));
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

            // 5. Yuva Ağzına İlk Temas Anı (1. Adım: İlk Çarpma -> Particle, Squash, Flash & Ripple) -> (2. Adım: İçeri Süzülüp Düzleşir)
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

                    Vector3 localFlushPos = parentSeg.InverseTransformPoint(flushSeatedPos);
                    Quaternion localRot = Quaternion.Inverse(parentSeg.rotation) * lastAnchor.rotation;

                    // Kullanıcı isteği: artık kabartılı (embossed) pozisyonda durup ayrıca
                    // sinkDuration kadar süzülmüyor — bu 0.2s'lik ekstra bekleme, delik zaten
                    // kapandıktan SONRA hâlâ görünür bir "kabartı" olarak duruyordu. Direkt nihai
                    // (flush/oturmuş) pozisyona yerleşiyor, delikle TAM AYNI ANDA kapanıyor —
                    // vuruş efektleri (kıvılcım, patlama, ses, ripple) yine embossedLandingPos'ta
                    // (temas noktası) tetikleniyor, sadece şeklin kendisi artık orada asılı kalmıyor.
                    transform.localPosition = localFlushPos;
                    transform.localRotation = localRot;
                    transform.localScale = localShapeScale;

                    StopFlightTrail();

                    // NOT: eski squash&stretch (impactSquash) tween'i buradan kaldırıldı — şekil
                    // artık aynı fonksiyon çağrısı içinde hemen altında SetActive(false) olduğu
                    // için o tween'in tek bir karesi bile render olmadan görünmez kalıyordu
                    // (ölü/gereksiz kod). Vuruş hissi artık kıvılcım + patlama + ses + ripple
                    // efektleriyle veriliyor.

                    // 🌟 İLK ÇARPMA ANINDA TETİKLENEN EFEKTLER (Anında Reaksiyon):
                    // 1. Sürtünme kıvılcımları & Parıltılar
                    SpawnInsertionSparks(embossedLandingPos);

                    // 2. Çok tonlu parlak yıldızlar patlaması
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

                    // 5. Delik ve şekil TAM VURUŞ ANINDA birlikte kapanıyor — HideHoleCutout deliği
                    // kapatır kapatmaz segmentin kendi düz yüzeyi ortaya çıkıyor (ayrı bir "dolu"
                    // kapak objesi gerekmiyor, doğrulanmıştı), o yüzden şekli aynı anda gizlemek
                    // hiçbir görsel boşluk bırakmıyor.
                    HideHoleCutout(parentSeg);
                    gameObject.SetActive(false);
                    isSeated = true;

                    OnShapeEntered?.Invoke();

                    // 🏆 Tüm şekiller deliklerine oturdu mu kontrol et
                    CheckAllShapesSeated();
                }
            });
        }

        /// <summary>
        /// Tüm aktif şekiller oturduğunda tekerlekleri sıra sıra döndürüp oyunu sıfırlayan ana döngü.
        /// </summary>
        public static void CheckAllShapesSeated()
        {
            // Sahnede aktif kullanılan tüm şekilleri filtrele
            List<ShapeController> activeShapes = new List<ShapeController>();
            foreach (var shape in allTrackedShapes)
            {
                if (shape != null && shape.gameObject != null)
                {
                    activeShapes.Add(shape);
                }
            }

            if (activeShapes.Count == 0) return;

            bool allSeated = true;
            foreach (var shape in activeShapes)
            {
                if (!shape.isSeated)
                {
                    allSeated = false;
                    break;
                }
            }

            if (allSeated)
            {
                // 1. Tüm şekiller tamamlandı - Başarı tamamlama sesi
                if (FitTheShapeAudioManager.Instance != null)
                {
                    FitTheShapeAudioManager.Instance.PlaySuccessSound();
                }

                // 2. Kısa bir zafer anı (0.45s) sonrası çarklar sıra sıra dönsün
                DOVirtual.DelayedCall(0.45f, () =>
                {
                    if (WheelReactor.Instance != null)
                    {
                        WheelReactor.Instance.SpinAllColumnsSequence(
                            onHalfway: () =>
                            {
                                // Çarklar dönerken delikleri tekrar açık hale getir
                                RestoreAllHoleCutouts();

                                // Şekilleri yuvalarında (Deck) pop-in animasyonuyla spawnla
                                foreach (var shape in activeShapes)
                                {
                                    if (shape != null)
                                    {
                                        shape.RespawnOnDeck();
                                    }
                                }
                            },
                            onComplete: () =>
                            {
                                // Çarkın dönüşü tamamen bitince şekilleri tekrar tıklanabilir yap
                                foreach (var shape in activeShapes)
                                {
                                    if (shape != null)
                                    {
                                        shape.ResetClickableState();
                                    }
                                }

                                // Çark durdu - Hazır sesi
                                if (FitTheShapeAudioManager.Instance != null)
                                {
                                    FitTheShapeAudioManager.Instance.PlayLaunchSound();
                                }
                            }
                        );
                    }
                });
            }
        }

        /// <summary>
        /// Şekli Deck üzerindeki başlangıç yuvasına geri döndürür ve pop-in animasyonuyla spawnlar.
        /// </summary>
        public void RespawnOnDeck()
        {
            activeSequence?.Kill();
            transform.DOKill();

            if (initialParent != null)
            {
                transform.SetParent(initialParent, true);
            }
            transform.position = initialDeckPosition;
            transform.rotation = initialDeckRotation;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);

            // Pop-in bouncy scale animasyonu (0 -> normal boyut)
            transform.DOScale(originalScale, 0.38f).SetEase(Ease.OutBack, 1.45f);

            // Pedestal butonunda yaylanma geri tepmesi
            if (deckPedestal != null)
            {
                deckPedestal.DOKill();
                deckPedestal.DOPunchScale(new Vector3(0.08f, -0.15f, 0.08f), 0.28f, 6, 0.5f);
            }
        }

        /// <summary>
        /// Çark durduğunda şeklin tıklanabilirliğini yeniden aktifleştirir.
        /// </summary>
        public void ResetClickableState()
        {
            isTriggered = false;
            isSeated = false;
            EnsureCollider();
            if (shapeCollider != null)
            {
                shapeCollider.enabled = true;
            }
        }

        /// <summary>
        /// Çark üzerindeki tüm delik objelerini yeniden görünür/açık hale getirir.
        /// </summary>
        public static void RestoreAllHoleCutouts()
        {
            GameObject drum = GameObject.Find("Drum");
            if (drum != null)
            {
                Transform[] allTrans = drum.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allTrans)
                {
                    if (t == null) continue;
                    string cName = t.name.ToLower();
                    if (cName.Contains("hole") && !cName.Contains("cap"))
                    {
                        t.gameObject.SetActive(true);
                    }
                }
            }
        }

        /// <summary>
        /// Yuvaya oturma anında şeklin renginde parlak bir neon halka patlaması oluşturur.
        /// </summary>
        private void SpawnSegmentNeonFlash(Vector3 spawnPos, Vector3 normalDir, Color flashColor)
        {
            GameObject flashGo = new GameObject("VFX_SegmentNeonFlash");
            flashGo.transform.position = spawnPos + normalDir * 0.04f;
            flashGo.transform.rotation = Quaternion.LookRotation(normalDir);

            ParticleSystem ps = flashGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.duration = 0.28f;
            main.startLifetime = 0.26f;
            main.startSpeed = 0.05f;
            main.startSize = 0.40f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = false;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(flashColor, 0.35f),
                    new GradientColorKey(flashColor, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.40f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.3f);
            sizeCurve.AddKey(0.35f, 1.2f);
            sizeCurve.AddKey(1.0f, 1.6f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            ParticleSystemRenderer psRenderer = flashGo.GetComponent<ParticleSystemRenderer>();
            if (neonFlashMaterial == null)
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
                if (unlitShader != null)
                {
                    neonFlashMaterial = new Material(unlitShader);
                }
            }
            if (neonFlashMaterial != null)
            {
                psRenderer.sharedMaterial = neonFlashMaterial;
            }

            ps.Emit(1);
            Destroy(flashGo, 0.45f);
        }

        private void HideHoleCutout(Transform segmentRoot)
        {
            if (segmentRoot == null) return;

            Transform[] allChildren = segmentRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
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

            Vector3 outwardDir = Camera.main != null ? (Camera.main.transform.position - spawnPos).normalized : Vector3.up;
            Quaternion outwardRot = Quaternion.LookRotation(outwardDir);
            GameObject sparksInstance = Instantiate(insertionSparksPrefab, spawnPos + (outwardDir * 0.04f), outwardRot);
            ParticleSystem[] psList = sparksInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                ps.Play();
            }
            Destroy(sparksInstance, 1.5f);
        }

        private void SpawnStarBurstVfx(Vector3 spawnPos)
        {
            if (starBurstVfxPrefab == null) return;

            Vector3 outwardDir = Camera.main != null ? (Camera.main.transform.position - spawnPos).normalized : Vector3.up;
            Quaternion sprayRot = Quaternion.LookRotation(outwardDir);

            GameObject starBurstInstance = Instantiate(starBurstVfxPrefab, spawnPos + (outwardDir * 0.04f), sprayRot);

            ParticleSystem[] psList = starBurstInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in psList)
            {
                ps.Play();
            }

            Destroy(starBurstInstance, 2.0f);
        }

        private void OnDestroy()
        {
            allTrackedShapes.Remove(this);
            activeSequence?.Kill();
            if (activeTrailInstance != null)
            {
                Destroy(activeTrailInstance);
            }
        }
    }
}
