# Fit The Shape Case Assets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** shape-slot oyunundan Fit The Shape sekansı için gereken art assetlerini ROQ_Games_Dev_Case starter reposuna taşı — GUID koruyarak, paid asset içermeden, staged sahne ve basit VFX ile.

**Architecture:** Dosya+meta kopyası (GUID korunur → referanslar sağlam kalır) → .mat dosyalarında TCP shader'ları URP Lit/Unlit'e YAML rewrite → prefab'lardan şirket scriptleri YAML strip → Unity içi işler (TMP, sahne, VFX) Ivan Murzak MCP ile.

**Tech Stack:** Unity 6000.3.11f1, URP 17.3, Python 3 (YAML rewrite), Ivan Murzak Unity MCP (`assets-*`, `script-execute`, `screenshot-game-view`).

## Global Constraints

- Hedef proje: `/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case` (Unity 6000.3.11f1). Kaynak: `/Users/macbookpro/Desktop/Unity_Projects/shape-slot`.
- **Paid asset yasak:** Toony Colors Pro / JMO, Layer Lab, Epic Toon FX, CFXR, Feel, DOTween, Odin — hiçbir dosyası repoya giremez.
- Tüm kopyalar `.meta` dosyasıyla birlikte yapılır (GUID korunur). Rename'de `.mat` + `.mat.meta` çifti birlikte rename edilir.
- Her case kendi klasöründe self-contained: her şey `Assets/Case1_FitTheShape/` altına.
- Sadece resmi Unity paketleri. Git LFS kullanılmaz.
- Unity işlemleri SADECE Ivan Murzak MCP ile (`unity-mcp` araçları); Unity'nin kendi MCP'si kullanılmaz.
- Commit mesajları sonu: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

## Sabitler (keşifte doğrulandı)

