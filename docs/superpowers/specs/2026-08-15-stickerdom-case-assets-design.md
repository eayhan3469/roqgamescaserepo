# ROQ Games Dev Case — Case 3: Stickerdom Asset Hazırlığı

**Tarih:** 2026-08-15
**Kapsam:** stickerpages projesinden ve kullanıcının sticker arşivinden Case 3 (Stickerdom: tap → peel → move → deck'e yapışma) art assetlerinin `Assets/Case3_Stickerdom/` altına hazırlanması. Case 1-2 spec'lerinin genel kararları ve memory'deki pipeline dersleri (case-pipeline-lessons) aynen geçerli.

## Kaynaklar / Hedef

- **Kaynak 1 (READ-ONLY):** `/Users/macbookpro/Desktop/Unity_Projects/stickerpages/Assets/_StickerPages` (Unity 6000.4.10f1).
- **Kaynak 2 (READ-ONLY):** `/Users/macbookpro/Desktop/StickerPages/Stickers_Split/` — kullanıcının temalı sticker PNG arşivi (RGBA).
- **Hedef:** `ROQ_Games_Dev_Case`, branch `case3-stickerdom` (case2-block-hole üzerinden — stack stratejisi).

## Taşınacaklar / Üretilecekler

- **Prefab'lar:** `Prefabs/Sticker/` beşlisi: `CardModel`, `SheetModel`, `DrawPileModel`, `WastePileModel`, `PegModel` (+ meta, GUID korunarak). 5 şirket Model scripti sökülür. Feel bundle'ındaki LiberationSans SDF referansı standart TMP Essentials'a remap edilir: guid → `8f586378b4e144a9851e7b34d9b748ee`; font asset fileID 11400000 kalır, materyal/renderer slotlarındaki sub-asset fileID'leri → `2180264` (Case 1 Task 4 dersi).
- **Sticker sprite'ları (YENİ dosyalar, yeni GUID):** Stickers_Split'ten 8 farklı temadan 8 adet `01.png`: Hayvanlar_V1, Meyve_V1, Arac_V1, Enstrunman_V1, Kumsal_V1, Spor_V1, Teknoloji_V1, Doga_V1. Hedef isimlendirme: `sticker_<tema>.png` (küçük harf, ASCII). Her biri için `sticker_<tema>_ghost.png` programatik üretilir: alfa kanalı korunur, RGB düz açık-gri (örn. 0.78 gri), alfa ×0.55 — oyundaki ghost pattern'inin karşılığı. Sprite .meta'ları kaynak projedeki mevcut bir sticker sprite meta'sından şablonlanır (TextureImporter sprite ayarları aynı, GUID'ler yeni üretilir; iki case'te aynı GUID kullanılmaz).
- **Sayfa/board art'ı:** `Sprites/StickerPages/` ve `Sprites/StickersMisc/`ten prefab/sahne bağımlılık yürüyüşünde çıkan subset (albüm sayfası, slot çerçeveleri vb.). Twemoji sticker seti ve TWEMOJI-LICENSE.md **alınmaz**.
- **Kamera/ışık:** Gameplay.unity'den — ortografik, size 13.845; pos/rot/ışıklar implementasyonda birebir çekilir.

## Kapsam Dışı

Twemoji sprite'ları, UI panelleri (Prefabs/UI), level/LevelGen içerikleri, ses, `_MatchKnit`/`_Efsun` içerikleri, queue/puzzle mekaniği.

## Dönüşüm / Temizlik

Pipeline Case 2 v2 scriptlerinin aynısı, memory'deki derslerle: closure `.cs/.asmdef/.dll` hariç + "diğer üçüncü-parti" kovası raporlanır; materyal çıkarsa path-tabanlı convert (bu projede JMO/TCP olasılığı düşük ama sweep yine koşar); strip v2 (`_StickerPages/Scripts` + standart paid prefix'ler; `orig` snapshot düzeltmesiyle); ortak-şablon GUID çakışma kontrolü (Case 1-2'de var olan GUID'ler kopyalanmaz — özellikle Feel/TMP türevleri); doğrulama scanı ParticleSystemRenderer.m_Mesh dahil.

## Staged Sahne

`Case3_Stickerdom/Scenes/Stickerdom.unity`: ortho kamera (size 13.845) + kaynak ışık/ambient değerleri; albüm sayfası görünümü üzerinde 3-4 ghost slot (yeni ghost sprite'larıyla), kenarda/altta sökülmeye hazır 2-3 sticker (SpriteRenderer'lı basit objeler veya CardModel instance'ları — hangisi kaynak görünüme daha sadıksa), deck/draw-pile alanı. Script yok. Screenshot iterasyonu ≤3.

## VFX

`Case3_Stickerdom/VFX/`: SparklePop, PeelDust, AttachBurst — 3 taze partikül, Case 2'deki batch yöntem, in-house/yeni sprite'larla (PFX materyalleri bu case için yeni GUID'lerle üretilir; Case 2'ninkiler kopyalanmaz).

## Doğrulama

Aynı zincir: import sonrası konsol 0 error; pembe/missing scan (bilinen whitelist pattern'leri kanıtla); path-tabanlı paid GUID sweep + metin grep (Decoding/LodInfo false-positive bilgisiyle); README'ye Case 3 satırı; commit'ler path-scoped (`Assets/Case3_Stickerdom` + README) — Case1'deki kullanıcı el düzenlemelerine ve Case2'ye dokunulmaz.
