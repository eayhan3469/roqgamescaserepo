# Block Hole Case Assets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** samil-hole-block projesinden Block Hole sekansı (drag → hole → kırılma → düşüş) art assetlerini `Assets/Case2_BlockHole/` altına, paid asset'siz ve GUID koruyarak taşı; staged sahne + VFX ile.

**Architecture:** Case 1 pipeline'ının v2'si: dosya+meta kopyası → **path-tabanlı** materyal dönüşümü (shader guid → kaynak path → sınıf) → **path-tabanlı** script strip (script guid → kaynak path → sök/tut) + hedefli GUID remap'leri (LeanCommon siyah materyal, Epic Toon FX partikül materyalleri) → Unity MCP ile import doğrulama, sahne, VFX.

**Tech Stack:** Unity 6000.3.11f1, URP 17.3, Python 3, Ivan Murzak Unity MCP (kurulu, port 23435).

## Global Constraints

- Hedef: `/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case`, branch `case2-block-hole`. Kaynak (READ-ONLY): `/Users/macbookpro/Desktop/Unity_Projects/samil-hole-block`.
- **Paid asset yasak:** Toony Colors Pro/JMO, AllIn1SpriteShader, DinoFracture, RayFire, Feel/MoreMountains, LeanTouch/CW, Epic Toon FX, Layer Lab — hiçbir dosya/GUID izi Case2 klasörüne giremez. Kırık mesh .asset'leri (DinoFracture ÜRETİMİ, kendi içeriğimiz) serbesttir; DinoFracture script/prefab'ları yasaktır.
- Tüm kopyalar `.meta` ile (GUID korunur). Her şey `Assets/Case2_BlockHole/` altında self-contained.
- Unity işlemleri SADECE Ivan Murzak MCP. `script-execute` body-mode `return` derlenmezse full-code mode (Case 1 Task 5 raporundaki workaround). `screenshot-game-view` bir capture geriden gelir — önce çöp capture al.
- Kaynak index dosyası hazır: `<SCRATCH>/hb_guid_index.txt` (`<kaynak-path> <guid>` satırları; yeniden üretmek gerekirse: `cd kaynak && grep -r "^guid: " --include="*.meta" Assets | sed 's/\.meta:guid: / /'`). `<SCRATCH>` = `/private/tmp/claude-501/-Users-macbookpro-Desktop-Unity-Projects-ROQ-Games-Dev-Case/9482373b-cae1-4eae-af4a-e803c84a3644/scratchpad`.
- Commit mesajı sonu: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

## Sabitler (keşifte doğrulandı)

| Ne | Değer |
|---|---|
| Kamera (Gameplay.unity) | **Orthographic**, size 8.506944, pos (3.5, 5, 3.63), quat (0.6427876, 0, 0, 0.7660445) |
| Işık 1 (Directional Light.prefab) | directional, intensity 0.02, quat (0.8761058, 0.0538351, 0.15313274, 0.45397228) |
| Işık 2 (Directional Light (1).prefab) | directional, intensity 0.3, quat (0.14494713, 0.86564136, -0.30007166, 0.37364742) |
| Ambient (RenderSettings) | Trilight (mode 1): sky (0.8117647, 0.8117647, 0.8117647), equator (0.59607846, …), ground (0.27450982, …), intensity 1 |
| URP Lit / Unlit shader GUID | `933532a4fcc9baf4fa0491de14d08ed7` / `650dd9526735d5b46b79224bc6e94025` |
| URP Particles/Unlit shader GUID | `0406db5a14f94604a8c57ccfbc9f3b46` |
| LeanCommon Black.mat kullanıcısı | `Prefabs/Hole/Hole.prefab` (guid remap edilecek) |
| Epic Toon FX mat kullanıcıları | `Art/PREFAB/VFX/{BlockBreakage, BlockConsumed, IceExplosion, ShinePieceTile}.prefab` |
| StandardDynamicFracturePiece referansı | Sadece Block-Single.prefab'ın DinoFracture component'i içinde — strip ile birlikte gider |
| Yeni in-house mat GUID'leri (Task 2 üretir) | Black: `c2b10000b1ac000000000000000000a1` · PFX_SoftAdd: `c2b10000f0f7add000000000000000a2` · PFX_CircleAdd: `c2b10000c19c1ead00000000000000a3` |
| Strip path prefix'leri (script kaynak path'i bununla başlıyorsa SÖK) | `Assets/_HoleBlock/Scripts`, `Assets/_Scripts`, `Assets/DinoFracture`, `Assets/RayFire`, `Assets/Feel`, `Assets/Plugins/CW` |
| Bilinen paket script guid'leri (TUT) | TMP `9541d86e2fd84c1d9990edf0852d74ab`; URP camera-data `a79441f348de89743a2939f4d699eac1`, light-data `474bcb49f4fd6b7429b36013a1ab52b8` (Case 1 sahnesinden bilinen) |

---