| Ne | Değer |
|---|---|
| URP Lit shader GUID | `933532a4fcc9baf4fa0491de14d08ed7` |
| URP Unlit shader GUID | `650dd9526735d5b46b79224bc6e94025` |
| TCP UserSHAPES GUID (→ Lit) | `d5a641412fc0b774b9bf47cbff0ffece` |
| TCP UserSHAPES_WithoutLine GUID (→ Lit) | `9b5a91723e7a840c6b53c694953a1f6e` |
| TCP UserSHADOW GUID (→ Unlit transparent) | `1cd0ae6487ac2024eb03b8e314495b2d` |
| mysterynew.shadergraph GUID (→ Lit) | `9e7217c4dd508204e973a33bd00940ff` |
| Layer Lab TMP font GUID (SpinButton'da, değişecek) | `23ae78aa8db6e4eb9872a56fdb005f63` |
| LiberationSans SDF GUID (TMP Essentials) | `8f586378b4e144a9851e7b34d9b748ee` |
| Strip edilecek script GUID'leri | `fd64db82008a04d2ba8273517497e212` (DeckSlotModel), `fb9d59a47cfa5432dbe963196aa04acf` (TargetSlotSegmentModel), `4fff2ee4bbda4469bb394571fcf64333` (SpinButtonModel) |
| Kamera (Gameplay.unity) | pos (0, 97.51, -28.56), quat (0.57357645, 0, 0, 0.81915206), FOV 10, perspective |
| Işık 1 "SLOTINSIDELIGHT" | directional, intensity 1.34, quat (0.8493997, 0, 0, 0.5277502) |
| Işık 2 "Directional Light" | directional, intensity 0.33, quat (0.81640714, -0.28371835, -0.022132609, 0.50248724) |
| Drum root | pos (0, 2.22, 10.9), scale 0.95 |
| Segment yerleşimi | notchAngle 24° (15 segment/ring), radius 0.5, segmentScale (0.87, 1.47, 1.47), segmentBaseEuler (-90,0,0), frontAngle 66.3, columnGap 0, 5 kolon |
| SideMount | scale 0.9657, drum uçlarından x-offset 0.85 |
| MiddlePart | scale 0.91, euler (4.85, 180, 0) |
| FrontFrame | offset (0, 0, -0.4), height 1.37 |
| SpinButton | deck sonundan offset (1.86, -0.07, 0.05), euler (340.38, 180, 0) |
| DrumShadow | offset (0, -5.5, -1.5), euler (90, 180, 0), scale (19.59, 6.18, 10.43) |
| Deck | drum'dan offset (0, -4.28, -3.42), pedestal spacing 1.58, pedestalScale (1.22, 1.56, 1.22), pedestalEuler (19.34, 0, 0) |
| Deck'te şekil | pedestal'dan offset (0, 0.645, 0.11), scale (1.03, 1.93, 1.03) |
| DeckSlotShadow | offset (0, -0.38, 0), euler (90, 0, 0), scale (0.34, 0.25, 0.34) |

---

### Task 1: Paket manifest'i ve template temizliği

**Files:**
- Modify: `Packages/manifest.json`
- Delete: `Assets/TutorialInfo/` (+ `.meta`), `Assets/Readme.asset` (+ `.meta`)

**Interfaces:**
- Produces: temiz manifest (Cinemachine dahil), template çöpü olmayan Assets kökü. Sonraki task'lar bu duruma dayanır.

- [ ] **Step 1: manifest.json'ı düzenle**

`Packages/manifest.json` içinde `dependencies`'ten şu 4 satırı SİL:
```json
    "com.unity.ai.navigation": "2.0.11",
    "com.unity.collab-proxy": "2.11.4",
    "com.unity.multiplayer.center": "1.0.1",
    "com.unity.visualscripting": "1.9.10",
```
Ve alfabetik sıraya uyacak şekilde şunu EKLE:
```json
    "com.unity.cinemachine": "3.1.4",
```

- [ ] **Step 2: Template çöpünü sil**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
rm -rf "Assets/TutorialInfo" "Assets/TutorialInfo.meta" "Assets/Readme.asset" "Assets/Readme.asset.meta"
```

- [ ] **Step 3: JSON geçerliliğini doğrula**

Run: `python3 -c "import json; json.load(open('/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Packages/manifest.json')); print('OK')"`
Expected: `OK`

- [ ] **Step 4: Commit**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
git add -A
git commit -m "chore: trim package manifest, add Cinemachine, remove template junk

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Asset dosyalarını kopyala (dosya + .meta, GUID korunarak)

**Files:**
- Create: `Assets/Case1_FitTheShape/{Models,Materials/{Shapes,Colors,Holes,Slot},Shaders,Textures,Prefabs,VFX,Scenes}/` altında aşağıdaki tüm dosyalar

**Interfaces:**
- Produces: Task 3-4'ün rewrite edeceği `.mat`/`.prefab` dosyaları hedef klasörlerde. GUID'ler kaynaktakiyle birebir aynı.

- [ ] **Step 1: Kopyalama scriptini çalıştır**

```bash
SRC="/Users/macbookpro/Desktop/Unity_Projects/shape-slot/Assets"
DST="/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case1_FitTheShape"
mkdir -p "$DST/Models" "$DST/Materials/Shapes" "$DST/Materials/Colors" "$DST/Materials/Holes" "$DST/Materials/Slot" "$DST/Shaders" "$DST/Textures" "$DST/Prefabs" "$DST/VFX" "$DST/Scenes"
cpm() { cp "$1" "$3/$(basename "${2:-$1}")"; cp "$1.meta" "$3/$(basename "${2:-$1}").meta"; }
# --- Modeller (bağımlılık yürüyüşünde çıkan 8 FBX)
for f in SM_Shapes.fbx SM_Shapes-Hole.fbx SM_Shapes_Shadows.fbx SHAPE-SLOT-PART.fbx SLOT-MIDDLE-PART.fbx SLOT-SPIN-BTN.fbx SLOT-TUTUCU.fbx SM_SHAPE_SLOT.fbx; do cpm "$SRC/_ShapeSlot/Models/$f" "" "$DST/Models"; done
# --- Materyaller: SHAPESCOLOR 11 renk (RED base, digerleri variant)
for c in BLUE BROWN GREEN GREY OLIVE ORANGE PINK PURPLE RED TEAL YELLOW; do cpm "$SRC/_ShapeSlot/Models/MATERIALS/SHAPESCOLOR/$c.mat" "" "$DST/Materials/Shapes"; done
# --- COLORS 11 renk (YELLOW base) — ayni isimler, farkli klasor
for c in BLUE BROWN GREEN GREY OLIVE ORANGE PINK PURPLE RED TEAL YELLOW; do cpm "$SRC/_ShapeSlot/Models/MATERIALS/COLORS/$c.mat" "" "$DST/Materials/Colors"; done
# --- HOLES 10 renk
for c in BLUE BROWN GREEN OLIVE ORANGE PINK PURPLE RED TEAL YELLOW; do cpm "$SRC/_ShapeSlot/Models/MATERIALS/HOLES/HOLE-$c.mat" "" "$DST/Materials/Holes"; done
# --- Slot/cesitli materyaller
for f in METAL SHADOW SLOT-COLOR SPIN-BTN WHITE; do cpm "$SRC/_ShapeSlot/Models/MATERIALS/LVL01-10/$f.mat" "" "$DST/Materials/Slot"; done
cp "$SRC/_ShapeSlot/Models/MATERIALS/LVL01-10/New Material.mat" "$DST/Materials/Slot/SLOT-DETAIL.mat"
cp "$SRC/_ShapeSlot/Models/MATERIALS/LVL01-10/New Material.mat.meta" "$DST/Materials/Slot/SLOT-DETAIL.mat.meta"
cpm "$SRC/_ShapeSlot/Settings/FrontFrameWhite.mat" "" "$DST/Materials/Slot"
cpm "$SRC/_Efsun/MaskShader/Custom_MaskShader.mat" "" "$DST/Materials/Slot"
cpm "$SRC/_ShopControl/ART/Materials/CharacterColors/MysterySlot_New.mat" "" "$DST/Materials/Slot"
# --- In-house shader'lar
cpm "$SRC/_Efsun/MaskShader/MaskShader.shader" "" "$DST/Shaders"
cpm "$SRC/_ShapeSlot/Models/SHADERS/SpriteAlphaBoost.shader" "" "$DST/Shaders"
# --- Texture'lar
cpm "$SRC/_ShapeSlot/Models/TEXTURE/SHADOW.png" "" "$DST/Textures"
cpm "$SRC/_ShapeSlot/Models/TEXTURE/SHADOW-2.png" "" "$DST/Textures"
cpm "$SRC/_ShapeSlot/Models/TEXTURE/SHADOW-SLOT.png" "" "$DST/Textures"
cpm "$SRC/_ShapeSlot/Models/TEXTURE/SHAPE-SLOT-HOLE-SHADOW_AlbedoTransparency.jpg" "" "$DST/Textures"
cpm "$SRC/_ShapeSlot/Models/MATERIALS/questionmark pattern2.png" "" "$DST/Textures"
cpm "$SRC/maskemiss3_white_sharpened.png" "" "$DST/Textures"
cpm "$SRC/_ShapeSlot/Sprites/Union.png" "" "$DST/Textures"
cpm "$SRC/_ShapeSlot/Models/TEXTURE/Triangle-Soft.png" "" "$DST/Textures"
cpm "$SRC/_ShapeSlot/Models/TEXTURE/HALFTONE.png" "" "$DST/Textures"
# --- Prefab'lar (14)
for f in MODELS/Round MODELS/Square MODELS/Triangle MODELS/Diamond MODELS/Star MODELS/Hexagon Target/TargetSlotSegment Target/DrumSideMount Target/SlotMiddlePart Target/FrontFrame Target/SpinButton Deck/DeckSlot Shadows/DeckSlotShadow Shadows/DrumShadow; do cpm "$SRC/_ShapeSlot/Prefabs/$f.prefab" "" "$DST/Prefabs"; done
echo "DONE: $(find "$DST" -type f ! -name '*.meta' | wc -l) dosya"
```
Expected: `DONE: 74 dosya` (8 fbx + 41 mat [11 Shapes + 11 Colors + 10 Holes + 9 Slot] + 2 shader + 9 texture + 14 prefab).

- [ ] **Step 2: Her dosyanın .meta çiftinin varlığını doğrula**

```bash
DST="/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case1_FitTheShape"
find "$DST" -type f ! -name "*.meta" | while read f; do [ -f "$f.meta" ] || echo "META EKSIK: $f"; done; echo "meta-check-done"
```
Expected: sadece `meta-check-done` (hiç `META EKSIK` satırı yok).

- [ ] **Step 3: Commit**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
git add Assets/Case1_FitTheShape
git commit -m "feat: copy Fit The Shape art assets from shape-slot (GUID-preserving)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: TCP → URP materyal dönüşümü (YAML rewrite)

**Files:**
- Create: scratchpad'de `convert_mats.py` (repoya girmez)
- Modify: `Assets/Case1_FitTheShape/Materials/**/*.mat` (43 dosya, in-place)

**Interfaces:**
- Consumes: Task 2'nin kopyaladığı .mat dosyaları; kaynak projedeki parent chain (renk çözümü için).
- Produces: Tüm .mat'ler URP Lit (opak) veya URP Unlit (transparent) shader'lı, `m_Parent` sıfırlanmış, `_BaseColor` bake edilmiş. TCP GUID'i içeren dosya kalmaz.

- [ ] **Step 1: Dönüşüm scriptini yaz**

Scratchpad'e `convert_mats.py` olarak kaydet:

```python
#!/usr/bin/env python3
import re, glob, os

SRC = "/Users/macbookpro/Desktop/Unity_Projects/shape-slot/Assets"
DST = "/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case1_FitTheShape/Materials"
LIT, UNLIT = "933532a4fcc9baf4fa0491de14d08ed7", "650dd9526735d5b46b79224bc6e94025"
TO_LIT = {"d5a641412fc0b774b9bf47cbff0ffece", "9b5a91723e7a840c6b53c694953a1f6e", "9e7217c4dd508204e973a33bd00940ff"}
TO_UNLIT = {"1cd0ae6487ac2024eb03b8e314495b2d"}

# Kaynak projede guid -> mat dosyasi indeksi (parent chain cozumu icin)
guid2file = {}
for meta in glob.glob(SRC + "/**/*.mat.meta", recursive=True):
    m = re.search(r"^guid: ([0-9a-f]{32})", open(meta).read(), re.M)
    if m: guid2file[m.group(1)] = meta[:-5]

def get_color(text, prop):
    m = re.search(r"- " + prop + r": \{r: ([\d.e+-]+), g: ([\d.e+-]+), b: ([\d.e+-]+), a: ([\d.e+-]+)\}", text)
    return m.groups() if m else None

def resolve_base_color(path, depth=0):
    """_BaseColor -> _Color -> parent chain -> beyaz."""
    text = open(path).read()
    c = get_color(text, "_BaseColor") or get_color(text, "_Color")
    if c: return c
    p = re.search(r"m_Parent: \{fileID: \d+, guid: ([0-9a-f]{32})", text)
    if p and depth < 5 and p.group(1) in guid2file:
        return resolve_base_color(guid2file[p.group(1)], depth + 1)
    return ("1", "1", "1", "1")

for path in glob.glob(DST + "/**/*.mat", recursive=True):
    text = open(path).read()
    sh = re.search(r"m_Shader: \{fileID: \d+, guid: ([0-9a-f]{32})", text)
    if not sh or sh.group(1) not in (TO_LIT | TO_UNLIT):
        print(f"SKIP (in-house/zaten urp): {os.path.basename(path)}"); continue
    unlit = sh.group(1) in TO_UNLIT
    r, g, b, a = resolve_base_color(path)
    new_guid = UNLIT if unlit else LIT
    text = re.sub(r"m_Shader: \{fileID: \d+, guid: [0-9a-f]{32}, type: \d\}",
                  f"m_Shader: {{fileID: 4800000, guid: {new_guid}, type: 3}}", text)
    text = re.sub(r"m_Parent: \{fileID: \d+(, guid: [0-9a-f]{32}, type: \d)?\}", "m_Parent: {fileID: 0}", text)
    # keyword listelerini temizle (TCP2_* kalmasin)
    text = re.sub(r"m_ValidKeywords:\n(  - \S+\n)*", "m_ValidKeywords:\n" + ("  - _SURFACE_TYPE_TRANSPARENT\n" if unlit else ""), text)
    text = re.sub(r"m_InvalidKeywords:\n(  - \S+\n)*", "m_InvalidKeywords: []\n", text)
    # _BaseColor'i bake et (var olani guncelle yoksa ekle)
    bc = f"- _BaseColor: {{r: {r}, g: {g}, b: {b}, a: {a}}}"
    if get_color(text, "_BaseColor"):
        text = re.sub(r"- _BaseColor: \{[^}]*\}", bc, text)
    else:
        text = text.replace("m_Colors:\n", "m_Colors:\n    " + bc + "\n")
    # Lit: dusuk smoothness/metallic; Unlit-transparent: surface propertyleri
    floats = "    - _Smoothness: 0.15\n    - _Metallic: 0\n" if not unlit else \
             "    - _Surface: 1\n    - _Blend: 0\n    - _SrcBlend: 5\n    - _DstBlend: 10\n    - _ZWrite: 0\n    - _AlphaClip: 0\n"
    text = text.replace("m_Floats:\n", "m_Floats:\n" + floats, 1)
    if unlit:
        text = re.sub(r"m_CustomRenderQueue: -?\d+", "m_CustomRenderQueue: 3000", text)
    text = re.sub(r"disabledShaderPasses:\n(  - \S+\n)*", "disabledShaderPasses: []\n", text)
    open(path, "w").write(text)
    print(f"{'UNLIT' if unlit else 'LIT  '}: {os.path.basename(path)}  rgba=({r},{g},{b},{a})")
```

- [ ] **Step 2: Scripti çalıştır**

Run: `python3 <scratchpad>/convert_mats.py`
Expected: her SHAPESCOLOR/COLORS/HOLES/LVL mat için `LIT:`/`UNLIT:` satırı; `Custom_MaskShader` ve `FrontFrameWhite` için `SKIP (in-house...)`.

- [ ] **Step 3: TCP/paid GUID kalmadığını doğrula**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case1_FitTheShape
grep -rl "d5a641412fc0b774b9bf47cbff0ffece\|9b5a91723e7a840c6b53c694953a1f6e\|1cd0ae6487ac2024eb03b8e314495b2d\|9e7217c4dd508204e973a33bd00940ff" Materials/ ; echo "exit=$?"
```
Expected: hiç dosya listelenmez, `exit=1` (grep bulamadı).

- [ ] **Step 4: Commit**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
git add Assets/Case1_FitTheShape/Materials
git commit -m "feat: convert TCP materials to URP Lit/Unlit, bake colors, detach variants

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Prefab script strip + SpinButton font swap

**Files:**
- Create: scratchpad'de `strip_prefabs.py` (repoya girmez)
- Modify: `Assets/Case1_FitTheShape/Prefabs/{TargetSlotSegment,DeckSlot,SpinButton}.prefab`

**Interfaces:**
- Consumes: Task 2'nin kopyaladığı prefab'lar.
- Produces: Şirket MonoBehaviour'ları sökülmüş, m_Component listeleri temiz, SpinButton fontu LiberationSans SDF GUID'ine bağlı prefab'lar. Task 5'in doğrulaması buna dayanır.

- [ ] **Step 1: Strip scriptini yaz**

Scratchpad'e `strip_prefabs.py` olarak kaydet:

```python
#!/usr/bin/env python3
import re, os

DST = "/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case1_FitTheShape/Prefabs"
STRIP_GUIDS = {"fd64db82008a04d2ba8273517497e212", "fb9d59a47cfa5432dbe963196aa04acf", "4fff2ee4bbda4469bb394571fcf64333"}
LAYERLAB_FONT, TMP_FONT = "23ae78aa8db6e4eb9872a56fdb005f63", "8f586378b4e144a9851e7b34d9b748ee"

for name in os.listdir(DST):
    if not name.endswith(".prefab"): continue
    path = os.path.join(DST, name)
    text = open(path).read()
    header, *docs = text.split("--- !u!")   # docs: "114 &123\nMonoBehaviour:..."
    removed_ids, kept = set(), []
    for d in docs:
        m = re.match(r"114 &(\d+)", d)
        if m and any(g in d for g in STRIP_GUIDS):
            removed_ids.add(m.group(1)); continue
        kept.append(d)
    out = header + "".join("--- !u!" + d for d in kept)
    for rid in removed_ids:  # GameObject'in m_Component listesindeki dangling entry'yi de sil
        out = out.replace(f"  - component: {{fileID: {rid}}}\n", "")
    if LAYERLAB_FONT in out:
        out = out.replace(LAYERLAB_FONT, TMP_FONT)
        print(f"{name}: font swapped")
    if removed_ids:
        print(f"{name}: stripped {len(removed_ids)} MonoBehaviour")
    open(path, "w").write(out)
```

- [ ] **Step 2: Scripti çalıştır**

Run: `python3 <scratchpad>/strip_prefabs.py`
Expected: `TargetSlotSegment.prefab: stripped 1`, `DeckSlot.prefab: stripped 1`, `SpinButton.prefab: stripped 1` ve `SpinButton.prefab: font swapped`.

- [ ] **Step 3: Şirket kodu / paid GUID kalmadığını doğrula**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case1_FitTheShape
grep -rl "fd64db82008a04d2ba8273517497e212\|fb9d59a47cfa5432dbe963196aa04acf\|4fff2ee4bbda4469bb394571fcf64333\|23ae78aa8db6e4eb9872a56fdb005f63" Prefabs/; echo "exit=$?"
```
Expected: `exit=1` (hiçbir eşleşme yok).

- [ ] **Step 4: Commit**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
git add Assets/Case1_FitTheShape/Prefabs
git commit -m "feat: strip company scripts from prefabs, swap SpinButton font to TMP default

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: Unity import doğrulaması + TMP Essentials (MCP)

**Files:**
- Create: `Assets/TextMesh Pro/` (TMP Essentials import'u üretir)

**Interfaces:**
- Consumes: Task 1-4 çıktıları; Unity Editor'ün ROQ_Games_Dev_Case ile açık olması.
- Produces: Hatasız import edilmiş Case1 assetleri + LiberationSans SDF mevcut. Task 6-7 Unity içinde çalışacak.

- [ ] **Step 1: PREFLIGHT — doğru proje açık mı?**

MCP `script-execute` ile (body mode): `return UnityEngine.Application.dataPath;`
Expected: `/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets`.
**Değilse DUR** ve kullanıcıya sor: *"Unity, ROQ_Games_Dev_Case ile açık değil — case projesini Unity'de açar mısın? (Ivan Murzak MCP bağlantısı o projeden gelmeli)"*. Cevabı bekle.

- [ ] **Step 2: AssetDatabase refresh + konsol taraması**

1. `console-clear-logs` çağır.
2. `assets-refresh` çağır (Task 1'in manifest değişikliği de burada resolve olur; domain reload bekle).
3. `console-get-logs` (errors only) çağır.
Expected: Case1 assetlerine dair hiç error yok. (`Cinemachine` paketi resolve edilmiş olmalı — `package-list` ile `com.unity.cinemachine` görünmeli.)

- [ ] **Step 3: TMP Essentials'ı import et**

MCP `script-execute` (body mode):
```csharp
UnityEditor.AssetDatabase.ImportPackage("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage", false);
return "imported";
```
Sonra `assets-refresh`, sonra `assets-find` filter: `LiberationSans t:TMP_FontAsset`.
Expected: `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` bulunur (GUID `8f586378b4e144a9851e7b34d9b748ee`).

- [ ] **Step 4: Pembe materyal / missing referans taraması**

MCP `script-execute` (body mode):
```csharp
var sb = new System.Text.StringBuilder();
foreach (var guidStr in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Case1_FitTheShape" }))
{
    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guidStr);
    var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
    foreach (var r in go.GetComponentsInChildren<UnityEngine.Renderer>(true))
        foreach (var m in r.sharedMaterials)
            if (m == null) sb.AppendLine($"NULL MAT: {path} / {r.name}");
            else if (m.shader == null || m.shader.name == "Hidden/InternalErrorShader") sb.AppendLine($"BROKEN SHADER: {path} / {r.name} / {m.name}");
    foreach (var mf in go.GetComponentsInChildren<UnityEngine.MeshFilter>(true))
        if (mf.sharedMesh == null) sb.AppendLine($"NULL MESH: {path} / {mf.name}");
    foreach (var c in go.GetComponentsInChildren<UnityEngine.Component>(true))
        if (c == null) sb.AppendLine($"MISSING SCRIPT: {path}");
}
return sb.Length == 0 ? "CLEAN" : sb.ToString();
```
Expected: `CLEAN`. Değilse: raporlanan her sorunu çöz (eksik texture/mat → Task 2'deki `cpm` kalıbıyla kaynaktan kopyala + refresh; bozuk shader → Task 3 mapping'ine ekle + script'i tekrar çalıştır), sonra bu step'i tekrarla.

- [ ] **Step 5: Commit (TMP klasörü + varsa düzeltmeler)**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
git add -A
git commit -m "feat: import TMP Essentials, verify Case1 assets import clean

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 6: Staged sahne — FitTheShape.unity (MCP)

**Files:**
- Create: `Assets/Case1_FitTheShape/Scenes/FitTheShape.unity`

**Interfaces:**
- Consumes: Task 2-5'in prefab/materyalleri; Global Sabitler tablosundaki tüm yerleşim değerleri.
- Produces: Kamera + ışık + kurulu drum + deck'te 3 şekil içeren, script'siz sahne. README (Task 8) bu sahneyi işaret eder.

- [ ] **Step 1: Sahneyi oluştur**

MCP `scene-create` → path `Assets/Case1_FitTheShape/Scenes/FitTheShape.unity`, sonra `scene-set-active`. (`scene-create` sorun çıkarırsa fallback — `script-execute` body mode: `var s = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s, "Assets/Case1_FitTheShape/Scenes/FitTheShape.unity"); return "ok";`)

- [ ] **Step 2: Sahne iskeletini kur (kamera, ışıklar, drum, deck) — tek batch C#**

MCP `script-execute` (body mode) ile aşağıdaki batch'i çalıştır. Kod, Global Sabitler tablosunun birebir uygulamasıdır:

```csharp
using UnityEngine; using UnityEditor;
T Load<T>(string p) where T : Object => AssetDatabase.LoadAssetAtPath<T>(p);
string P = "Assets/Case1_FitTheShape/Prefabs/";
GameObject Spawn(string prefab, Transform parent) {
    var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(P + prefab + ".prefab"));
    go.transform.SetParent(parent, false); return go;
}
// --- Kamera
var cam = new GameObject("Main Camera").AddComponent<Camera>();
cam.gameObject.tag = "MainCamera";
cam.transform.SetPositionAndRotation(new Vector3(0f, 97.51f, -28.56f), new Quaternion(0.57357645f, 0f, 0f, 0.81915206f));
cam.fieldOfView = 10f; cam.clearFlags = CameraClearFlags.SolidColor;
cam.backgroundColor = new Color(0.95f, 0.92f, 0.86f);
cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
// --- Isiklar
var l1 = new GameObject("SLOTINSIDELIGHT").AddComponent<Light>();
l1.type = LightType.Directional; l1.intensity = 1.34f;
l1.transform.rotation = new Quaternion(0.8493997f, 0f, 0f, 0.5277502f);
var l2 = new GameObject("Directional Light").AddComponent<Light>();
l2.type = LightType.Directional; l2.intensity = 0.33f;
l2.transform.rotation = new Quaternion(0.81640714f, -0.28371835f, -0.022132609f, 0.50248724f);
// --- Drum root
var drum = new GameObject("Drum").transform;
drum.position = new Vector3(0f, 2.22f, 10.9f); drum.localScale = Vector3.one * 0.95f;
// Segment kolon genisligini olcmek icin bir ornek instantiate et
var probe = Spawn("TargetSlotSegment", drum);
probe.transform.localScale = new Vector3(0.87f, 1.47f, 1.47f);
float colW = 0f; foreach (var r in probe.GetComponentsInChildren<Renderer>()) colW = Mathf.Max(colW, r.bounds.size.x);
Object.DestroyImmediate(probe);
int cols = 5, ringN = 15; float radius = 0.5f, notch = 24f, frontAngle = 66.3f;
for (int c = 0; c < cols; c++) {
    float x = (c - (cols - 1) / 2f) * colW;
    for (int i = 0; i < ringN; i++) {
        float a = frontAngle + i * notch;
        var seg = Spawn("TargetSlotSegment", drum);
        var rot = Quaternion.Euler(a, 0f, 0f);
        seg.transform.localPosition = new Vector3(x, 0f, 0f) + rot * new Vector3(0f, 0f, -radius);
        seg.transform.localRotation = rot * Quaternion.Euler(-90f, 0f, 0f);
        seg.transform.localScale = new Vector3(0.87f, 1.47f, 1.47f);
        seg.name = $"Segment_c{c}_r{i}";
    }
}
float halfW = cols * colW / 2f;
foreach (var sx in new[] { -1f, 1f }) {
    var mount = Spawn("DrumSideMount", drum);
    mount.transform.localPosition = new Vector3(sx * (halfW + 0.85f), 0f, 0f);
    mount.transform.localScale = Vector3.one * 0.9657f;
    if (sx > 0) mount.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
}
var mid = Spawn("SlotMiddlePart", drum);
mid.transform.localRotation = Quaternion.Euler(4.85f, 180f, 0f); mid.transform.localScale = Vector3.one * 0.91f;
var frame = Spawn("FrontFrame", drum);
frame.transform.localPosition = Quaternion.Euler(frontAngle, 0f, 0f) * new Vector3(0f, 0f, -radius) + new Vector3(0f, 0f, -0.4f);
frame.transform.localRotation = Quaternion.Euler(frontAngle - 90f, 0f, 0f);
var dShadow = Spawn("DrumShadow", drum);
dShadow.transform.localPosition = new Vector3(0f, -5.5f, -1.5f);
dShadow.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
dShadow.transform.localScale = new Vector3(19.59f, 6.18f, 10.43f);
// --- Deck: 5 pedestal + 3 sekil
var deck = new GameObject("Deck").transform;
deck.position = drum.position + new Vector3(0f, -4.28f, -3.42f);
string[] shapes = { "Round", "Star", "Hexagon" };
for (int i = 0; i < 5; i++) {
    float x = (i - 2f) * 1.58f;
    var ped = Spawn("DeckSlot", deck);
    ped.transform.localPosition = new Vector3(x, 0f, 0f);
    ped.transform.localRotation = Quaternion.Euler(19.34f, 0f, 0f);
    ped.transform.localScale = new Vector3(1.22f, 1.56f, 1.22f);
    var shadow = Spawn("DeckSlotShadow", deck);
    shadow.transform.localPosition = new Vector3(x, -0.38f, 0f);
    shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    shadow.transform.localScale = new Vector3(0.34f, 0.25f, 0.34f);
    if (i >= 1 && i <= 3) {
        var sh = Spawn(shapes[i - 1], deck);
        sh.transform.localPosition = new Vector3(x, 0.645f, 0.11f);
        sh.transform.localScale = new Vector3(1.03f, 1.93f, 1.03f);
    }
}
// --- Spin button (deck sonundan offset)
var spin = Spawn("SpinButton", deck);
spin.transform.localPosition = new Vector3(2f * 1.58f + 1.86f, -0.07f, 0.05f);
spin.transform.localRotation = Quaternion.Euler(340.38f, 180f, 0f);
UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
return "scene built";
```

- [ ] **Step 3: Görsel doğrulama + iterasyon**

1. `scene-save` çağır, sonra `screenshot-game-view` al.
2. Screenshot'ı referans oyunla karşılaştır (drum yatay silindir, delikli segmentler öne bakıyor, deck altta, kadraj Gameplay.unity'dekiyle uyumlu).
3. Sapma varsa (segment yönü ters, drum ekran dışı, deck kayık): ilgili transform'u `gameobject-modify` ile düzelt ya da Step 2 kodundaki açı/işareti düzeltip objeleri silip yeniden çalıştır (`gameobject-destroy` Drum/Deck root'ları → batch tekrar). **En fazla 3 iterasyon**; hâlâ bozuksa kullanıcıya screenshot'la danış.
4. Mystery overlay görünüyorsa (soru işaretli desen): TargetSlotSegment prefabında ilgili child renderer'ı `assets-prefab-open` → `gameobject-modify` (m_IsActive=false) → `assets-prefab-save` ile kapat.

- [ ] **Step 4: Kaydet + commit**

`scene-save` çağır, sonra:
```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
git add Assets/Case1_FitTheShape/Scenes
git commit -m "feat: staged FitTheShape scene (camera, lights, drum, deck)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 7: Basit in-house VFX prefab'ları (MCP)

