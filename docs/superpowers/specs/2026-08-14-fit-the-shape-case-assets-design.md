# ROQ Games Dev Case — Starter Repo & Case 1: Fit The Shape Asset Hazırlığı

**Tarih:** 2026-08-14
**Kapsam:** Adaylara verilecek starter Unity reposunun iskeleti + Case 1 (Fit The Shape) assetlerinin shape-slot projesinden taşınması. Case 2–4 (Block Hole, Stickerdom, Buca) sonraki oturumlarda aynı yapıya eklenecek.

## Amaç

ROQ Games'in Game Developer Case PDF'i 4 kısa gameplay interaction'ının yeniden üretilmesini istiyor. Adaylar feel/juice'a odaklansın diye art hammaddesi (mesh, materyal, texture, prefab, basit VFX) hazır verilecek. Bu repo adayların clone/fork edip içinde çalışacağı starter template'tir.

**Kaynak:** `/Users/macbookpro/Desktop/Unity_Projects/shape-slot` (ROQ'un kendi Fit The Shape oyunu, Unity 6000.4.10f1)
**Hedef:** `/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case` (Unity 6000.3.11f1, URP 17.3)

## Kararlar (kullanıcı onaylı)

| Karar | Seçim |
|---|---|
| Teslim şekli | Starter repo — adaylar clone/fork eder |
| Asset kapsamı | Sadece görsel assetler + basit in-house VFX; **SFX yok** |
| Toony Colors Pro materyalleri | URP Lit'e çevrilir |
| Paketler | Sadece resmi Unity paketleri; DOTween vb. adaya bırakılır (README notu) |

## Repo Yapısı

```
Assets/
  Case1_FitTheShape/
    Models/  Materials/  Textures/  Prefabs/  VFX/  Scenes/
  Case2_BlockHole/      (sonra)
  Case3_Stickerdom/     (sonra)
  Case4_Buca/           (sonra)
```

- PDF şartı gereği her case tamamen kendi klasöründe self-contained; **shared klasör yok** (texture duplikasyonu kabul edilir).
- Unity template çöpü temizlenir: `Assets/TutorialInfo/`, `Assets/Readme.asset`.

## Paket Manifest'i

- **Eklenir:** `com.unity.cinemachine` (camera feedback/shake için resmi paket).
- **Çıkarılır:** `com.unity.multiplayer.center`, `com.unity.collab-proxy`, `com.unity.visualscripting`, `com.unity.ai.navigation`.
- **Kalır:** URP, Input System, uGUI/TMP, Timeline, test-framework, IDE paketleri.
- Asset Store paketi (DOTween, Feel, TCP…) repoya **girmez** — lisans; README'de "tweening paketinizi kendiniz ekleyin" notu.

## Case 1: Fit The Shape — Taşınacak Assetler

Kaynak: `shape-slot/Assets/_ShapeSlot/`. Kopyalama **dosya + .meta birlikte** yapılır → GUID'ler korunur → prefab→materyal→mesh referansları kendiliğinden sağlam kalır.

**Modeller (`Models/`):** `SM_Shapes.fbx`, `SM_Shapes-Hole.fbx`, `SM_Shapes_Shadows.fbx`, `NEW-SLOT-MACHINE.fbx`, `SLOT-MIDDLE-PART.fbx`, `SLOT-TUTUCU.fbx`, `SM-SPIN-OBJECTS.fbx`, `SLOT-SPIN-BTN.fbx`, `ROUND-SHADOW.fbx`, `SHAPE-SLOT-SHADOW.fbx`.

**Prefab'lar (`Prefabs/`):** 6 şekil (`Round`, `Square`, `Triangle`, `Diamond`, `Star`, `Hexagon`), Target seti (`TargetSlotSegment`, `DrumSideMount`, `SlotMiddlePart`, `FrontFrame`, `SpinButton`), `DeckSlot`, shadow prefab'ları (`DeckSlotShadow`, `DrumShadow`).

**Materyaller (`Materials/`):** `SHAPESCOLOR/` 11 renk + prefab'ların bağımlılık taramasında çıkan wheel/deck materyalleri (ör. `FrontFrameWhite`, Metal, SHADOW).

**Texture'lar (`Textures/`):** Bağımlılık taramasında çıkan subset (SLOT albedo/AO, shadow texture'ları, BTN vb.).