### Task 1: Asset dosyalarını kopyala (dosya + .meta, GUID korunarak)

**Files:**
- Create: `Assets/Case2_BlockHole/{Models,Materials,Textures,Prefabs/{Blocks,Holes,Walls,Fractured,Covers,Chains,GameVFX},Shaders,VFX,Scenes}/` altına aşağıdaki tüm dosyalar

**Interfaces:**
- Produces: Task 2-3'ün işleyeceği .mat/.prefab dosyaları hedefte, kaynak GUID'leriyle.

- [ ] **Step 1: Kopyalama scriptini çalıştır**

```bash
SRC="/Users/macbookpro/Desktop/Unity_Projects/samil-hole-block/Assets"
HB="$SRC/_HoleBlock"
DST="/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case2_BlockHole"
mkdir -p "$DST/Models" "$DST/Materials/BlockColors" "$DST/Materials/HoleColors" "$DST/Materials/GridColors" "$DST/Materials/HoleArt" "$DST/Textures" "$DST/Prefabs/Blocks" "$DST/Prefabs/Holes" "$DST/Prefabs/Walls" "$DST/Prefabs/Fractured" "$DST/Prefabs/Covers" "$DST/Prefabs/Chains" "$DST/Prefabs/GameVFX" "$DST/Shaders" "$DST/VFX" "$DST/Scenes"
cpm() { cp "$1" "$2/$(basename "$1")"; cp "$1.meta" "$2/$(basename "$1").meta"; }
cpdir() { mkdir -p "$2"; (cd "$1" && find . -maxdepth 1 -type f ! -name "*.meta" | while read f; do cp "$f" "$2/"; cp "$f.meta" "$2/"; done); }
# --- Bloklar (BombMMF HARIC)
for b in Block-Single Block-2 Block-3 Block-L Block-T Block-Square Block-Cross Block-L-R Block-R-R; do cpm "$HB/Prefabs/ArtBlocks/$b.prefab" "$DST/Prefabs/Blocks"; done
# --- Kirik mesh depolari (3 konumdan, alt klasor yapisiyla)
cp -R "$HB/Prefabs/ArtBlocks/FractureMeshes" "$DST/Prefabs/Fractured/FractureMeshes"
cp "$HB/Prefabs/ArtBlocks/FractureMeshes.meta" "$DST/Prefabs/Fractured/FractureMeshes.meta"
cp -R "$HB/Prefabs/FracturedBlock" "$DST/Prefabs/Fractured/FracturedBlock"
cp "$HB/Prefabs/FracturedBlock.meta" "$DST/Prefabs/Fractured/FracturedBlock.meta"
cp -R "$HB/Scenes/Game/FractureMeshes" "$DST/Prefabs/Fractured/FractureMeshes-Game"
cp "$HB/Scenes/Game/FractureMeshes.meta" "$DST/Prefabs/Fractured/FractureMeshes-Game.meta"
cp -R "$HB/Scenes/TechinalArt/FractureMeshes" "$DST/Prefabs/Fractured/FractureMeshes-TA"
cp "$HB/Scenes/TechinalArt/FractureMeshes.meta" "$DST/Prefabs/Fractured/FractureMeshes-TA.meta"
# --- Delikler (Produceral HARIC; HoleClose + NewHoles dahil)
cpdir "$HB/Prefabs/Hole" "$DST/Prefabs/Holes"
rm -f "$DST/Prefabs/Holes/Hole-Produceral.prefab" "$DST/Prefabs/Holes/Hole-Produceral.prefab.meta"
cp -R "$HB/Prefabs/Hole/HoleClose" "$DST/Prefabs/Holes/HoleClose"; cp "$HB/Prefabs/Hole/HoleClose.meta" "$DST/Prefabs/Holes/HoleClose.meta"
cp -R "$HB/Prefabs/Hole/NewHoles" "$DST/Prefabs/Holes/NewHoles"; cp "$HB/Prefabs/Hole/NewHoles.meta" "$DST/Prefabs/Holes/NewHoles.meta"
# --- Duvarlar, gameplay, cover, chain, oyun VFX'i
cpdir "$HB/Prefabs/Walls" "$DST/Prefabs/Walls"
cpm "$HB/Prefabs/Gameplay/BlockConsumed_2.prefab" "$DST/Prefabs/GameVFX"
cpdir "$HB/Art/PREFAB/Hole" "$DST/Prefabs/Covers"
cpdir "$HB/Art/PREFAB/Chains" "$DST/Prefabs/Chains"
for v in BlockBreakage BlockConsumed IceExplosion ShinePieceTile; do cpm "$HB/Art/PREFAB/VFX/$v.prefab" "$DST/Prefabs/GameVFX"; done
# --- Modeller
for m in Bevelled Bevelled2 Bevelled3 Edge OneCorner TwoCorner FourCorner TwoEdge bevellikup; do cpm "$HB/Models/$m.fbx" "$DST/Models"; done
cpm "$HB/Art/BLOCKS/BLOCKS.fbx" "$DST/Models"
cpm "$HB/Art/GRID-WALL/MESH/Grid-Wall.fbx" "$DST/Models"
for m in HolePieces HoleFxPieces HoleStraightPieces NewBlocks frames; do cpm "$HB/Art/Models/$m.fbx" "$DST/Models"; done
# --- Materyaller
cpdir "$HB/Art/MATERIALS/BlockColors" "$DST/Materials/BlockColors"
cpdir "$HB/Art/MATERIALS/HoleColors" "$DST/Materials/HoleColors"
cpdir "$HB/Art/MATERIALS/NewGridColors" "$DST/Materials/GridColors"
for m in "FrameMat.mat" "MAT-GRID-WALL.mat" "MAT-GRID-FLOOR-DARK.mat" "MAT-GRID-FLOOR-LIGHT 1.mat" "MAT_BLOCK BASE.mat"; do cpm "$HB/Art/MATERIALS/$m" "$DST/Materials"; done
for f in "$HB/Art/HOLE/"*.mat; do cpm "$f" "$DST/Materials/HoleArt"; done
cpm "$HB/Art/HOLE/HOLE-GRADIENT.jpg" "$DST/Textures"
cpm "$SRC/_DragonMatch/Shaders/Custom_MaskShader.mat" "$DST/Materials"
# --- VFX sprite'lari (in-house, _BlockSort)
for i in 2 5 6 8 10; do cpm "$SRC/_BlockSort/Sprites/Blocks/Particles/Layer $i.png" "$DST/Textures"; done
echo "COPIED: $(find "$DST" -type f ! -name '*.meta' | wc -l)"
```
Beklenen: COPIED sayısı 300+ (131 kırık mesh .asset dahil). Sayıyı rapora yaz.

