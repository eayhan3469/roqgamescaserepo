using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Stickerdom
{
    public class StickerLevelManager : MonoBehaviour
    {
        public static StickerLevelManager Instance { get; private set; }

        [Header("Draw Pile Reference (Sağ Alttaki Mor Obje)")]
        [Tooltip("The purple deck/pile in the bottom-right corner from which the 4th sticker emerges.")]
        [SerializeField] private Transform drawPile;

        [Header("Album & Waiting Roots")]
        [Tooltip("Parent of waiting stickers.")]
        [SerializeField] private Transform waitingStickersRoot;

        [Tooltip("Album page / sheet transform for victory celebration bounce.")]
        [SerializeField] private Transform pageSheet;

        [Header("Stickers & Slots")]
        [SerializeField] private List<StickerClickable> initialStickers = new List<StickerClickable>();
        [SerializeField] private List<GhostSlot> ghostSlots = new List<GhostSlot>();

        [Header("4th Sticker (Doga) Setup")]
        [Tooltip("Reference to the 4th sticker (Doga). If null, will be auto-found or dynamically instantiated.")]
        [SerializeField] private StickerClickable fourthSticker;

        [Tooltip("Sprite for sticker_doga.png.")]
        [SerializeField] private Sprite dogaSprite;

        [Tooltip("Target resting position for the 4th sticker after being drawn from the DrawPile.")]
        [SerializeField] private Vector3 fourthStickerRestPosition = new Vector3(0f, -6.45f, 0f);

        [Header("Draw Animation Settings")]
        [Tooltip("Delay before the 4th sticker emerges from DrawPile after the 3rd sticker lands.")]
        [SerializeField] private float fourthStickerDrawDelay = 0.35f;

        [Tooltip("Flight duration of the 4th sticker emerging from the purple pile.")]
        [SerializeField] private float drawFlightDuration = 0.65f;

        [Tooltip("Jump arc height of the 4th sticker during draw flight.")]
        [SerializeField] private float drawJumpPower = 2.4f;

        [Header("Victory & Auto-Restart Settings")]
        [Tooltip("Delay in seconds before the level automatically restarts after all 4 stickers are placed.")]
        [SerializeField] private float autoRestartDelay = 2.2f;

        private int placedStickerCount = 0;
        private bool isDrawingFourthSticker = false;
        private bool isRestarting = false;

        public int PlacedStickerCount => placedStickerCount;
        public bool IsRestarting => isRestarting;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            AutoFindReferences();
        }

        private void Start()
        {
            SetupFourthSticker();
            CacheInitialStickers();
        }

        public void AutoFindReferences()
        {
            if (drawPile == null)
            {
                GameObject dpObj = GameObject.Find("DrawPile");
                if (dpObj != null) drawPile = dpObj.transform;
            }

            if (pageSheet == null)
            {
                GameObject pageObj = GameObject.Find("PageSheet") ?? GameObject.Find("Page");
                if (pageObj != null) pageSheet = pageObj.transform;
            }

            if (waitingStickersRoot == null)
            {
                GameObject waitObj = GameObject.Find("WaitingStickers");
                if (waitObj != null) waitingStickersRoot = waitObj.transform;
            }

            // Find all GhostSlots
            ghostSlots.Clear();
            GhostSlot[] allSlots = FindObjectsOfType<GhostSlot>();
            ghostSlots.AddRange(allSlots);

            // Find initial stickers & 4th sticker (including inactive in hierarchy)
            initialStickers.Clear();
            if (waitingStickersRoot != null)
            {
                StickerClickable[] childStickers = waitingStickersRoot.GetComponentsInChildren<StickerClickable>(true);
                foreach (var st in childStickers)
                {
                    if (st == null) continue;
                    if (st.name.Contains("Doga") || st.StickerType == StickerType.Doga)
                    {
                        fourthSticker = st;
                    }
                    else if (!initialStickers.Contains(st))
                    {
                        initialStickers.Add(st);
                    }
                }
            }

            if (initialStickers.Count == 0)
            {
                StickerClickable[] allStickers = FindObjectsOfType<StickerClickable>();
                foreach (var st in allStickers)
                {
                    if (st != null && st.StickerType != StickerType.Doga && !initialStickers.Contains(st))
                    {
                        initialStickers.Add(st);
                    }
                }
            }
        }

        private void SetupFourthSticker()
        {
            if (dogaSprite == null)
            {
                dogaSprite = Resources.Load<Sprite>("Stickers/sticker_doga");
            }
#if UNITY_EDITOR
            if (dogaSprite == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("sticker_doga t:Sprite");
                if (guids != null && guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    dogaSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
#endif

            // Look for existing Sticker_Doga if not found yet
            if (fourthSticker == null && waitingStickersRoot != null)
            {
                Transform dogaT = waitingStickersRoot.Find("Sticker_Doga");
                if (dogaT != null)
                {
                    fourthSticker = dogaT.GetComponent<StickerClickable>();
                }
            }

            if (fourthSticker != null)
            {
                fourthSticker.StickerType = StickerType.Doga;

                // Ensure sprite is sticker_doga
                SpriteRenderer sr = fourthSticker.GetComponent<SpriteRenderer>();
                if (sr != null && dogaSprite != null)
                {
                    sr.sprite = dogaSprite;
                    if (fourthSticker.GetComponent<BoxCollider2D>() is BoxCollider2D box)
                    {
                        box.size = dogaSprite.rect.size / dogaSprite.pixelsPerUnit;
                    }
                }

                // Connect to Ghost_Doga
                GhostSlot dogaSlot = ghostSlots.Find(s => s != null && s.StickerType == StickerType.Doga);
                if (dogaSlot == null)
                {
                    GhostSlot[] allSlots = FindObjectsOfType<GhostSlot>();
                    foreach (var s in allSlots)
                    {
                        if (s != null && s.StickerType == StickerType.Doga)
                        {
                            dogaSlot = s;
                            break;
                        }
                    }
                }
                fourthSticker.TargetGhostSlot = dogaSlot;

                // Hide at initial start
                fourthSticker.gameObject.SetActive(false);
            }
        }

        private void CacheInitialStickers()
        {
            placedStickerCount = 0;
            isDrawingFourthSticker = false;
            isRestarting = false;
        }

        /// <summary>
        /// StickerClickable tarafından sticker hedefine ulaştığında çağrılır.
        /// </summary>
        public void OnStickerPlaced(StickerClickable sticker)
        {
            if (isRestarting) return;

            placedStickerCount++;
            Debug.Log($"[StickerLevelManager] Sticker placed: {sticker.name} (Total: {placedStickerCount}/4)");

            // 1. Durum: İlk 3 sticker yapıştırıldı -> 4. sticker (Doga) sağ alttaki mor objeden çıksın!
            if (placedStickerCount == 3 && !isDrawingFourthSticker)
            {
                isDrawingFourthSticker = true;
                StartCoroutine(DrawFourthStickerFromPileRoutine());
            }
            // 2. Durum: 4 sticker'ın hepsi de yapıştırıldı -> Zafer & Otomatik Baştan Başlama!
            else if (placedStickerCount >= 4)
            {
                StartCoroutine(VictoryAndAutoRestartRoutine());
            }
        }

        /// <summary>
        /// 4. Sticker'ın (Doga) sağ alttaki mor objeden (DrawPile) fırlayıp masaya yerleşme sekansı.
        /// </summary>
        private IEnumerator DrawFourthStickerFromPileRoutine()
        {
            yield return new WaitForSeconds(fourthStickerDrawDelay);

            Vector3 spawnPos = drawPile != null ? drawPile.position : new Vector3(4.75f, -10.45f, 0f);

            // 1. Mor Obje Squash & Stretch / Pop Animasyonu
            if (drawPile != null)
            {
                drawPile.DOKill();
                drawPile.DOPunchScale(new Vector3(0.22f, -0.18f, 0.22f), 0.45f, 6, 0.5f);
            }

            // 2. Ses ve Parçacık Efekti (Draw Swoosh & Puff)
            if (StickerAudioManager.Instance != null)
            {
                StickerAudioManager.Instance.PlayFlySound();
            }

            if (StickerVFXManager.Instance != null)
            {
                StickerVFXManager.Instance.PlayPeelVFX(spawnPos);
            }

            // 3. 4. Sticker'ı Mor Objenin Merkezinde Başlat
            if (fourthSticker != null)
            {
                fourthSticker.gameObject.SetActive(true);
                fourthSticker.ResetSticker();

                Transform stT = fourthSticker.transform;
                stT.position = spawnPos;
                stT.localScale = Vector3.zero;
                stT.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-15f, 15f));

                // 4. Parabolik Uçuş ile Bekleme Alanına (Masa Kartına) Fırlama
                Sequence drawSeq = DOTween.Sequence();
                drawSeq.Append(stT.DOJump(fourthStickerRestPosition, drawJumpPower, 1, drawFlightDuration).SetEase(Ease.OutQuad));
                drawSeq.Join(stT.DOScale(Vector3.one * 0.8f, drawFlightDuration).SetEase(Ease.OutBack, 1.35f));
                drawSeq.Join(stT.DORotate(Vector3.zero, drawFlightDuration).SetEase(Ease.OutCubic));

                drawSeq.OnComplete(() =>
                {
                    // Masaya iniş vuruşu & pul efekti
                    stT.DOPunchScale(new Vector3(0.12f, -0.12f, 0.12f), 0.25f, 5, 0.5f);

                    if (StickerAudioManager.Instance != null)
                    {
                        StickerAudioManager.Instance.PlayStampSound();
                    }

                    if (StickerVFXManager.Instance != null)
                    {
                        StickerVFXManager.Instance.PlayStampVFX(fourthStickerRestPosition);
                    }
                });
            }
        }

        /// <summary>
        /// Tüm 4 sticker tamamlandığında zafer kutlaması ve otomatik yeniden başlatma sekansı.
        /// </summary>
        private IEnumerator VictoryAndAutoRestartRoutine()
        {
            isRestarting = true;
            yield return new WaitForSeconds(0.15f);

            // 1. Zafer Müziği / Fanfar
            if (StickerAudioManager.Instance != null)
            {
                StickerAudioManager.Instance.PlayVictorySound();
            }

            // 2. Albüm Sayfası Kutlama Zıplaması (Page Celebration Punch)
            if (pageSheet != null)
            {
                pageSheet.DOKill();
                pageSheet.DOPunchScale(new Vector3(0.045f, 0.045f, 0.045f), 0.70f, 6, 0.5f);
            }

            // 3. 4 Yuva Üzerinde Kademeli Yıldız Yağmuru (Sparkle Cascade)
            if (StickerVFXManager.Instance != null && ghostSlots.Count > 0)
            {
                for (int i = 0; i < ghostSlots.Count; i++)
                {
                    if (ghostSlots[i] != null)
                    {
                        StickerVFXManager.Instance.PlayStampVFX(ghostSlots[i].TargetPosition);
                        yield return new WaitForSeconds(0.12f);
                    }
                }
            }

            // 4. Zafer Anının Keyfinin Çıkarılması (Bekleme Süresi)
            yield return new WaitForSeconds(autoRestartDelay);

            // 5. OTOMATİK TEKRAR BAŞLAT (Seamless Auto-Restart)
            RestartLevel();
        }

        /// <summary>
        /// Seviyeyi pürüzsüzce temizleyip ilk 3 sticker ile tekrar başlatır.
        /// </summary>
        public void RestartLevel()
        {
            StartCoroutine(RestartLevelRoutine());
        }

        private IEnumerator RestartLevelRoutine()
        {
            isRestarting = true;

            // 1. Yapıştırılmış tüm sticker'ları küçülterek tatlıca yok et
            List<StickerClickable> allPlaced = new List<StickerClickable>(initialStickers);
            if (fourthSticker != null) allPlaced.Add(fourthSticker);

            foreach (var st in allPlaced)
            {
                if (st != null && st.gameObject.activeInHierarchy)
                {
                    st.transform.DOScale(Vector3.zero, 0.32f).SetEase(Ease.InBack);
                }
            }

            yield return new WaitForSeconds(0.35f);

            // 2. Tüm GhostSlot'ları sıfırla (Hayalet çizgileri tekrar açılsın)
            foreach (var slot in ghostSlots)
            {
                if (slot != null)
                {
                    slot.ResetSlot();
                }
            }

            // 3. 4. Sticker'ı gizle
            if (fourthSticker != null)
            {
                fourthSticker.gameObject.SetActive(false);
                fourthSticker.ResetSticker();
            }

            // 4. İlk 3 sticker'ı orijinal başlangıç pozisyonlarına getir ve kademeli Pop-in ile aç
            for (int i = 0; i < initialStickers.Count; i++)
            {
                StickerClickable st = initialStickers[i];
                if (st != null)
                {
                    st.ResetSticker();
                    Transform t = st.transform;
                    Vector3 origScale = t.localScale;
                    t.localScale = Vector3.zero;

                    t.DOScale(origScale, 0.42f).SetEase(Ease.OutBack, 1.4f).SetDelay(i * 0.08f);

                    if (StickerVFXManager.Instance != null)
                    {
                        StickerVFXManager.Instance.PlayPeelVFX(t.position);
                    }
                }
            }

            // 5. Durumları ve Sesleri Sıfırla
            placedStickerCount = 0;
            isDrawingFourthSticker = false;
            isRestarting = false;

            if (StickerAudioManager.Instance != null)
            {
                StickerAudioManager.Instance.ResetVictoryState();
            }

            Debug.Log("[StickerLevelManager] Level successfully auto-restarted! Ready for new round.");
        }
    }
}
