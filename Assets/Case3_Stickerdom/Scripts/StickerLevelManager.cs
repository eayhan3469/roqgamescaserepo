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

        [Header("Pack Tear-Open (2-Piece Purple Package)")]
        [Tooltip("Top flap piece of the 2-piece purple package — uses the tightly-cropped PurplePackage_Top_Peel sprite (just the visible flap, seam-to-tip) positioned so its own transform sits at the flap's vertical center. Auto-found by name (\"PackTop\") under drawPile if left unassigned. Gets a real StickerPeel3DMesh 3D curl (the same corner-curl effect stickers use) driving the tear, instead of a rigid translate/rotate.")]
        [SerializeField] private Transform packTop;
        [Tooltip("Bottom body piece of the 2-piece purple package (PurplePackage_Bottom sprite) — child of drawPile, auto-found by name (\"PackBottom\") if left unassigned. Stays in place (gets a small anticipation pop) while packTop tears away.")]
        [SerializeField] private Transform packBottom;
        [Tooltip("How long the top flap's tear-open curl animation takes. The 4th sticker's emerge sequence now waits for this to finish first, so the tree visibly comes out of the torn opening rather than overlapping the tear.")]
        [SerializeField] private float packTearDuration = 0.4f;
        [Tooltip("Curl direction in degrees (angle convention matches StickerPeel3DMesh.peelAngle: 0=curls from the right edge leftward, 90=curls from the top tip downward, 180=curls from the left edge rightward). Kept at exactly 180 (pure left-to-right) — packTearAngleJitter used to add variety here but that made the tear read as diagonal instead of a clean left-to-right, so it is fixed to 0.")]
        [SerializeField] private float packTearBaseAngle = 180f;
        [Tooltip("Random variation (degrees) added to packTearBaseAngle each tear. Kept at 0 so the tear is always exactly left-to-right; left here (rather than removed) in case a future design wants slight variety back.")]
        [SerializeField] private float packTearAngleJitter = 0f;
        [Tooltip("Once the torn-open pack has released the 4th sticker, the remaining pack body (packBottom) slowly fades away over this many seconds instead of staying visible forever.")]
        [SerializeField] private float packDissolveDuration = 0.9f;
        [Tooltip("Delay after the 4th sticker lands in the waiting row before the empty pack body starts dissolving away.")]
        [SerializeField] private float packDissolveDelay = 0.3f;

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
            ResetPackPieces();
            RestrictEdgeStickerPeelCorners();
        }

        /// <summary>
        /// Corner angles a peel can pick from without its curl bulging past the sticker's own
        /// bounding box in a given horizontal direction — see StickerPeel3DMesh.DeformMesh: the
        /// curled part of a peel moves in the OPPOSITE direction of where the peel started (e.g.
        /// peelAngle 45/315 both start from a corner on the RIGHT side, so the curl bulges LEFT).
        /// </summary>
        private static readonly float[] CornersBulgingRight = { 135f, 225f }; // start from a LEFT corner, bulge right/inward
        private static readonly float[] CornersBulgingLeft = { 45f, 315f };  // start from a RIGHT corner, bulge left/inward

        /// <summary>
        /// Finds whichever sticker sits furthest left and furthest right across all 4 waiting-row
        /// slots (using each initial sticker's authored position, and the 4th sticker's configured
        /// fourthStickerRestPosition since it isn't positioned there yet at Start), and restricts
        /// each to only the corners whose curl bulges inward — so a randomized peel never rolls
        /// the curled paper out past the screen edge. Middle stickers keep all 4 corners.
        /// </summary>
        private void RestrictEdgeStickerPeelCorners()
        {
            List<StickerClickable> candidates = new List<StickerClickable>(initialStickers);
            if (fourthSticker != null) candidates.Add(fourthSticker);
            candidates.RemoveAll(st => st == null);
            if (candidates.Count < 2) return;

            StickerClickable leftmost = null, rightmost = null;
            float minX = float.MaxValue, maxX = float.MinValue;

            foreach (var st in candidates)
            {
                float x = (st == fourthSticker) ? fourthStickerRestPosition.x : st.transform.position.x;
                if (x < minX) { minX = x; leftmost = st; }
                if (x > maxX) { maxX = x; rightmost = st; }
            }

            if (leftmost != null) leftmost.SetAllowedPeelCornerAngles(CornersBulgingRight);
            if (rightmost != null && rightmost != leftmost) rightmost.SetAllowedPeelCornerAngles(CornersBulgingLeft);
        }

        /// <summary>
        /// Puts PackTop/PackBottom (the 2-piece purple package) back to their rest state —
        /// PackTop at its original local transform and full alpha, both pieces at scale 1.
        /// Called once on Start (defensive, in case the scene wasn't authored in the exact
        /// rest state) and again in RestartLevelRoutine so the tear-open animation can play
        /// correctly every subsequent round, not just the first. Hardcodes identity/one rather
        /// than caching whatever the scene happened to have at Awake, since the known-correct
        /// rest state (matching the original single-piece Pack sprite's transform) is simple
        /// and unambiguous — no reason to trust arbitrary authored values instead.
        /// </summary>
        private void ResetPackPieces()
        {
            if (packTop != null)
            {
                // DOKill(true) completes/clears ANY tween targeting packTop, INCLUDING the
                // tear+fade Sequence started in DrawFourthStickerFromPileRoutine (that Sequence
                // is explicitly SetTarget(packTop) for exactly this reason) — without this, a
                // still-running tear/fade from a round that got restarted early keeps writing
                // peel progress and alpha on top of this reset, and the pack looked like it
                // "didn't reset" (old bug: the tear Sequence had no target, so this call used to
                // silently kill nothing).
                packTop.DOKill();

                StickerPeel3DMesh peelMesh = packTop.GetComponent<StickerPeel3DMesh>();
                if (peelMesh != null)
                {
                    peelMesh.ResetPeel();
                    peelMesh.SetAlpha(1f);
                    // ResetPeel() does not reset shadow opacity (it is independent of peel
                    // progress) — without this, the curl's separate ground-contact shadow mesh
                    // would stay faded from the previous round's dissolve.
                    peelMesh.SetShadowOpacity(1f);
                }

                SpriteRenderer topSr = packTop.GetComponent<SpriteRenderer>();
                if (topSr != null)
                {
                    Color c = topSr.color;
                    topSr.color = new Color(c.r, c.g, c.b, 1f);
                }
            }

            if (packBottom != null)
            {
                packBottom.DOKill();
                packBottom.localScale = Vector3.one;

                SpriteRenderer bottomSr = packBottom.GetComponent<SpriteRenderer>();
                if (bottomSr != null)
                {
                    // DOFade tweens the SpriteRenderer directly (a different DOTween target than
                    // the Transform above), so it needs its own DOKill to stop the pack's
                    // post-emerge dissolve-away fade if a restart interrupts it mid-fade.
                    bottomSr.DOKill();
                    Color c = bottomSr.color;
                    bottomSr.color = new Color(c.r, c.g, c.b, 1f);
                }
            }
        }

        public void AutoFindReferences()
        {
            if (drawPile == null)
            {
                GameObject dpObj = GameObject.Find("DrawPile");
                if (dpObj != null) drawPile = dpObj.transform;
            }

            if (drawPile != null)
            {
                if (packTop == null) packTop = drawPile.Find("PackTop");
                if (packBottom == null) packBottom = drawPile.Find("PackBottom");
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

            // 1. Paket üst kapağı yırtılarak açılıyor (2 parçalı paket: PackTop + PackBottom).
            // Ağaç (4. sticker) bu yırtılma bitene kadar ÇIKMIYOR — kullanıcı isteği, eskiden
            // tüm paket tek parça olarak "pop" edip ağaç aynı anda fırlıyordu; artık önce üst
            // parça yırtılıp uçuyor, alt gövde yerinde kalıp küçük bir sıkışma tepkisi veriyor,
            // ağaç ancak ondan sonra deliğinden çıkıyor.
            float tearDuration = 0f;

            if (packBottom != null)
            {
                packBottom.DOKill();
                // Alt gövde yerinde kalıyor, sadece küçük bir "sıkışma" tepkisi (eski tek parça
                // paketin punch-scale'inin daha küçük/hafif hali) — büyük hareket üst parçada.
                packBottom.DOPunchScale(new Vector3(0.12f, -0.10f, 0.12f), packTearDuration * 0.75f, 5, 0.5f);
            }
            else if (drawPile != null)
            {
                // Geriye dönük uyum: 2 parçalı paket bulunamazsa (örn. eski tek parçalı sahne),
                // eski tek-parça punch-scale davranışına düş.
                drawPile.DOKill();
                drawPile.DOPunchScale(new Vector3(0.22f, -0.18f, 0.22f), 0.45f, 6, 0.5f);
            }

            if (packTop != null)
            {
                packTop.DOKill();
                tearDuration = packTearDuration;

                // Real sticker-style 3D curl (the exact same StickerPeel3DMesh corner-curl the
                // stickers themselves use), not a rigid translate/rotate — the flap bends and
                // rolls up like paper actually tearing, instead of just falling/flying away.
                // packTop's sprite (PurplePackage_Top_Peel) is pre-cropped to just the visible
                // flap with its own transform centered on it, so peelAngle=180 curls from the
                // left edge (peels first) rightward — tears open left-to-right, per user request.
                StickerPeel3DMesh peelMesh = packTop.GetComponent<StickerPeel3DMesh>();
                if (peelMesh == null) peelMesh = packTop.gameObject.AddComponent<StickerPeel3DMesh>();

                float angleJitter = UnityEngine.Random.Range(-packTearAngleJitter, packTearAngleJitter);
                peelMesh.SetPeelAngle(packTearBaseAngle + angleJitter);
                peelMesh.SetAlpha(1f);
                peelMesh.ResetPeel();

                float fadeAlpha = 1f;
                Sequence tearSeq = DOTween.Sequence();
                // Target this Sequence to packTop so ResetPackPieces's packTop.DOKill() can
                // actually find and stop it if a restart happens mid-tear — see the comment on
                // ResetPackPieces for why this matters.
                tearSeq.SetTarget(packTop);
                tearSeq.Append(peelMesh.AnimatePeelOff(tearDuration));
                // Fade only kicks in for the back half of the curl, once the flap already reads
                // clearly as "peeling open" — it dissolves away rather than popping out instantly.
                // Also fades the curl's separate ground-contact shadow mesh (its own renderer
                // and material — SetAlpha alone never touched it) — without this, a dark shadow
                // patch stayed sitting where the flap used to be even after the flap itself had
                // fully faded, reading as "the top never actually disappeared".
                tearSeq.Insert(tearDuration * 0.5f, DOTween.To(() => fadeAlpha, x => { fadeAlpha = x; peelMesh.SetAlpha(x); peelMesh.SetShadowOpacity(x); }, 0f, tearDuration * 0.5f).SetEase(Ease.InQuad));
            }

            // 2. Ses ve Parçacık Efekti (Yırtılma anı - Draw Swoosh & Puff)
            if (StickerAudioManager.Instance != null)
            {
                StickerAudioManager.Instance.PlayFlySound();
            }

            if (StickerVFXManager.Instance != null)
            {
                StickerVFXManager.Instance.PlayPeelVFX(spawnPos);
            }

            // 3. Ağaç, üst kapak tamamen yırtılıp uçana kadar bekliyor.
            if (tearDuration > 0f)
            {
                yield return new WaitForSeconds(tearDuration);
            }

            // 4. 4. Sticker'ı Mor Objenin Merkezinde Başlat
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

                    // Ağaç (4. sticker) paketten tamamen çıktı — artık boşalan paket gövdesinin
                    // (packBottom) kullanıcı isteğiyle yavaşça sönerek kaybolma vaktı geldi.
                    if (packBottom != null)
                    {
                        SpriteRenderer bottomSr = packBottom.GetComponent<SpriteRenderer>();
                        if (bottomSr != null)
                        {
                            bottomSr.DOKill();
                            bottomSr.DOFade(0f, packDissolveDuration).SetDelay(packDissolveDelay).SetEase(Ease.InQuad);
                        }
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

            // 3.5. Yırtılan paket parçalarını (PackTop/PackBottom) başlangıç haline getir, bir
            // sonraki turda yırtılma animasyonu tekrar oynayabilsin.
            ResetPackPieces();

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