- [ ] **Step 2: 2. seviye bağımlılık kapaması — eksikleri kopyala**

Aşağıdaki python, hedefteki TÜM dosyaların guid referanslarını kaynak index'e karşı çözer; kaynak path'i `Assets/_HoleBlock`, `Assets/_BlockSort`, `Assets/_DragonMatch` altında olup hedefte OLMAYAN her dosyayı otomatik kopyalar (uygun alt klasöre: .fbx→Models, .mat→Materials, texture→Textures, .prefab→Prefabs/Extra, .shader/.shadergraph→Shaders, .asset→Prefabs/Fractured/ExtraMeshes). Paid path'lere (JMO/DinoFracture/RayFire/Feel/CW/Epic Toon/AllIn1/Layer Lab) çözülenler KOPYALANMAZ — Task 2-3 halledecek; sadece raporlanır. 2 tur çalıştır (yeni kopyalar yeni bağımlılık getirebilir).

```python
#!/usr/bin/env python3
import re, os, glob, shutil
SRC = "/Users/macbookpro/Desktop/Unity_Projects/samil-hole-block"
DST = "/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case2_BlockHole"
SCRATCH = "/private/tmp/claude-501/-Users-macbookpro-Desktop-Unity-Projects-ROQ-Games-Dev-Case/9482373b-cae1-4eae-af4a-e803c84a3644/scratchpad"
PAID = ("Assets/JMO", "Assets/DinoFracture", "Assets/RayFire", "Assets/Feel", "Assets/Plugins/CW", "Assets/Epic Toon", "Assets/Plugins/AllIn1", "Assets/Layer Lab")
INHOUSE = ("Assets/_HoleBlock", "Assets/_BlockSort", "Assets/_DragonMatch")
idx = {}
for line in open(SCRATCH + "/hb_guid_index.txt"):
    m = re.match(r"(.+) ([0-9a-f]{32})$", line.rstrip())
    if m: idx[m.group(2)] = m.group(1)
def dst_have():
    have = set()
    for meta in glob.glob(DST + "/**/*.meta", recursive=True):
        m = re.search(r"^guid: ([0-9a-f]{32})", open(meta).read(), re.M)
        if m: have.add(m.group(1))
    return have
def subdir_for(path):
    ext = os.path.splitext(path)[1].lower()
    return {"fbx": "Models", "mat": "Materials", "png": "Textures", "jpg": "Textures", "jpeg": "Textures", "tga": "Textures",
            "prefab": "Prefabs/Extra", "shader": "Shaders", "shadergraph": "Shaders", "asset": "Prefabs/Fractured/ExtraMeshes",
            "physicmaterial": "Materials", "anim": "Prefabs/Extra", "controller": "Prefabs/Extra"}.get(ext[1:], "Prefabs/Extra")
for rnd in (1, 2):
    have = dst_have(); missing_paid = []
    refs = set()
    for f in glob.glob(DST + "/**/*", recursive=True):
        if os.path.isfile(f) and not f.endswith(".meta") and os.path.splitext(f)[1] in (".prefab", ".mat", ".asset", ".unity", ".controller", ".anim"):
            refs |= set(re.findall(r"guid: ([0-9a-f]{32})", open(f, errors="ignore").read()))
    copied = 0
    for g in sorted(refs):
        if g in have or g not in idx: continue
        p = idx[g]
        if p.startswith(PAID): missing_paid.append(p); continue
        if not p.startswith(INHOUSE): continue
        sub = subdir_for(p); os.makedirs(f"{DST}/{sub}", exist_ok=True)
        base = os.path.basename(p)
        shutil.copy(f"{SRC}/{p}", f"{DST}/{sub}/{base}"); shutil.copy(f"{SRC}/{p}.meta", f"{DST}/{sub}/{base}.meta")
        copied += 1; print(f"[r{rnd}] + {sub}/{base}  <- {p}")
    print(f"round {rnd}: {copied} kopyalandi")
print("PAID refs (Task2-3 cozecek):"); [print("  ", p) for p in sorted(set(missing_paid))]
```
Beklenen: round 2'de 0 veya az sayıda kopya; PAID listesinde JMO shader'ları, DinoFracture/RayFire/Feel scriptleri, Epic Toon FX mat'ları, LeanCommon Black.mat — başka sürpriz paid path çıkarsa rapora yaz.