Kesin liste implementasyonda prefab'lardan **bağımlılık yürüyüşüyle (GUID trace)** çıkarılır — yukarıdaki liste bilinen çekirdek; eksik bağımlılık pembe materyal/missing mesh taramasıyla yakalanır.

## Toony Colors Pro → URP Lit Dönüşümü

- Kopyalanan `.mat` dosyalarında `m_Shader` referansı TCP generated shader'lardan (`UserSHAPES`, `UserSHAPES_WithoutLine`, `UserSHADOW`) **URP Lit**'e yeniden yazılır (URP Lit shader GUID'i case projesinin PackageCache'inden doğrulanır).
- TCP'nin ana renk property'si URP Lit `_BaseColor`'a taşınır; smoothness düşük tutulur (flat görünüm).
- Materyal GUID'leri değişmediği için prefab'lara dokunulmaz. Repoya tek satır TCP içeriği girmez.
- Shader dönüşümü sonrası görsel fark (toon ramp/outline kaybı) kabul edilmiş trade-off'tur.

## Prefab Temizliği

- `TargetSlotSegment` ve `DeckSlot` üzerindeki 1'er MonoBehaviour (şirket kodu, Model binding scriptleri) prefab YAML'ından sökülür; kopyalanan diğer prefab'larda çıkan script'ler de aynı şekilde temizlenir. (`QueueShapeModel` queue/rope sistemine bağlı olduğundan kopyalanmaz.)
- Kalan: Transform / MeshFilter / MeshRenderer / Collider. Adaylar kendi kodunu yazar.
- Sökme sonrası prefab'larda missing-script kalmadığı doğrulanır.

## Staged Sahne

`Case1_FitTheShape/Scenes/FitTheShape.unity`:

- Kamera açısı/FOV/ışık shape-slot `Scenes/Game/Gameplay.unity`'den okunarak birebir alınır.
- Wheel (drum + segment + hole + frame + spin button) kurulu; deck'te 2–3 şekil dizili — referans videodaki kadraj hazır.
- **Script yok** — sadece sahnelenmiş art. Aday açar, direkt interaction kodlamaya başlar.
- PDF'teki "projeyi çalıştırmak için gerekli dosyalar" şartını bu sahne karşılar.

## VFX (in-house, basit)

`Case1_FitTheShape/VFX/` altında 2–3 Unity Particle System prefabı: impact burst, ring/shockwave, mini-confetti. Texture olarak shape-slot'un kendi in-house texture'ları (`Triangle-Soft.png`, `HALFTONE.png`) kullanılır. Epic Toon FX / CFXR gibi paid paketlerden **hiçbir şey** kopyalanmaz.

## Git & README

- `git init` + standart Unity `.gitignore` (yapıldı). **LFS yok** — dosyalar küçük, adaya clone sürtünmesi çıkarmayalım.
- Root `README.md`: Unity sürümü (6000.3.11f1), klasör yapısı, case dokümanına referans, "tweening/animasyon paketinizi kendiniz ekleyin" notu.

## Doğrulama

1. Ivan Murzak Unity MCP ile case projede AssetDatabase refresh + console hata taraması.
2. Pembe materyal / missing mesh / missing script taraması (sahne + prefab'lar).
3. Staged sahnenin Game View screenshot'ı ile kompozisyon kontrolü.

## Yayın Öncesi Checklist (candidate-facing temizlik)

- [ ] `docs/superpowers/` (internal spec/plan) adaylara gidecek branch'ten çıkarılır.
- [ ] Template çöpü (`TutorialInfo/`, `Readme.asset`) silinmiş mi kontrol edilir.
- [ ] Repoda paid asset izi (TCP, Epic Toon FX, Feel…) grep ile taranır.

## Kapsam Dışı (YAGNI)

- Gameplay/feel kodu — adayın işi.
- SFX — verilmeyecek (kullanıcı kararı).
- Case 2–4 asset göçü — sonraki oturumlar; bu spec sadece yapıyı hazırlar.
- Addressables, Zenject, Efsun pattern'leri — starter repo boş ve nötr kalır.
