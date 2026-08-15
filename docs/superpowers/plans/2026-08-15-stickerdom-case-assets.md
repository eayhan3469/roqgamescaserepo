# Stickerdom Case Assets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** stickerpages projesinden Stickerdom sekansı (tap → peel → move → attach) prefab/art'ını ve kullanıcının 8 örnek sticker'ını `Assets/Case3_Stickerdom/` altına hazırla; ghost varyantları + staged sahne + VFX ile.

**Architecture:** Case 2 pipeline v2'nin hafif uygulaması + iki yenilik: (1) kullanıcı PNG'lerinden programatik sprite üretimi (ghost + FX dokuları) ve şablondan .meta yazımı (yeni GUID'ler), (2) closure v3 — `.cs/.asmdef/.dll` hariç, "diğer üçüncü-parti" kovası raporlu, hedefte-var-olan-GUID atlama kuralı (Feel'in TMP kopyası dersi).

**Tech Stack:** Unity 6000.3.11f1, URP 17.3, Python 3 + Pillow, Ivan Murzak Unity MCP (port 23435, script-execute FULL-CODE mode).

## Global Constraints

- Hedef: `/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case`, branch `case3-stickerdom`. Kaynaklar (READ-ONLY): `/Users/macbookpro/Desktop/Unity_Projects/stickerpages` ve `/Users/macbookpro/Desktop/StickerPages/Stickers_Split`.
- **Paid asset yasak** (JMO/DinoFracture/RayFire/Feel/CW/Epic Toon/AllIn1/Layer Lab) — GUID izi dahil. Twemoji sprite'ları ve TWEMOJI-LICENSE.md alınmaz.
- Kopyalar `.meta` ile (GUID korunur); YENİ üretilen dosyalar (sticker/ghost/FX sprite'ları, PFX mat'ları) yeni GUID alır. **Hedef repoda zaten var olan bir GUID asla ikinci kez kopyalanmaz** (kopyalamadan önce kontrol).
- Her şey `Assets/Case3_Stickerdom/` altında self-contained.
- Unity işlemleri SADECE Ivan Murzak MCP; script-execute FULL-CODE mode (body-mode return CS0127 verir); screenshot-game-view bir capture geriden gelir (önce çöp capture).
- **Commit'ler path-scoped:** yalnızca `Assets/Case3_Stickerdom` (+ Task 7'de README.md). `git add -A` YASAK — working tree'de kullanıcının Case1 el düzenlemeleri var.
- Kaynak index: `<SCRATCH>/sp_guid_index.txt` (hazır; format `<path> <guid>`). `<SCRATCH>` = `/private/tmp/claude-501/-Users-macbookpro-Desktop-Unity-Projects-ROQ-Games-Dev-Case/9482373b-cae1-4eae-af4a-e803c84a3644/scratchpad`.
- Commit mesajı sonu: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

## Sabitler (keşifte doğrulandı)