- [ ] **Step 3: Meta çifti doğrula + commit**

```bash
DST="/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case2_BlockHole"
find "$DST" -type f ! -name "*.meta" | while read f; do [ -f "$f.meta" ] || echo "META EKSIK: $f"; done; echo check-done
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case && git add Assets/Case2_BlockHole && git commit -m "feat: copy Block Hole art assets from samil-hole-block (GUID-preserving)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```
Beklenen: hiç `META EKSIK` yok.

---

### Task 2: In-house yedek materyaller + path-tabanlı materyal dönüşümü

**Files:**
- Create: `Assets/Case2_BlockHole/Materials/Black.mat`, `Assets/Case2_BlockHole/VFX/Materials/PFX_SoftAdd.mat`, `Assets/Case2_BlockHole/VFX/Materials/PFX_CircleAdd.mat` (+ .meta, sabit GUID'lerle)
- Modify: `Assets/Case2_BlockHole/**/*.mat` (in-place dönüşüm)
- Create: `<SCRATCH>/convert_mats_v2.py`

**Interfaces:**
- Consumes: Task 1 kopyaları + `hb_guid_index.txt`.
- Produces: Paid shader'sız materyaller; Task 3'ün remap edeceği 3 yeni mat GUID'i (Sabitler tablosunda).

- [ ] **Step 1: 3 yeni materyali üret**

Python ile her biri için `.mat` + `.meta` yaz. Şablon (Black.mat örneği; diğer ikisi Particles/Unlit `0406db5a14f94604a8c57ccfbc9f3b46` shader'ı, `_Surface: 1`, `_Blend: 2` (additive), renderQueue 3000 ve `_BaseMap` olarak sırasıyla `Layer 2.png` / `Layer 5.png` guid'lerini kullanır — o guid'leri kopyalanan .meta'lardan oku):

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Black
  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _Smoothness: 0.15
    - _Metallic: 0
    m_Colors:
    - _BaseColor: {r: 0.05, g: 0.05, b: 0.05, a: 1}
  m_BuildTextureStacks: []
```
Meta şablonu (guid satırına Sabitler tablosundaki değer):
```yaml
fileFormatVersion: 2
guid: c2b10000b1ac000000000000000000a1
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 2: convert_mats_v2.py yaz ve çalıştır**

Case 1 converter'ından farklar: sınıflandırma shader GUID hardcode yerine **kaynak path'ten**; `_MainTex` → `_BaseMap` taşınır. Tam script:

```python
#!/usr/bin/env python3
import re, glob, os
SRC = "/Users/macbookpro/Desktop/Unity_Projects/samil-hole-block"
DST = "/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case2_BlockHole"
SCRATCH = "/private/tmp/claude-501/-Users-macbookpro-Desktop-Unity-Projects-ROQ-Games-Dev-Case/9482373b-cae1-4eae-af4a-e803c84a3644/scratchpad"
LIT, UNLIT = "933532a4fcc9baf4fa0491de14d08ed7", "650dd9526735d5b46b79224bc6e94025"
idx = {}
for line in open(SCRATCH + "/hb_guid_index.txt"):
    m = re.match(r"(.+) ([0-9a-f]{32})$", line.rstrip())
    if m: idx[m.group(2)] = m.group(1)
guid2mat = {}
for meta in glob.glob(SRC + "/Assets/**/*.mat.meta", recursive=True):
    m = re.search(r"^guid: ([0-9a-f]{32})", open(meta).read(), re.M)
    if m: guid2mat[m.group(1)] = meta[:-5]
def get_color(text, prop):
    m = re.search(r"- " + prop + r": \{r: ([\d.e+-]+), g: ([\d.e+-]+), b: ([\d.e+-]+), a: ([\d.e+-]+)\}", text)
    return m.groups() if m else None
def resolve_base_color(path, depth=0):
    text = open(path).read()
    c = get_color(text, "_BaseColor") or get_color(text, "_Color")
    if c: return c
    p = re.search(r"m_Parent: \{fileID: -?\d+, guid: ([0-9a-f]{32})", text)
    if p and depth < 5 and p.group(1) in guid2mat:
        return resolve_base_color(guid2mat[p.group(1)], depth + 1)
    return ("1", "1", "1", "1")
def classify(shader_path):
    lp = shader_path.lower()
    if "jmo" in lp or "toony" in lp:
        return "UNLIT_T" if ("fx" in os.path.basename(lp) or "shadow" in lp) else "LIT"
    if "allin1" in lp: return "UNLIT_T"
    return None  # in-house / paket — dokunma
for path in glob.glob(DST + "/**/*.mat", recursive=True):
    text = open(path).read()
    sh = re.search(r"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})", text)
    if not sh: continue
    src_path = idx.get(sh.group(1), "")
    kind = classify(src_path) if src_path else None
    if kind is None:
        print(f"SKIP ({src_path or 'paket/bilinmeyen: ' + sh.group(1)[:8]}): {os.path.basename(path)}"); continue
    r, g, b, a = resolve_base_color(path)
    new_guid = UNLIT if kind == "UNLIT_T" else LIT
    text = re.sub(r"m_Shader: \{fileID: -?\d+, guid: [0-9a-f]{32}, type: \d\}",
                  f"m_Shader: {{fileID: 4800000, guid: {new_guid}, type: 3}}", text)
    text = re.sub(r"m_Parent: \{fileID: -?\d+(, guid: [0-9a-f]{32}, type: \d)?\}", "m_Parent: {fileID: 0}", text)
    text = re.sub(r"m_ValidKeywords:\n(  - \S+\n)*", "m_ValidKeywords:\n" + ("  - _SURFACE_TYPE_TRANSPARENT\n" if kind == "UNLIT_T" else ""), text)
    text = re.sub(r"m_InvalidKeywords:\n(  - \S+\n)*", "m_InvalidKeywords: []\n", text)
    # _MainTex -> _BaseMap (varsa ve _BaseMap yoksa)
    if "_BaseMap:" not in text:
        text = text.replace("- _MainTex:", "- _BaseMap:", 1)
    bc = f"- _BaseColor: {{r: {r}, g: {g}, b: {b}, a: {a}}}"
    if get_color(text, "_BaseColor"): text = re.sub(r"- _BaseColor: \{[^}]*\}", bc, text)
    else: text = text.replace("m_Colors:\n", "m_Colors:\n    " + bc + "\n")
    floats = {"LIT": [("_Smoothness", "0.15"), ("_Metallic", "0")],
              "UNLIT_T": [("_Surface", "1"), ("_Blend", "0"), ("_SrcBlend", "5"), ("_DstBlend", "10"), ("_ZWrite", "0"), ("_AlphaClip", "0")]}[kind]
    for k, v in floats:  # once ayni-isimli eskiyi sil, sonra ekle (Case 1 duplicate-key dersi)
        text = re.sub(r"    - " + k + r": [^\n]*\n", "", text)
    text = text.replace("m_Floats:\n", "m_Floats:\n" + "".join(f"    - {k}: {v}\n" for k, v in floats), 1)
    if kind == "UNLIT_T": text = re.sub(r"m_CustomRenderQueue: -?\d+", "m_CustomRenderQueue: 3000", text)
    text = re.sub(r"disabledShaderPasses:\n(  - \S+\n)*", "disabledShaderPasses: []\n", text)
    open(path, "w").write(text)
    print(f"{kind}: {os.path.basename(path)} rgba=({r},{g},{b},{a})")
```

- [ ] **Step 3: Doğrula — Case2'de paid shader guid'i kalmadı**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case2_BlockHole
python3 - <<'EOF'
import re, glob
SCRATCH = "/private/tmp/claude-501/-Users-macbookpro-Desktop-Unity-Projects-ROQ-Games-Dev-Case/9482373b-cae1-4eae-af4a-e803c84a3644/scratchpad"
idx = {}
for line in open(SCRATCH + "/hb_guid_index.txt"):
    m = re.match(r"(.+) ([0-9a-f]{32})$", line.rstrip())
    if m: idx[m.group(2)] = m.group(1)
bad = []
for f in glob.glob("Materials/**/*.mat", recursive=True):
    sh = re.search(r"m_Shader: \{fileID: -?\d+, guid: ([0-9a-f]{32})", open(f).read())
    if sh:
        p = idx.get(sh.group(1), "")
        if any(s in p for s in ("JMO", "AllIn1", "Epic Toon", "Layer Lab")): bad.append((f, p))
print("CLEAN" if not bad else bad)
EOF
```
Beklenen: `CLEAN`. Ayrıca duplicate-float taraması (Case 1 fix'indeki script) → `CLEAN`.

- [ ] **Step 4: Commit**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
git add Assets/Case2_BlockHole && git commit -m "feat: convert Block Hole materials to URP, add in-house replacement mats

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: Path-tabanlı prefab strip + GUID remap'leri

**Files:**
- Create: `<SCRATCH>/strip_prefabs_v2.py`
- Modify: `Assets/Case2_BlockHole/Prefabs/**/*.prefab` (in-place)

**Interfaces:**
- Consumes: Task 1 prefab'ları, Task 2'nin 3 yeni mat GUID'i, `hb_guid_index.txt`.
- Produces: Şirket/paid script'siz, dangling referanssız prefab'lar. Task 4 doğrulaması buna dayanır.

- [ ] **Step 1: strip_prefabs_v2.py yaz ve çalıştır**

Case 1'den farklar: strip listesi **path-tabanlı** (guid → kaynak path → prefix eşleşmesi); `m_Component` VE `m_AddedComponents` temizliği birlikte (Case 1 Task 4 dersi); Lean Black + Epic Toon FX mat **guid remap'i**. Tam script:

```python
#!/usr/bin/env python3
import re, os, glob
DST = "/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case2_BlockHole"
SCRATCH = "/private/tmp/claude-501/-Users-macbookpro-Desktop-Unity-Projects-ROQ-Games-Dev-Case/9482373b-cae1-4eae-af4a-e803c84a3644/scratchpad"
STRIP_PREFIXES = ("Assets/_HoleBlock/Scripts", "Assets/_Scripts", "Assets/DinoFracture", "Assets/RayFire", "Assets/Feel", "Assets/Plugins/CW")
idx = {}
for line in open(SCRATCH + "/hb_guid_index.txt"):
    m = re.match(r"(.+) ([0-9a-f]{32})$", line.rstrip())
    if m: idx[m.group(2)] = m.group(1)
# Remap tablosu: LeanCommon Black.mat -> in-house Black; Epic Toon FX mat'lari -> PFX mat'lari
BLACK_NEW = "c2b10000b1ac000000000000000000a1"
SOFT_NEW, CIRCLE_NEW = "c2b10000f0f7add000000000000000a2", "c2b10000c19c1ead00000000000000a3"
remap = {}
for g, p in idx.items():
    if p.startswith("Assets/Plugins/CW") and p.endswith("Black.mat"): remap[g] = BLACK_NEW
    elif p.startswith("Assets/Epic Toon") and p.endswith(".mat"):
        remap[g] = CIRCLE_NEW if "circle" in os.path.basename(p).lower() else SOFT_NEW
unresolved_report, stripped_report = [], []
for path in glob.glob(DST + "/Prefabs/**/*.prefab", recursive=True):
    text = open(path).read()
    # 1) remap
    for old, new in remap.items():
        if old in text:
            # ETFX mat sub-fileID'leri standart 2100000 degilse normalize et
            text = re.sub(r"\{fileID: -?\d+, guid: " + old + r", type: 2\}", "{fileID: 2100000, guid: " + new + ", type: 2}", text)
    # 2) strip
    header, *docs = text.split("--- !u!")
    removed_ids, kept = set(), []
    for d in docs:
        m = re.match(r"114 &(-?\d+)", d)
        if m:
            sg = re.search(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", d)
            if sg:
                src = idx.get(sg.group(1))
                if src and src.startswith(STRIP_PREFIXES):
                    removed_ids.add(m.group(1)); stripped_report.append(f"{os.path.basename(path)}: {os.path.basename(src)}"); continue
                if not src and sg.group(1) not in ("9541d86e2fd84c1d9990edf0852d74ab", "a79441f348de89743a2939f4d699eac1", "474bcb49f4fd6b7429b36013a1ab52b8"):
                    unresolved_report.append(f"{os.path.basename(path)}: guid {sg.group(1)}")
        kept.append(d)
    out = header + "".join("--- !u!" + d for d in kept)
    for rid in removed_ids:
        out = out.replace(f"  - component: {{fileID: {rid}}}\n", "")
        # m_AddedComponents list-item'i (3 satirlik blok) temizle
        out = re.sub(r"    - targetCorrespondingSourceObject: \{[^}]*\}\n      insertIndex: -?\d+\n      addedObject: \{fileID: " + rid + r"\}\n", "", out)
    out = re.sub(r"m_AddedComponents:\n(?=  m_)", "m_AddedComponents: []\n", out)
    if out != text or removed_ids: open(path, "w").write(out)
print(f"stripped {len(stripped_report)}:"); [print("  ", s) for s in stripped_report[:40]]
print("UNRESOLVED script guids (karar gerek):"); [print("  ", u) for u in sorted(set(unresolved_report))]
```

- [ ] **Step 2: Doğrula**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case2_BlockHole
python3 - <<'EOF'
import re, glob
SCRATCH = "/private/tmp/claude-501/-Users-macbookpro-Desktop-Unity-Projects-ROQ-Games-Dev-Case/9482373b-cae1-4eae-af4a-e803c84a3644/scratchpad"
idx = {}
for line in open(SCRATCH + "/hb_guid_index.txt"):
    m = re.match(r"(.+) ([0-9a-f]{32})$", line.rstrip())
    if m: idx[m.group(2)] = m.group(1)
PAID = ("Assets/JMO", "Assets/DinoFracture", "Assets/RayFire", "Assets/Feel", "Assets/Plugins/CW", "Assets/Epic Toon", "Assets/Plugins/AllIn1", "Assets/Layer Lab", "Assets/_HoleBlock/Scripts", "Assets/_Scripts")
bad = []
for f in glob.glob("Prefabs/**/*.prefab", recursive=True):
    for g in set(re.findall(r"guid: ([0-9a-f]{32})", open(f).read())):
        p = idx.get(g, "")
        if p.startswith(PAID): bad.append((f, p))
print("CLEAN" if not bad else "\n".join(str(b) for b in bad[:30]))
EOF
```
Beklenen: `CLEAN`. Değilse: script referansıysa STRIP_PREFIXES'i genişlet, mat/mesh referansıysa remap tablosuna ekle; scripti tekrar çalıştır (idempotent). Ayrıca dangling `addedObject`/`- component` kalmadığını Case 1 Task 4'teki grep kalıbıyla kontrol et.

- [ ] **Step 3: Commit**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
git add Assets/Case2_BlockHole/Prefabs && git commit -m "feat: strip paid/company scripts from Block Hole prefabs, remap paid material refs

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Unity import doğrulaması (MCP)

**Files:** yok (doğrulama + Unity'nin ürettiği klasör .meta'ları)

**Interfaces:**
- Consumes: Task 1-3 çıktıları; Unity Editor açık + MCP bağlı (port 23435).
- Produces: Hatasız import edilmiş Case2; Task 5-6 Unity içinde çalışabilir.

- [ ] **Step 1: Preflight** — `script-execute`: `Application.dataPath` `.../ROQ_Games_Dev_Case/Assets` ile bitmeli. Bağlantı yoksa 30 sn sonra 1 kez dene, sonra BLOCKED raporla.
- [ ] **Step 2:** `console-clear-logs` → `assets-refresh` → `console-get-logs` (errors). Beklenen: Case2'ye dair 0 error.
- [ ] **Step 3: Pembe materyal / missing script taraması** — C# scan kodunu `docs/superpowers/plans/2026-08-14-fit-the-shape-case-assets.md` Task 5 Step 4'ten aynen al, `"Assets/Case1_FitTheShape"` yerine `"Assets/Case2_BlockHole"` yaz, `script-execute` ile çalıştır. Beklenen: CLEAN ya da yalnızca kaynak oyunda da null olan runtime-slot pattern'leri (ör. boş MeshFilter placeholder'ları — kaynakla byte-diff ederek doğrula, raporla, whitelist'le).
- [ ] **Step 4: Commit** — Unity'nin ürettiği .meta/normalizasyon değişiklikleri: `git add -A && git commit -m "feat: verify Case2 assets import clean` + Co-Authored-By satırı.

---

### Task 5: Staged sahne — BlockHole.unity (MCP)

**Files:**
- Create: `Assets/Case2_BlockHole/Scenes/BlockHole.unity`

**Interfaces:**
- Consumes: Task 1-4 asset'leri; Sabitler tablosundaki kamera/ışık/ambient değerleri.
- Produces: Script'siz staged sahne: board (zemin + duvar çerçevesi) + 2-3 renkli hole + kenarda 2-3 blok.

- [ ] **Step 1: Sahne oluştur** — `scene-create` → `Assets/Case2_BlockHole/Scenes/BlockHole.unity` → `scene-set-active` (sorun olursa Case 1'deki EditorSceneManager fallback'i).
- [ ] **Step 2: İskelet batch C#** — `script-execute` ile: kamera (**orthographic = true, orthographicSize = 8.506944**, pos (3.5, 5, 3.63), quat (0.6427876, 0, 0, 0.7660445), SolidColor bg (0.93, 0.91, 0.88) başlangıç), 2 directional ışık (Sabitler tablosundaki quat+intensity), RenderSettings: `ambientMode = AmbientMode.Trilight; ambientSkyColor/EquatorColor/GroundColor` Sabitler'den. Board: `Prefabs/Walls` parçalarıyla dikdörtgen çerçeve (ör. 7×9 hücre; duvar parçası boyutunu Case 1'deki gibi renderer bounds'tan ölç), içine grid zemini (GridColors materyalli quad'lar veya Grid-Wall.fbx zemin mesh'i — kaynakta hangisi zemin görünümünü veriyorsa), 3 hole (`Prefabs/Holes/NewHoles`'tan Single, 2, L — farklı HoleColors renkleriyle), board kenarında 3 blok (`Blocks/Block-Single`, `Block-2`, `Block-L`). Kod şablonu: `docs/superpowers/plans/2026-08-14-fit-the-shape-case-assets.md` Task 6 Step 2'deki batch C# (Spawn helper, bounds ölçümü, PrefabUtility.InstantiatePrefab pattern'i) — kamera/ışık/yerleşim bölümlerini bu task'ın değerleriyle değiştirerek uyarla; uygulanan gerçek batch kodunu rapora ekle.
- [ ] **Step 3: Görsel iterasyon** — çöp capture + `screenshot-game-view` → referans oyunla karşılaştır (üstten hafif açılı ortho, grid board, renkli delikler, kenarda bloklar) → en fazla 3 iterasyon; hâlâ bozuksa DONE_WITH_CONCERNS + screenshot. Screenshot'ları `<workspace>/task5-*.png` olarak kaydet. Triplanar→Lit dönüşümü zemin/duvar dokularını bozmuş görünüyorsa: ilgili materyalde `_BaseMap`'i kaldırıp düz `_BaseColor`'a çek (kaynak renkle), raporla.
- [ ] **Step 4:** `scene-save` → commit `"feat: staged BlockHole scene (ortho camera, board, holes, blocks)"` + Co-Authored-By.

---

### Task 6: Kırılma temalı in-house VFX (MCP)

**Files:**
- Create: `Assets/Case2_BlockHole/VFX/{DustPuff,DebrisBurst,ImpactRing}.prefab`

**Interfaces:**
- Consumes: Task 2'nin PFX_SoftAdd/PFX_CircleAdd materyalleri (`Assets/Case2_BlockHole/VFX/Materials/`).
- Produces: 3 particle prefab.

- [ ] **Step 1: Batch C#** — `MakePS` helper kodunu `docs/superpowers/plans/2026-08-14-fit-the-shape-case-assets.md` Task 7 Step 1'den aynen al (materyal oluşturma kısmını atla — PFX mat'ları Task 2'de üretildi, `AssetDatabase.LoadAssetAtPath` ile yükle), şu üç kurulumla:
  - **DustPuff** (PFX_SoftAdd): burst 8, sphere r 0.2, startSpeed 0.5-1.2, startSize 0.4-0.8, lifetime 0.6-0.9, gravity 0, colorOverLifetime alpha 0.5→0, sizeOverLifetime 0.6→1.
  - **DebrisBurst** (PFX_CircleAdd): burst 20, sphere r 0.1, startSpeed 3-6, startSize 0.08-0.2, lifetime 0.5-0.8, gravity 2, rotationOverLifetime ±360°, startColor iki-renk-arası (0.8,0.7,0.5)↔(0.5,0.45,0.4) — ahşap/moloz tonu.
  - **ImpactRing** (PFX_SoftAdd): tek particle, startSize 0.3, sizeOverLifetime → 6 (EaseInOut), lifetime 0.3, alpha 0.7→0.
  Hepsi playOnAwake false, loop false. `PrefabUtility.SaveAsPrefabAsset` → `Assets/Case2_BlockHole/VFX/`.
- [ ] **Step 2: Doğrula** — `assets-find` 3 prefab + console temiz.
- [ ] **Step 3: Commit** — `"feat: add fracture-themed particle VFX for Case2"` + Co-Authored-By.

---

### Task 7: README güncelle + path-tabanlı final denetim

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: tümü.
- Produces: Case 2 satırı işlenmiş README; denetimden geçmiş branch.

- [ ] **Step 1: README'ye Case 2 bölümü ekle** — Yapı bölümündeki `Case2_BlockHole/` satırının yanına içerik özeti gelecek şekilde README'deki klasör bloğunu güncelle:

```markdown
    Assets/
      Case1_FitTheShape/   → Models, Materials, Textures, Prefabs, VFX, Scenes (staged sahne hazır)
      Case2_BlockHole/     → Models, Materials, Textures, Prefabs (Blocks/Holes/Walls/Fractured), VFX, Scenes (staged sahne hazır)
      Case3_Stickerdom/
      Case4_Buca/
```
Ayrıca VFX/SFX notuna bir cümle ekle: "Case 2'de blokların önceden kırılmış (pre-fractured) mesh parçaları `Prefabs/Fractured/` altındadır — kırılma efektinizi bunlarla kurabilirsiniz."

- [ ] **Step 2: Path-tabanlı final denetim** — Task 3 Step 2'deki python taramasını TÜM Case2 dosya tipleri (.prefab, .mat, .unity, .asset, .controller, .anim) üzerinde çalıştır; ek olarak Case 1'in metin grep'i (`toony|tcp2|JMO|layer lab|epic toon|cartoon fx|CFXR|dotween|odin|rayfire|MoreMountains|dinofracture|allin1|lean`) — `Encoding/Decoding` tarzı substring false-positive'leri ayıklayarak. Beklenen: gerçek eşleşme 0.
- [ ] **Step 3: MCP final** — `assets-refresh` → `console-get-logs` errors temiz.
- [ ] **Step 4: Commit** — `"docs: add Case2 to README, final license audit"` + Co-Authored-By.