**Files:**
- Create: `Assets/Case1_FitTheShape/VFX/{ImpactBurst,RingShockwave,MiniConfetti}.prefab` + `Assets/Case1_FitTheShape/VFX/Materials/{ParticleSoft,ParticleHalftone}.mat`

**Interfaces:**
- Consumes: Task 2'nin kopyaladığı `Textures/Triangle-Soft.png` ve `Textures/HALFTONE.png`.
- Produces: Adayın juice için kullanabileceği 3 hazır particle prefabı.

- [ ] **Step 1: Particle materyalleri + 3 prefabı tek batch'te oluştur**

MCP `script-execute` (body mode):

```csharp
using UnityEngine; using UnityEditor;
string V = "Assets/Case1_FitTheShape/VFX/";
if (!AssetDatabase.IsValidFolder(V + "Materials")) AssetDatabase.CreateFolder("Assets/Case1_FitTheShape/VFX", "Materials");
Material MakeMat(string name, string texPath) {
    var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
    m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 1f); // transparent, additive-ish premultiply yerine alpha
    m.renderQueue = 3000;
    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
    if (tex != null) m.SetTexture("_BaseMap", tex);
    AssetDatabase.CreateAsset(m, V + "Materials/" + name + ".mat"); return m;
}
var soft = MakeMat("ParticleSoft", "Assets/Case1_FitTheShape/Textures/Triangle-Soft.png");
var half = MakeMat("ParticleHalftone", "Assets/Case1_FitTheShape/Textures/HALFTONE.png");
GameObject MakePS(string name, Material mat, System.Action<ParticleSystem> setup) {
    var go = new GameObject(name);
    var ps = go.AddComponent<ParticleSystem>();
    var main = ps.main; main.playOnAwake = false; main.loop = false;
    var em = ps.emission; em.rateOverTime = 0f;
    go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
    setup(ps);
    PrefabUtility.SaveAsPrefabAsset(go, V + name + ".prefab");
    Object.DestroyImmediate(go); return go;
}
// 1) ImpactBurst — sekil oturunca patlayan kisa burst
MakePS("ImpactBurst", soft, ps => {
    var main = ps.main; main.duration = 0.5f; main.startLifetime = 0.45f;
    main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
    main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
    main.gravityModifier = 1.2f;
    var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
    var shp = ps.shape; shp.shapeType = ParticleSystemShapeType.Sphere; shp.radius = 0.1f;
});
// 2) RingShockwave — tek buyuyen halka (tek particle + size over lifetime)
MakePS("RingShockwave", soft, ps => {
    var main = ps.main; main.duration = 0.4f; main.startLifetime = 0.35f; main.startSpeed = 0f;
    main.startSize = 0.2f;
    var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
    var sol = ps.sizeOverLifetime; sol.enabled = true;
    sol.size = new ParticleSystem.MinMaxCurve(8f, AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f));
    var col = ps.colorOverLifetime; col.enabled = true;
    var grad = new Gradient();
    grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f) },
                 new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
    col.color = grad;
});
// 3) MiniConfetti — renkli kutlama
MakePS("MiniConfetti", half, ps => {
    var main = ps.main; main.duration = 1f; main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
    main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f); main.gravityModifier = 1.5f;
    main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
    main.startRotation3D = true;
    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 1f));
    var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
    var shp = ps.shape; shp.shapeType = ParticleSystemShapeType.Cone; shp.angle = 30f; shp.radius = 0.05f;
    var rol = ps.rotationOverLifetime; rol.enabled = true;
    rol.z = new ParticleSystem.MinMaxCurve(-360f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);
});
AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
return "vfx created";
```

