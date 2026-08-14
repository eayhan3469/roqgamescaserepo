# ROQ Games Dev Case — Case 2: Block Hole Asset Hazırlığı

**Tarih:** 2026-08-14
**Kapsam:** samil-hole-block projesinden Block Hole sekansı (drag → matching hole'a bırak → kırılma/impact → düşüş) için gereken art assetlerinin `Assets/Case2_BlockHole/` altına taşınması. Case 1 spec'inin (2026-08-14-fit-the-shape-case-assets-design.md) tüm genel kararları burada da geçerlidir.

## Kaynak / Hedef

- **Kaynak:** `/Users/macbookpro/Desktop/Unity_Projects/samil-hole-block/Assets/_HoleBlock` (GitHub'dan klonlandı; Unity 6000.3.11f1 — hedefle birebir aynı sürüm). Kaynak READ-ONLY.
- **Hedef:** `ROQ_Games_Dev_Case`, branch `case2-block-hole` (case1-fit-the-shape üzerinden — kullanıcı kararı: Case 1 main'e merge edilmedi).

## Case 1'den Devralınan Kararlar

Starter repo modeli; sadece görsel asset + basit in-house VFX (SFX yok); paid shader → URP Lit/Unlit dönüşümü (GUID koruyarak, renk bake); sadece resmi Unity paketleri; her case kendi klasöründe self-contained; dosya+meta kopyasıyla GUID koruma; Ivan Murzak MCP geçici kurulu kalır (tüm işler bitince kaldırılacak); commit/doğrulama zinciri aynı.

## Taşınacaklar

- **Bloklar:** `Prefabs/ArtBlocks/` 10 blok prefabı (Block-Single, Block-2, Block-3, Block-L, Block-T, Block-Square, Block-Cross, Block-L-R, Block-R-R + BombMMF HARİÇ — bomb özel mekanik) + modüler blok FBX'leri (`Models/`: Bevelled, Bevelled2, Bevelled3, Edge, OneCorner, TwoCorner, FourCorner, TwoEdge, bevellikup) + `Art/BLOCKS/BLOCKS.fbx`.
- **Delikler:** `Prefabs/Hole/` 6 prefab (Hole, Hole-Block-Single, -2, -L, -T, -Square) + `Art/HOLE/` (HOLE.fbx, HOLE.mat + 7 renk varyantı, HOLE-GRADIENT.jpg).
- **Board:** `Prefabs/Walls/` 9 duvar/çerçeve prefabı, grid floor materyalleri (MAT-GRID-FLOOR-*), bağımlılık yürüyüşünde çıkan wood/bg texture'ları.
- **Kırılma:** `Prefabs/FracturedBlock/` (Fracture Object 1..22, Block-Single - Fracture Root, MoreFractures/ alt klasörü) + prefab'ların referansladığı baked kırık mesh asset'leri. **DinoFracture/RayFire scriptleri sökülür; mesh, collider ve Rigidbody kalır** (mesh'ler tool-generated kendi içeriğimiz; scriptler paid).
- **Kesin liste** implementasyonda prefab'lardan GUID bağımlılık yürüyüşüyle çıkarılır (Case 1 yöntemi).

## Kapsam Dışı

Ice/Lock/Arrow/key özel blok elemanları, BombMMF, Curtain sistemi, LevelGen içerikleri, PowerUp/Timer/Reward sistemleri, ses (3D Match SFX paketi dahil — paid), UI/marketing sprite'ları (bg_blockhole.png, blockhole_splash.jpg dahil — kapsam dışı; yürüyüşte bir prefab bunlara bağımlı çıkarsa referans prefab temizliğinde sökülür, sprite kopyalanmaz).

## Shader / Script Temizliği

- Blok materyalleri **TCP2 Hybrid Shader 2** (guid `edd7abf643fa4bc4e8561d4c280c97cf`, paid) kullanıyor → URP Lit'e çevrilir (Case 1 converter'ı uyarlanır; TCP2 Hybrid negatif fileID'li shadergraph-tarzı referans kullanır — regex buna hazır). HOLE/duvar/floor materyallerinin shader'ları yürüyüşte tespit edilir; paid olanlar aynı politikayla dönüştürülür, in-house olanlar shader'ıyla birlikte kopyalanır.
- Sökülecek scriptler: şirket scriptleri (BlockVisual vb. — yürüyüşte tam liste), `RayfireShatter` (guid `7b55d9bb3ec909340848c72a5bfc0ad0`), DinoFracture üçlüsü: `FracturedObject` (`40b3b4341d686c248bc5aecab579e715`), `CleanupMeshOnDestroy` (`74a66a1d9fc746d49abdfc531db526f8`), `PreFracturedGeometry` (`e5711ac80149b2e449032315bef115b0`). Nested-prefab `m_AddedComponents` temizliği dahil (Case 1 Task 4 dersi).

## Staged Sahne

`Case2_BlockHole/Scenes/BlockHole.unity`: kamera + ışıklar kaynak `Scenes/Game/Gameplay.unity`'den; board kurulu (grid floor + duvar çerçevesi + 2-3 farklı renk/şekilde hole); kenarda 2-3 blok drag'e hazır dizili; script yok. Screenshot ile referans karşılaştırması (Case 1 yöntemi; screenshot-stale gotcha'sı biliniyor).

## VFX

`Case2_BlockHole/VFX/` altında kırılma temalı 3 taze partikül prefabı: DustPuff (yumuşak toz), DebrisBurst (köşeli parçacık saçılımı), ImpactRing (çarpma halkası). Case 1 VFX'leri kopyalanMAZ (aynı projede GUID çakışması + self-contained şartı) — aynı batch C# yöntemiyle sıfırdan, in-house texture'larla.

## Doğrulama

Case 1 zinciri: import sonrası konsol temiz; pembe materyal / missing mesh / missing script taraması CLEAN (bilinen whitelist pattern'leri hariç); paid GUID grep'i (TCP2 Hybrid + DinoFracture + RayFire + Layer Lab + Epic Toon GUID'leri) sıfır eşleşme; staged sahne screenshot onayı; README'ye Case 2 durumu işlenir.