| Ne | Değer |
|---|---|
| Kamera (Gameplay.unity) | **Orthographic**, size 13.845, pos (0, 1, -10), rot identity (0,0,0,1) |
| Işık | 1 directional, intensity 1, quat (0.40821788, -0.23456968, 0.10938163, 0.8754261) |
| Ambient | mode 0 (Skybox), AmbientSkyColor (0.212, 0.227, 0.259) — sahnede RenderSettings'e aynen yazılır |
| Feel LiberationSans GUID | `8f586378b4e144a9851e7b34d9b748ee` — **TMP Essentials'la AYNI; remap GEREKMEZ, Feel kopyası KOPYALANMAZ** (repoda Assets/TextMesh Pro'da zaten var) |
| Kopyalanacak 5 prefab | `_StickerPages/Prefabs/Sticker/{CardModel,SheetModel,DrawPileModel,WastePileModel,PegModel}.prefab` |
| Sayfa art'ı | `_StickerPages/Sprites/StickerPages/EmptyStickerSheetBackground.png`, `_StickerPages/Sprites/StickersMisc/bg_gradient.png` |
| Sticker kaynak seçimi (8) | `Stickers_Split/{Hayvanlar_V1,Meyve_V1,Arac_V1,Enstrunman_V1,Kumsal_V1,Spor_V1,Teknoloji_V1,Doga_V1}/01.png` |
| Hedef sticker isimleri | `sticker_hayvan.png, sticker_meyve.png, sticker_arac.png, sticker_enstruman.png, sticker_kumsal.png, sticker_spor.png, sticker_teknoloji.png, sticker_doga.png` (+ `_ghost` çiftleri) |
| Ghost formülü | RGB sabit (199,199,199); alfa = kaynak alfa × 0.55 |
| Sprite meta şablonu | `stickerpages/Assets/_StickerPages/Sprites/Stickers/daisy.png.meta` (TextureImporter sprite ayarları aynen, guid satırı yeni uuid4 hex) |
| PFX mat GUID'leri (Task 2 üretir) | PFX_StickerSoft: `c3b10000f0f7add000000000000000b1` · PFX_StickerStar: `c3b10000c19c1ead00000000000000b2` (URP Particles/Unlit `0406db5a14f94604a8c57ccfbc9f3b46`) |
| FX doku üretimi | `fx_softcircle.png` (256px radyal gradyan) + `fx_star4.png` (4 kollu yıldız, hafif blur) — Task 2'de PIL ile |
| Strip path prefix'leri | `Assets/_StickerPages/Scripts`, `Assets/_Scripts`, `Assets/DinoFracture`, `Assets/RayFire`, `Assets/Feel`, `Assets/Plugins/CW`, `Assets/JMO` |
| Tutulan paket guid'leri | TMP `9541d86e2fd84c1d9990edf0852d74ab`, TMP font `8f586378b4e144a9851e7b34d9b748ee`, URP `a79441f348de89743a2939f4d699eac1` / `474bcb49f4fd6b7429b36013a1ab52b8` |

---

### Task 1: Prefab + sayfa art kopyası ve closure v3

**Files:**
- Create: `Assets/Case3_Stickerdom/{Prefabs,Sprites,Materials,VFX/Materials,Scenes,Textures}/` altına: 5 prefab + 2 sayfa sprite'ı (+ closure'ın getirdikleri)
- Create: `<SCRATCH>/close_deps_v3.py`

**Interfaces:**
- Produces: Task 3'ün strip edeceği prefab'lar; Task 5'in kullanacağı sayfa art'ı. Closure raporu (PAID / OTHER-3RD-PARTY / SKIPPED-EXISTING kovaları).

- [ ] **Step 1: Temel kopya**

```bash
SRC="/Users/macbookpro/Desktop/Unity_Projects/stickerpages/Assets/_StickerPages"
DST="/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case3_Stickerdom"
mkdir -p "$DST/Prefabs" "$DST/Sprites" "$DST/Materials" "$DST/VFX/Materials" "$DST/Scenes" "$DST/Textures"
cpm() { cp "$1" "$2/$(basename "$1")"; cp "$1.meta" "$2/$(basename "$1").meta"; }
for p in CardModel SheetModel DrawPileModel WastePileModel PegModel; do cpm "$SRC/Prefabs/Sticker/$p.prefab" "$DST/Prefabs"; done
cpm "$SRC/Sprites/StickerPages/EmptyStickerSheetBackground.png" "$DST/Sprites"
cpm "$SRC/Sprites/StickersMisc/bg_gradient.png" "$DST/Sprites"
echo "BASE COPIED: $(find "$DST" -type f ! -name '*.meta' | wc -l)"
```
Beklenen: `BASE COPIED: 7`.

- [ ] **Step 2: Closure v3 — 2 tur**

`<SCRATCH>/close_deps_v3.py` olarak kaydet ve çalıştır. Case 2 closure'ından farklar: script/derlenebilir uzantılar hedef DEĞİL; üç kova raporu; hedef-repo GUID çakışma ataması:

```python
#!/usr/bin/env python3
import re, os, glob, shutil, sys
SRC = "/Users/macbookpro/Desktop/Unity_Projects/stickerpages"
REPO = "/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case"
DST = REPO + "/Assets/Case3_Stickerdom"
SCRATCH = "/private/tmp/claude-501/-Users-macbookpro-Desktop-Unity-Projects-ROQ-Games-Dev-Case/9482373b-cae1-4eae-af4a-e803c84a3644/scratchpad"
PAID = ("Assets/JMO", "Assets/DinoFracture", "Assets/RayFire", "Assets/Feel", "Assets/Plugins/CW", "Assets/Epic Toon", "Assets/Plugins/AllIn1", "Assets/Layer Lab")
INHOUSE = ("Assets/_StickerPages",)
NEVER_COPY_EXT = (".cs", ".asmdef", ".dll", ".unitypackage", ".md")
idx = {}
for line in open(SCRATCH + "/sp_guid_index.txt"):
    m = re.match(r"(.+) ([0-9a-f]{32})$", line.rstrip())
    if m: idx[m.group(2)] = m.group(1)
def repo_guids():
    have = set()
    for meta in glob.glob(REPO + "/Assets/**/*.meta", recursive=True):
        m = re.search(r"^guid: ([0-9a-f]{32})", open(meta, errors="ignore").read(), re.M)
        if m: have.add(m.group(1))
    return have
def subdir_for(path):
    ext = os.path.splitext(path)[1].lower().lstrip(".")
    return {"png": "Sprites", "jpg": "Sprites", "psd": "Sprites", "mat": "Materials", "fbx": "Textures",
            "prefab": "Prefabs", "shader": "Materials", "shadergraph": "Materials", "asset": "Prefabs",
            "anim": "Prefabs", "controller": "Prefabs"}.get(ext, "Prefabs")
paid_hits, other_3rd, skipped_existing = [], [], []
for rnd in (1, 2):
    have = repo_guids()
    refs = set()
    for f in glob.glob(DST + "/**/*", recursive=True):
        if os.path.isfile(f) and os.path.splitext(f)[1] in (".prefab", ".mat", ".asset", ".unity", ".controller", ".anim"):
            refs |= set(re.findall(r"guid: ([0-9a-f]{32})", open(f, errors="ignore").read()))
    copied = 0
    for g in sorted(refs):
        if g not in idx: continue
        p = idx[g]
        if os.path.splitext(p)[1].lower() in NEVER_COPY_EXT: continue
        if g in have:
            if not p.startswith(INHOUSE): skipped_existing.append(p)
            continue
        if p.startswith(PAID): paid_hits.append(p); continue
        if not p.startswith(INHOUSE): other_3rd.append(p); continue
        sub = subdir_for(p); os.makedirs(f"{DST}/{sub}", exist_ok=True)
        base = os.path.basename(p)
        if os.path.exists(f"{DST}/{sub}/{base}"): base = os.path.splitext(base)[0] + "-2" + os.path.splitext(base)[1]
        shutil.copy(f"{SRC}/{p}", f"{DST}/{sub}/{base}"); shutil.copy(f"{SRC}/{p}.meta", f"{DST}/{sub}/{base}.meta")
        copied += 1; print(f"[r{rnd}] + {sub}/{base} <- {p}")
    print(f"round {rnd}: {copied}")
print("PAID (strip/remap ile çözülecek, KOPYALANMADI):"); [print("  ", x) for x in sorted(set(paid_hits))]
print("OTHER-3RD-PARTY (KARAR GEREK — koordinatöre raporla):"); [print("  ", x) for x in sorted(set(other_3rd))]
print("SKIPPED-EXISTING (hedefte aynı GUID zaten var):"); [print("  ", x) for x in sorted(set(skipped_existing))]
```
Beklenen: PAID kovasında `Assets/Feel/.../LiberationSans SDF.asset`... **hayır** — o SKIPPED-EXISTING'e düşer (guid repoda var). OTHER-3RD-PARTY boş ya da az; doluysa her kalemi rapora yaz, kopyalama.

- [ ] **Step 3: Meta çifti kontrolü + commit**

```bash
DST="/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case3_Stickerdom"
find "$DST" -type f ! -name "*.meta" | while read f; do [ -f "$f.meta" ] || echo "META EKSIK: $f"; done; echo check-done
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case && git add Assets/Case3_Stickerdom && git commit -m "feat: copy Stickerdom prefabs and page art from stickerpages (GUID-preserving)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Sticker seti — 8 sticker + ghost'lar + FX dokuları + PFX mat'ları

**Files:**
- Create: `Assets/Case3_Stickerdom/Sprites/Stickers/` altına 16 PNG (+16 .meta), `Assets/Case3_Stickerdom/Textures/{fx_softcircle,fx_star4}.png` (+ .meta), `Assets/Case3_Stickerdom/VFX/Materials/{PFX_StickerSoft,PFX_StickerStar}.mat` (+ .meta)
- Create: `<SCRATCH>/make_stickers.py`

**Interfaces:**
- Consumes: Stickers_Split kaynak PNG'leri; daisy.png.meta şablonu.
- Produces: Task 5'in sahneleyeceği sticker/ghost sprite'ları; Task 6'nın kullanacağı PFX mat GUID'leri (Sabitler tablosundaki).

- [ ] **Step 1: make_stickers.py yaz ve çalıştır**

```python
#!/usr/bin/env python3
import os, uuid, re
from PIL import Image, ImageFilter, ImageDraw
SPLIT = "/Users/macbookpro/Desktop/StickerPages/Stickers_Split"
DST = "/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case3_Stickerdom"
META_TPL = open("/Users/macbookpro/Desktop/Unity_Projects/stickerpages/Assets/_StickerPages/Sprites/Stickers/daisy.png.meta").read()
PICKS = [("Hayvanlar_V1", "sticker_hayvan"), ("Meyve_V1", "sticker_meyve"), ("Arac_V1", "sticker_arac"),
         ("Enstrunman_V1", "sticker_enstruman"), ("Kumsal_V1", "sticker_kumsal"), ("Spor_V1", "sticker_spor"),
         ("Teknoloji_V1", "sticker_teknoloji"), ("Doga_V1", "sticker_doga")]