- [ ] **Step 2: Doğrula**

`assets-find` filter: `t:Prefab` klasör `Assets/Case1_FitTheShape/VFX`.
Expected: ImpactBurst, RingShockwave, MiniConfetti listelenir. `console-get-logs` (errors) temiz.

- [ ] **Step 3: Commit**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
git add Assets/Case1_FitTheShape/VFX
git commit -m "feat: add simple in-house particle VFX prefabs for Case1

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 8: README + final paid-asset denetimi

**Files:**
- Create: `README.md` (repo kökü)

**Interfaces:**
- Consumes: tüm önceki task çıktıları.
- Produces: Aday-facing README; paid-asset'siz olduğu doğrulanmış repo.

- [ ] **Step 1: README.md yaz**

Repo köküne `README.md`:

```markdown
# ROQ Games — Game Developer Case

Bu repo, Game Developer Case dokümanında tarif edilen 4 kısa gameplay
interaction'ını geliştirmeniz için hazırlanmış starter Unity projesidir.

## Gereksinimler

- **Unity 6000.3.11f1** (Unity 6.3) — URP
- Proje yalnızca resmi Unity paketleri içerir (URP, Input System, Cinemachine, uGUI/TMP, Timeline).

## Yapı

Her case kendi klasöründe self-contained'dır; bir case'e ait scene, script,
material, prefab vb. her şeyi kendi klasörü altında tutmanızı bekliyoruz:

    Assets/
      Case1_FitTheShape/   → Models, Materials, Textures, Prefabs, VFX, Scenes
      Case2_BlockHole/
      Case3_Stickerdom/
      Case4_Buca/

Her case klasöründeki `Scenes/` altında sahnelenmiş bir başlangıç sahnesi
bulunur (kamera + ışık + dizilmiş art). Bu sahnede hiç script yoktur —
interaction'ı siz kodlayacaksınız.

## Üçüncü parti paketler

DOTween, PrimeTween vb. tweening/animasyon kütüphanelerini ihtiyacınıza göre
kendiniz ekleyebilirsiniz. Hangi aracı neden seçtiğinizi README'nize not
düşmeniz yeterli.

## VFX / SFX

Her case klasöründe basit particle prefab'ları verilmiştir; kullanmak zorunda
değilsiniz. SFX verilmemiştir — eklemek isterseniz kaynak belirtin.
```

- [ ] **Step 2: Paid-asset denetimi**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
grep -ril "toony\|tcp2\|JMO\|layer lab\|epic toon\|cartoon fx\|CFXR\|dotween\|odin\|rayfire\|MoreMountains" Assets/ Packages/ | grep -v "Library"; echo "exit=$?"
```
Expected: `exit=1` (hiç eşleşme yok). Eşleşme varsa dosyayı incele — paid asset kalıntısıysa kaldır/dönüştür, yanlış pozitifse (ör. bizim README'deki "DOTween" kelimesi) not düşüp geç.

- [ ] **Step 3: Final konsol kontrolü**

MCP: `assets-refresh` → `console-get-logs` (errors only).
Expected: temiz.

- [ ] **Step 4: Commit**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
git add README.md
git commit -m "docs: add candidate-facing README

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```
