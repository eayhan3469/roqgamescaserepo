# ROQ Games Dev Case — Case 4: Buca Asset Hazırlığı (Sıfırdan Üretim)

**Tarih:** 2026-08-15
**Kapsam:** Case 4 (Buca: yeşil blokların kırılıp deliğe gönderildiği sekans) için TÜM assetlerin sıfırdan üretilmesi. Kaynak Unity projesi YOK — referans, Buca APK'sının AssetRipper dökümünden yapılan görsel stil analizi (scratchpad'deki `buca_rip/`, atlas sayfaları `Assets/Texture2D/sactx-*-spriteatlas_game-*.png`). **Rip'ten tek bir dosya bile repoya girmez** — yalnızca bakılarak özgün asset üretilir; iş bitince rip silinir.

## Referans Görsel Dil (rip analizinden)

Portre kadraj; dar FOV (~30-40) perspektif kamera puck'ın arkasından-üstünden öne bakar; öne uzanan yuvarlatılmış köşeli **lane**; zemin doygun **neon gradyan** (mor→mavi ana varyant); **beyaz yumuşak yuvarlatılmış raylar**; kırmızı-beyaz çizgili dekor post'ları; delik lane ilerisinde **zemine gömülü siyah daire + beyaz rim**; fırlatılan şey **beyaz silindir puck** (top değil); yeşil öğeler **parlayan neon yeşil küp/bar'lar**; renkli iz/yıldız FX'leri; 3 kalp can göstergesi.

## Hedef

`ROQ_Games_Dev_Case`, branch `case4-buca` (case3-stickerdom üzerinden — stack). Her şey `Assets/Case4_Buca/` altında self-contained, tamamı yeni GUID'li özgün üretim.

## Üretilecek Assetler

- **Texture'lar (PIL):** lane gradyan zemini (mor→mavi, 512px), kırmızı-beyaz çizgili post şeridi, kalp sprite'ı, yıldız + soft-circle FX dokuları (Case 3 üretim pipeline'ı: PIL + şablondan .meta, yeni uuid4 GUID'ler; .meta şablonu Case 3'ün sprite meta'sından alınır).
- **Materyaller (el YAML, sabit GUID'ler):** LaneFloor (Unlit + gradyan texture), RailWhite (Lit, beyaz, hafif smoothness), PostStripe (Unlit + şerit texture), HoleBlack (Unlit siyah), RimWhite (Lit beyaz), PuckWhite (Lit), PuckAccent (Lit turuncu), **GreenNeon (Lit + emissive yeşil — glow hissi)**, GreenNeonDark (parça varyantı), PFX mat'ları (Particles/Unlit + FX dokuları).
- **Mesh/prefab'lar (Unity primitive kompozisyonu, MCP ile):**
  - `Lane.prefab`: zemin quad'ı + 2 beyaz yan rail (yatık silindir/kapsül görünümü: cube+cylinder kompozisyon) + 2 çizgili post.
  - `Hole.prefab`: siyah flush disk + beyaz torus-benzeri rim (silindir halka; torus yoksa yassı silindir ring).
  - `Puck.prefab`: beyaz silindir (r≈0.5, h≈0.25) + `Puck_Orange` varyantı.
  - `GreenBlock.prefab` (1x1 küp) ve `GreenBar.prefab` (3x1) — emissive neon yeşil.
  - `GreenBlock_Fractured.prefab` / `GreenBar_Fractured.prefab`: 8 / 12 mini-küp parça, her parçada Rigidbody + BoxCollider (kırılma/deliğe gidiş hammaddesi — aday fiziği kodlar).
  - `Hearts.prefab`: 3 kalp sprite'lı can göstergesi (world-space, dekor).
- **Staged sahne** `Case4_Buca/Scenes/Buca.unity`: referans kadraj — perspektif kamera (FOV ~35, arkadan-üstten, portre), lane öne uzanır, ortada 1 GreenBar + 2 GreenBlock dizilimi (biri yanında kırık halinin sergilendiği parça yığını), ileride Hole, altta Puck + Hearts. Screenshot iterasyonu ≤3 (atlas önizlemeleriyle karşılaştırılır). Script yok.
- **VFX:** GreenShatter (neon yeşil küp parçacıklar), HoleRing (beyaz genişleyen halka), StarTrail (yıldızlı iz — trail-tarzı uzun ömürlü partikül).

## Kapsam Dışı

Puck skin'leri (donut/unicorn vb.), powerup'lar, pembe tehlike duvarı mekaniği (dekor post yeter), level sistemi, UI, ses. Rip'ten asset kopyası KESİNLİKLE yok (IP: Neon Play).

## Pipeline / Doğrulama

Case 1-3 zinciri + memory dersleri: `rm -rf` yasak, path-scoped commit (`Assets/Case4_Buca` + README), GUID benzersizlik assert'i, MCP full-code mode, screenshot-stale çift capture, ParticleSystemRenderer.m_Mesh taraması (mesh modda built-in Cube tek submesh kuralı), import sonrası konsol 0 error, README Case 4 satırı. Paid-GUID denetimi bu case'te kaynak index'siz — denetim, Case4 klasöründeki tüm guid referanslarının repo-içi çözülmesi (referential integrity) + metin grep olarak koşar. Yayın öncesi: scratchpad'deki `buca_rip/` ve `.xapk` silinir (zaten repo dışında).