os.makedirs(DST + "/Sprites/Stickers", exist_ok=True)
def write_meta(png_path):
    meta = re.sub(r"^guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, META_TPL, flags=re.M)
    open(png_path + ".meta", "w").write(meta)
for folder, name in PICKS:
    src = f"{SPLIT}/{folder}/01.png"
    img = Image.open(src).convert("RGBA")
    out = f"{DST}/Sprites/Stickers/{name}.png"
    img.save(out); write_meta(out)
    a = img.split()[3].point(lambda v: int(v * 0.55))
    ghost = Image.new("RGBA", img.size, (199, 199, 199, 0)); ghost.putalpha(a)
    gout = f"{DST}/Sprites/Stickers/{name}_ghost.png"
    ghost.save(gout); write_meta(gout)
    print(name, img.size)
# FX dokulari
os.makedirs(DST + "/Textures", exist_ok=True)
S = 256
soft = Image.new("RGBA", (S, S), (255, 255, 255, 0))
d = ImageDraw.Draw(soft)
for r in range(S // 2, 0, -1):
    alpha = int(255 * (1 - r / (S / 2)) ** 2)
    d.ellipse([S/2 - r, S/2 - r, S/2 + r, S/2 + r], fill=(255, 255, 255, alpha))
p = f"{DST}/Textures/fx_softcircle.png"; soft.save(p); write_meta(p)
star = Image.new("RGBA", (S, S), (255, 255, 255, 0))
d = ImageDraw.Draw(star)
c, w = S / 2, S * 0.08
d.polygon([(c, 8), (c + w, c - w), (S - 8, c), (c + w, c + w), (c, S - 8), (c - w, c + w), (8, c), (c - w, c - w)], fill=(255, 255, 255, 255))
star = star.filter(ImageFilter.GaussianBlur(3))
p = f"{DST}/Textures/fx_star4.png"; star.save(p); write_meta(p)
print("fx dokulari yazildi")
```
Pillow yoksa: `python3 -m pip install --user pillow` (kullanıcı ortamına kurulum serbest — dev makinesi).

- [ ] **Step 2: PFX materyallerini yaz** — Case 2 Task 2'deki .mat/.meta YAML şablonunun aynısı (URP Particles/Unlit `0406db5a14f94604a8c57ccfbc9f3b46`, `_Surface: 1`, `_Blend: 2`, queue 3000): `PFX_StickerSoft.mat` guid `c3b10000f0f7add000000000000000b1`, `_BaseMap` = fx_softcircle.png'nin (yeni üretilen) guid'i; `PFX_StickerStar.mat` guid `c3b10000c19c1ead00000000000000b2`, `_BaseMap` = fx_star4.png guid'i. Şablonun tam YAML'ı `docs/superpowers/plans/2026-08-14-block-hole-case-assets.md` Task 2 Step 1'de.

- [ ] **Step 3: Doğrula + commit**

```bash
DST="/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case3_Stickerdom"
ls "$DST/Sprites/Stickers" | grep -c png$   # beklenen: 16
find "$DST" -name "*.png" ! -name "*.meta" | while read f; do [ -f "$f.meta" ] || echo "META EKSIK: $f"; done
python3 -c "
import re, glob
gs = [re.search(r'guid: ([0-9a-f]{32})', open(m).read()).group(1) for m in glob.glob('$DST/**/*.meta', recursive=True)]
assert len(gs) == len(set(gs)), 'DUPLICATE GUID!'
print('guid-unique OK', len(gs))"
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case && git add Assets/Case3_Stickerdom && git commit -m "feat: add 8 sample stickers with generated ghosts, FX textures and particle mats

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: Prefab strip v2 + materyal sweep

**Files:**
- Create: `<SCRATCH>/strip_prefabs_v3.py` (Case 2 v2 scriptinin uyarlaması)
- Modify: `Assets/Case3_Stickerdom/Prefabs/*.prefab`

**Interfaces:**
- Consumes: Task 1 prefab'ları, `sp_guid_index.txt`.
- Produces: Script'siz, paid-referanssız prefab'lar.

- [ ] **Step 1: Strip script** — `docs/superpowers/plans/2026-08-14-block-hole-case-assets.md` Task 3 Step 1'deki `strip_prefabs_v2.py`'yi şu değişikliklerle uyarla: `SCRATCH` index dosyası `sp_guid_index.txt`; `STRIP_PREFIXES` = Sabitler tablosundaki liste; `DST` = Case3 path; remap tablosu BOŞ başlar (bu case'te bilinen paid mat referansı yok — script yine de PAID path'e çözülen mat/mesh referansı bulursa STRIP değil RAPOR etsin); **`orig` snapshot düzeltmesi dahil** (`orig = open(path).read(); text = orig`, sonda `if out != orig or removed_ids:`). Çalıştır; beklenen: 5 prefab'dan 5 Model scripti sökülür (CardModel/SheetModel/DrawPileModel/WastePileModel/PegModel .cs'leri), UNRESOLVED listesi boş (TMP/URP guid'leri whitelist'te).

- [ ] **Step 2: Doğrula** — Case 2 Task 3 Step 2'deki paid-path sweep'i Case3 klasörüne uyarla (index: sp_guid_index.txt; PAID prefix'lere `Assets/_StickerPages/Scripts` ve `Assets/_Scripts` dahil) → `CLEAN`. Dangling `- component:`/`addedObject:` grep'leri → temiz. Materyal sweep: Case3'te .mat varsa shader guid'leri paid path'e çözülmemeli (closure zaten paid kopyalamadı; kontrol formalite).

- [ ] **Step 3: Commit**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
git add Assets/Case3_Stickerdom/Prefabs && git commit -m "feat: strip company scripts from Stickerdom prefabs

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Unity import doğrulaması (MCP)

**Interfaces:** Task 1-3 çıktıları; Unity açık, MCP 23435.

- [ ] **Step 1: Preflight** — script-execute (FULL-CODE mode): Application.dataPath `.../ROQ_Games_Dev_Case/Assets` ile bitmeli; değilse 30 sn sonra 1 deneme, sonra BLOCKED.
- [ ] **Step 2:** console-clear-logs → assets-refresh → console-get-logs (Error). Beklenen: 0 (sprite import'ları dahil).
- [ ] **Step 3: Scan** — `docs/superpowers/plans/2026-08-14-fit-the-shape-case-assets.md` Task 5 Step 4 scan'ini `Assets/Case3_Stickerdom` için çalıştır; ek olarak ParticleSystemRenderer.m_Mesh kontrolü (renderMode 4 olan renderer'ların mesh'i null/çözülür olmalı). NULL bulgular kaynakla byte-diff edilerek whitelist'lenir ya da DONE_WITH_CONCERNS.
- [ ] **Step 4: Commit** — `git add Assets/Case3_Stickerdom` (folder metaları + normalizasyon): "feat: verify Case3 assets import clean" + Co-Authored-By.

---

### Task 5: Staged sahne — Stickerdom.unity (MCP)

**Files:**
- Create: `Assets/Case3_Stickerdom/Scenes/Stickerdom.unity`

**Interfaces:** Sabitler tablosu (kamera/ışık/ambient); Task 1-2 sprite'ları; Task 1 prefab'ları.

- [ ] **Step 1:** scene-create → `Assets/Case3_Stickerdom/Scenes/Stickerdom.unity` → scene-set-active (fallback: EditorSceneManager, Case 1 planındaki).
- [ ] **Step 2: Batch C#** — şablon: `docs/superpowers/plans/2026-08-14-fit-the-shape-case-assets.md` Task 6 Step 2 (Spawn helper pattern'i). Bu sahnede: kamera orthographic=true, size 13.845, pos (0,1,-10), rot identity, SolidColor bg = ambient sky rengine yakın koyu (0.212, 0.227, 0.259); 1 directional ışık (Sabitler quat, intensity 1); RenderSettings.ambientMode=Skybox yerine pratik: ambientMode=Flat, ambientLight=(0.212,0.227,0.259) — kaynakta skybox yoktu, düz renk yeterli (raporla). Kompozisyon: arkada `bg_gradient` (SpriteRenderer, ekranı kaplayacak scale), ortada `EmptyStickerSheetBackground` (sayfa), sayfa üzerinde 4 ghost sprite (sticker_hayvan_ghost, sticker_meyve_ghost, sticker_arac_ghost, sticker_doga_ghost — grid diziliminde SpriteRenderer'lar), sayfanın altında 3 sticker (sticker_hayvan, sticker_meyve, sticker_arac — hafif rastgele rotasyonla, "sökülmeye hazır"), bir kenarda DrawPileModel instance'ı. Sprite sorting order'ları: bg 0 < sayfa 10 < ghost 20 < sticker 30.
- [ ] **Step 3: Görsel iterasyon** — çöp capture + screenshot-game-view; referans: albüm sayfası + soluk slotlar + canlı sticker'lar net okunmalı; ≤3 iterasyon; screenshot'lar `<workspace>/task5-*.png`.
- [ ] **Step 4:** scene-save → commit "feat: staged Stickerdom scene (page, ghost slots, stickers)" + Co-Authored-By.

---

### Task 6: Sticker temalı VFX (MCP)

**Files:**
- Create: `Assets/Case3_Stickerdom/VFX/{SparklePop,PeelDust,AttachBurst}.prefab`

**Interfaces:** Task 2'nin PFX_StickerSoft/PFX_StickerStar mat'ları.

- [ ] **Step 1: Batch C#** — MakePS helper (`docs/superpowers/plans/2026-08-14-fit-the-shape-case-assets.md` Task 7 Step 1; MakeMat kısmını atla, mat'ları LoadAssetAtPath ile yükle), üç kurulum:
  - **SparklePop** (PFX_StickerStar): burst 12, sphere r 0.15, startSpeed 1.5-3, startSize 0.15-0.35, lifetime 0.4-0.7, gravity 0.3, rotationOverLifetime ±180°, colorOverLifetime alpha 1→0.
  - **PeelDust** (PFX_StickerSoft): burst 6, hemisphere r 0.2, startSpeed 0.3-0.8, startSize 0.3-0.6, lifetime 0.5-0.8, gravity -0.05 (hafif yukarı süzülme), colorOverLifetime alpha 0.4→0, sizeOverLifetime 0.7→1.
  - **AttachBurst** (PFX_StickerStar): tek frame'de iki burst (0s: 10, 0.05s: 6), circle r 0.1, startSpeed 2-4, startSize 0.1-0.25, lifetime 0.3-0.5, gravity 0, colorOverLifetime alpha 1→0.
  Hepsi playOnAwake false, loop false; `PrefabUtility.SaveAsPrefabAsset` → VFX/.
- [ ] **Step 2:** assets-find 3 prefab + console temiz. Commit "feat: add sticker-themed particle VFX for Case3" + Co-Authored-By.

---

### Task 7: README + final denetim

**Files:**
- Modify: `README.md`

- [ ] **Step 1: README** — klasör bloğundaki Case3 satırını güncelle:
```markdown
      Case3_Stickerdom/    → Prefabs, Sprites (sticker + ghost), Textures, VFX, Scenes (staged sahne hazır)
```
VFX/SFX notuna ekle: "Case 3'te her sticker'ın soluk `_ghost` varyantı yapışma hedefi olarak kullanılabilir."
- [ ] **Step 2: Denetim** — path-tabanlı GUID sweep (sp_guid_index üzerinden PAID prefix'ler + `_StickerPages/Scripts`) tüm Case3 dosya tiplerinde → 0; metin grep (bilinen Decoding/LodInfo false-positive'leriyle) → 0 gerçek eşleşme; twemoji izi kontrolü: `grep -ril "twemoji" Assets/Case3_Stickerdom` → 0.
- [ ] **Step 3:** MCP: assets-refresh → console Error 0.
- [ ] **Step 4: Commit** — `git add README.md` → "docs: add Case3 to README, final license audit" + Co-Authored-By.
