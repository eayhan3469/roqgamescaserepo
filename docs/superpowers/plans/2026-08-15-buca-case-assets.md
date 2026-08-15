# Buca Case Assets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Case 4 (Buca) assetlerini sıfırdan üret: PIL texture'lar, el-YAML URP materyaller, MCP ile primitive-kompozisyon prefab'lar (lane/delik/puck/neon yeşil bloklar + kırık varyantlar), referans kadrajlı staged sahne, VFX, README.

**Architecture:** Kaynak proje yok — üç üretim katmanı: (1) PIL → PNG + şablondan .meta (Case 3 pattern'i), (2) el yazımı .mat YAML (sabit GUID'ler, Case 2 şablonu + emissive eklentisi), (3) Unity MCP script-execute batch C# → primitive'lerden prefab kompozisyonu + sahne. Görsel referans: scratchpad'deki Buca rip atlası (SADECE bakmak için — kopya yasak).

**Tech Stack:** Unity 6000.3.11f1, URP 17.3, Python 3 + Pillow, Ivan Murzak Unity MCP (port 23435, script-execute FULL-CODE mode).

## Global Constraints

- Hedef: `/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case`, branch `case4-buca`. Her şey `Assets/Case4_Buca/` altında, TAMAMI yeni GUID'li özgün üretim.
- **Rip'ten kopya YASAK:** `<SCRATCH>/buca_rip/` yalnızca görsel referans (atlas: `ExportedProject/Assets/Texture2D/sactx-0-...spriteatlas_game-*.png` sayfa 0 ve 2'de level önizlemeleri). Tek bir dosya/byte repoya girmez.
- **rm -rf / delete-and-recreate YASAK** — yalnızca in-place yazım.
- Commit'ler path-scoped: `Assets/Case4_Buca` (+ Task 6'da README.md). `git add -A` YASAK (working tree'de kullanıcının Case1/Case2 el düzenlemeleri var).
- MCP: script-execute FULL-CODE mode; screenshot-game-view bir capture geriden gelir (önce çöp capture); ParticleSystemRenderer mesh modunda SADECE built-in Cube (`{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}` — tek submesh kuralı).
- `<SCRATCH>` = `/private/tmp/claude-501/-Users-macbookpro-Desktop-Unity-Projects-ROQ-Games-Dev-Case/9482373b-cae1-4eae-af4a-e803c84a3644/scratchpad`
- Commit mesajı sonu: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

## Sabitler (tasarım kararları — kaynak yok, bunlar otorite)

| Ne | Değer |
|---|---|
| Renkler | LaneGrad üst `#8a3ff0` → alt `#2e6bf0`; Rail beyaz `#f5f2ea`; Post kırmızı `#e8484f`/beyaz; Hole siyah `#111318`; Rim `#f5f2ea`; Puck beyaz `#f7f5ef`; PuckAccent `#f0913b`; GreenNeon base `#39e06b` + emissive `#1fe25a` (intensity ~2); GreenNeonDark `#23a34c`; kalp `#e8484f` |
| Mat GUID'leri | LaneFloor `c4b10000f100f000000000000000c001` · RailWhite `c4b10000f100f000000000000000c002` · PostStripe `c4b10000f100f000000000000000c003` · HoleBlack `c4b10000f100f000000000000000c004` · RimWhite `c4b10000f100f000000000000000c005` · PuckWhite `c4b10000f100f000000000000000c006` · PuckAccent `c4b10000f100f000000000000000c007` · GreenNeon `c4b10000f100f000000000000000c008` · GreenNeonDark `c4b10000f100f000000000000000c009` · PFX_BucaSoft `c4b10000f100f000000000000000c00a` · PFX_BucaStar `c4b10000f100f000000000000000c00b` |
| Shader GUID'leri | URP Lit `933532a4fcc9baf4fa0491de14d08ed7` · URP Unlit `650dd9526735d5b46b79224bc6e94025` · URP Particles/Unlit `0406db5a14f94604a8c57ccfbc9f3b46` |
| Lane | zemin 6 × 18 (x×z), merkez z=-4, üst y=0; rail'ler x=±3.1, r≈0.18 kapsül görünümü; post'lar (±2.6, 0.5, 3.2) |
| Hole | merkez (0, 0.012, -9), r=0.85 siyah disk + rim dış r=1.0 |
| Puck | silindir r=0.5, h=0.24, pos (0, 0.13, 2.6) |
| GreenBlock / GreenBar | küp 0.8³ / bar 2.4×0.8×0.8; sahnede bar (0, 0.4, -4), bloklar (±1.2, 0.4, -3) |
| Fractured | Block→8 parça (2×2×2, 0.4³), Bar→12 parça (3×2×2, 0.8/0.4/0.4); her parçada Rigidbody (mass 0.2) + BoxCollider |
| Kamera (başlangıç, görsel iterasyonla ayarlanır) | perspective FOV 35, pos (0, 6.8, 7.2), Euler (38, 0, 0), SolidColor bg `#1a1f2e` |
| Işık/ambient | 1 directional intensity 1.0 Euler (55, -25, 0); ambient Flat (0.45, 0.45, 0.5) |
| Sprite .meta şablonu | `Assets/Case3_Stickerdom/Sprites/Stickers/sticker_hayvan.png.meta` (repo içi; guid satırı uuid4 ile değiştirilir) |

---

### Task 1: Texture/sprite üretimi (PIL)

**Files:**
- Create: `Assets/Case4_Buca/Textures/{lane_gradient,post_stripe,heart,fx_star4,fx_softcircle}.png` (+ .meta)
- Create: `<SCRATCH>/make_buca_textures.py`

**Interfaces:**
- Produces: Task 2 materyallerinin `_BaseMap` GUID'leri (üretilen .meta'lardan okunur); Task 3'ün Hearts sprite'ı.

- [ ] **Step 1: make_buca_textures.py yaz ve çalıştır**

```python
#!/usr/bin/env python3
import os, uuid, re, math
from PIL import Image, ImageDraw, ImageFilter
DST = "/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case4_Buca/Textures"
TPL = open("/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case3_Stickerdom/Sprites/Stickers/sticker_hayvan.png.meta").read()
os.makedirs(DST, exist_ok=True)
def meta(p): open(p+".meta","w").write(re.sub(r"^guid: [0-9a-f]{32}", "guid: "+uuid.uuid4().hex, TPL, flags=re.M))
# 1) lane gradyani: dikey mor->mavi 512x512
top,bot=(138,63,240),(46,107,240)
g=Image.new("RGB",(512,512))
for y in range(512):
    t=y/511; g.paste(tuple(int(top[i]+(bot[i]-top[i])*t) for i in range(3)),(0,y,512,y+1))
p=f"{DST}/lane_gradient.png"; g.save(p); meta(p)
# 2) kirmizi-beyaz cizgili post seridi 256x64 (45 derece cizgiler)
s=Image.new("RGB",(256,64),(245,242,234)); d=ImageDraw.Draw(s)
for i in range(-64,256,64): d.polygon([(i,64),(i+32,0),(i+64,0),(i+32,64)],fill=(232,72,79))
p=f"{DST}/post_stripe.png"; s.save(p); meta(p)
# 3) kalp 128x128
h=Image.new("RGBA",(128,128),(0,0,0,0)); d=ImageDraw.Draw(h)
d.ellipse([16,24,68,76],fill=(232,72,79)); d.ellipse([60,24,112,76],fill=(232,72,79))
d.polygon([(20,58),(108,58),(64,116)],fill=(232,72,79))
p=f"{DST}/heart.png"; h.save(p); meta(p)
# 4) fx_star4 256 (4 kollu, blur)
st=Image.new("RGBA",(256,256),(255,255,255,0)); d=ImageDraw.Draw(st); c,w=128,20
d.polygon([(c,8),(c+w,c-w),(248,c),(c+w,c+w),(c,248),(c-w,c+w),(8,c),(c-w,c-w)],fill=(255,255,255,255))
st=st.filter(ImageFilter.GaussianBlur(3)); p=f"{DST}/fx_star4.png"; st.save(p); meta(p)
# 5) fx_softcircle 256 radyal
sc=Image.new("RGBA",(256,256),(255,255,255,0)); d=ImageDraw.Draw(sc)
for r in range(128,0,-1): d.ellipse([128-r,128-r,128+r,128+r],fill=(255,255,255,int(255*(1-r/128)**2)))
p=f"{DST}/fx_softcircle.png"; sc.save(p); meta(p)
print("5 texture + meta yazildi")
```

- [ ] **Step 2: Doğrula + commit**

```bash
DST=/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/Case4_Buca/Textures
ls $DST | grep -c "png$"   # beklenen 5
find $DST -name "*.png" | while read f; do [ -f "$f.meta" ] || echo "META EKSIK: $f"; done
python3 -c "
import re,glob
gs=[re.search(r'guid: ([0-9a-f]{32})',open(m).read()).group(1) for m in glob.glob('/Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case/Assets/**/*.meta',recursive=True)]
assert len(gs)==len(set(gs)),'DUPLICATE GUID'; print('guid-unique OK',len(gs))"
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case && git add Assets/Case4_Buca && git commit -m "feat: generate Buca lane/FX textures and sprites

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Materyaller (el YAML, sabit GUID'ler)

**Files:**
- Create: `Assets/Case4_Buca/Materials/*.mat` (9) + `Assets/Case4_Buca/VFX/Materials/*.mat` (2) (+ .meta'lar)
- Create: `<SCRATCH>/make_buca_mats.py`

**Interfaces:**
- Consumes: Task 1 texture GUID'leri (lane_gradient → LaneFloor, post_stripe → PostStripe, fx_* → PFX'ler).
- Produces: Task 3-5'in kullanacağı 11 materyal, Sabitler tablosundaki GUID'lerle.

- [ ] **Step 1: make_buca_mats.py yaz ve çalıştır**

Şablon `docs/superpowers/plans/2026-08-14-block-hole-case-assets.md` Task 2 Step 1'deki .mat/.meta YAML'ı. Python scripti her materyal için şablonu doldurur:

- **Lit opak** (RailWhite, RimWhite, PuckWhite, PuckAccent, GreenNeonDark): URP Lit, `_BaseColor` Sabitler'den, `_Smoothness: 0.2`, `_Metallic: 0`.
- **GreenNeon**: Lit + emissive — m_Colors'a `- _EmissionColor: {r: 0.24, g: 1.77, b: 0.7, a: 1}` (HDR ~2x), m_Floats'a `- _EmissiveExposureWeight: 0`, `m_ValidKeywords`'e `- _EMISSION`, ve `m_LightmapFlags: 2`.
- **Unlit** (LaneFloor + lane_gradient texture, PostStripe + post_stripe texture, HoleBlack düz siyah): URP Unlit, `_BaseMap` m_TexEnvs girdisi texture GUID'iyle (`{fileID: 2800000, guid: <tex>, type: 3}`), `_BaseColor` beyaz (HoleBlack'te `#111318`).
- **PFX_BucaSoft / PFX_BucaStar**: Particles/Unlit, `_Surface: 1`, `_Blend: 2`, queue 3000, `_BaseMap` fx dokuları.
Meta'lar Sabitler tablosundaki sabit GUID'lerle yazılır (`NativeFormatImporter`, `mainObjectFileID: 2100000`).

- [ ] **Step 2: Doğrula + commit**

```bash
cd /Users/macbookpro/Desktop/Unity_Projects/ROQ_Games_Dev_Case
ls Assets/Case4_Buca/Materials Assets/Case4_Buca/VFX/Materials | grep -c "mat$"   # beklenen 11
grep -l "_EMISSION" Assets/Case4_Buca/Materials/GreenNeon.mat && echo emission-ok
python3 - <<'EOF'
import re, glob
have=set()
for m in glob.glob("Assets/**/*.meta",recursive=True):
    g=re.search(r"guid: ([0-9a-f]{32})",open(m).read())
    if g: have.add(g.group(1))
bad=[]
for f in glob.glob("Assets/Case4_Buca/**/*.mat",recursive=True):
    for g in re.findall(r"guid: ([0-9a-f]{32})",open(f).read()):
        if g not in have and g not in ("933532a4fcc9baf4fa0491de14d08ed7","650dd9526735d5b46b79224bc6e94025","0406db5a14f94604a8c57ccfbc9f3b46"): bad.append((f,g))
print("REF SWEEP:","CLEAN" if not bad else bad)
EOF
git add Assets/Case4_Buca && git commit -m "feat: add hand-authored URP materials for Buca case

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: Prefab'lar (MCP batch C#)

**Files:**
- Create: `Assets/Case4_Buca/Prefabs/{Lane,Hole,Puck,Puck_Orange,GreenBlock,GreenBar,GreenBlock_Fractured,GreenBar_Fractured,Hearts}.prefab`

**Interfaces:**
- Consumes: Task 2 mat GUID'leri (LoadAssetAtPath ile path'ten yüklenir), Task 1 heart sprite'ı.
- Produces: Task 4 sahnesinin instantiate edeceği 9 prefab.

- [ ] **Step 1: MCP preflight** — script-execute FULL-CODE: `Application.dataPath` `.../ROQ_Games_Dev_Case/Assets` ile bitmeli; `assets-refresh` sonrası konsol 0 error (Task 1-2 dosyaları import olur).

- [ ] **Step 2: Batch C# — 9 prefab**

script-execute FULL-CODE mode. Tam kod:

```csharp
using UnityEngine; using UnityEditor; using System;
public static class BucaPrefabBuilder {
    static string P = "Assets/Case4_Buca/Prefabs/";
    static Material M(string n, string sub = "Materials/") => AssetDatabase.LoadAssetAtPath<Material>("Assets/Case4_Buca/" + sub + n + ".mat");
    static GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material m, Transform parent, bool keepCollider = false) {
        var go = GameObject.CreatePrimitive(t); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!keepCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static void Save(GameObject root) { PrefabUtility.SaveAsPrefabAsset(root, P + root.name + ".prefab"); UnityEngine.Object.DestroyImmediate(root); }
    public static string Build() {
        if (!AssetDatabase.IsValidFolder("Assets/Case4_Buca/Prefabs")) AssetDatabase.CreateFolder("Assets/Case4_Buca", "Prefabs");
        // Lane: zemin + 2 rail + 2 cizgili post
        var lane = new GameObject("Lane");
        var floor = Prim(PrimitiveType.Cube, "Floor", new Vector3(0, -0.05f, 0), new Vector3(6f, 0.1f, 18f), M("LaneFloor"), lane.transform, true);
        foreach (var sx in new[]{-1f, 1f}) {
            var rail = Prim(PrimitiveType.Cube, sx < 0 ? "RailL" : "RailR", new Vector3(sx * 3.1f, 0.16f, 0), new Vector3(0.2f, 0.32f, 18f), M("RailWhite"), lane.transform, true);
            var cap = Prim(PrimitiveType.Cylinder, "Cap", new Vector3(0, 0.16f, 0), new Vector3(1f, 1f, 1f), M("RailWhite"), rail.transform);
            cap.transform.localScale = new Vector3(1.4f, 0.5f, 0.018f); cap.transform.localEulerAngles = new Vector3(90, 0, 0);
            var post = Prim(PrimitiveType.Cube, "Post", new Vector3(sx * 2.6f, 0.5f, 3.2f), new Vector3(0.15f, 1f, 0.15f), M("PostStripe"), lane.transform);
        }
        Save(lane);
        // Hole: siyah disk + rim
        var hole = new GameObject("Hole");
        Prim(PrimitiveType.Cylinder, "Disc", new Vector3(0, 0.012f, 0), new Vector3(1.7f, 0.005f, 1.7f), M("HoleBlack"), hole.transform);
        Prim(PrimitiveType.Cylinder, "Rim", new Vector3(0, 0.006f, 0), new Vector3(2.0f, 0.004f, 2.0f), M("RimWhite"), hole.transform);
        hole.transform.Find("Rim").SetSiblingIndex(0);
        Save(hole);
        // Puck x2
        foreach (var v in new[]{ new{n="Puck", m="PuckWhite"}, new{n="Puck_Orange", m="PuckAccent"} }) {
            var puck = new GameObject(v.n);
            Prim(PrimitiveType.Cylinder, "Body", new Vector3(0, 0.12f, 0), new Vector3(1f, 0.12f, 1f), M(v.m), puck.transform, true);
            Save(puck);
        }
        // GreenBlock / GreenBar
        var blk = new GameObject("GreenBlock");
        Prim(PrimitiveType.Cube, "Body", new Vector3(0, 0, 0), new Vector3(0.8f, 0.8f, 0.8f), M("GreenNeon"), blk.transform, true);
        Save(blk);
        var bar = new GameObject("GreenBar");
        Prim(PrimitiveType.Cube, "Body", new Vector3(0, 0, 0), new Vector3(2.4f, 0.8f, 0.8f), M("GreenNeon"), bar.transform, true);
        Save(bar);
        // Fractured varyantlar
        Action<string,int,int,int,Vector3> frac = (name, nx, ny, nz, piece) => {
            var root = new GameObject(name);
            for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) for (int k = 0; k < nz; k++) {
                var pos = new Vector3((i - (nx-1)/2f) * piece.x, (j - (ny-1)/2f) * piece.y, (k - (nz-1)/2f) * piece.z);
                var pc = Prim(PrimitiveType.Cube, $"Piece_{i}{j}{k}", pos, piece * 0.96f, (i+j+k) % 2 == 0 ? M("GreenNeon") : M("GreenNeonDark"), root.transform, true);
                var rb = pc.AddComponent<Rigidbody>(); rb.mass = 0.2f; rb.isKinematic = true;
            }
            Save(root);
        };
        frac("GreenBlock_Fractured", 2, 2, 2, new Vector3(0.4f, 0.4f, 0.4f));
        frac("GreenBar_Fractured", 3, 2, 2, new Vector3(0.8f, 0.4f, 0.4f));
        // Hearts: 3 kalp sprite
        var hearts = new GameObject("Hearts");
        var heartSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Case4_Buca/Textures/heart.png");
        for (int i = 0; i < 3; i++) {
            var h = new GameObject("Heart" + i); h.transform.SetParent(hearts.transform, false);
            h.transform.localPosition = new Vector3((i - 1) * 0.5f, 0, 0); h.transform.localScale = Vector3.one * 0.35f;
            h.AddComponent<SpriteRenderer>().sprite = heartSprite;
        }
        Save(hearts);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        return "9 prefab olusturuldu";
    }
}
```
(Not: fractured parçalar `isKinematic = true` başlar — staged sahnede fizik oynamaz; aday tetiklerken false yapar.)

- [ ] **Step 3: Doğrula + commit** — `assets-find` 9 prefab; konsol 0 error; `git add Assets/Case4_Buca && git commit -m "feat: build Buca primitive prefabs (lane, hole, puck, green blocks, fractured)"` + Co-Authored-By satırı.

---

### Task 4: Staged sahne — Buca.unity (MCP)

**Files:**
- Create: `Assets/Case4_Buca/Scenes/Buca.unity`

**Interfaces:**
- Consumes: Task 3 prefab'ları; Sabitler'deki kamera/ışık/yerleşim değerleri; görsel referans `<SCRATCH>/buca_rip/ExportedProject/Assets/Texture2D/sactx-0-2048x2048-ETC2-spriteatlas_game-4ec380b3.png` (sol-üst iki önizleme).

- [ ] **Step 1:** scene-create → `Assets/Case4_Buca/Scenes/Buca.unity` (Scenes klasörünü önce oluştur — Case 3 dersi) → scene-set-active.
- [ ] **Step 2: Batch C#** — kamera (perspective FOV 35, pos (0, 6.8, 7.2), Euler (38,0,0), SolidColor `#1a1f2e`), 1 directional (intensity 1, Euler (55,-25,0)), ambient Flat (0.45,0.45,0.5); PrefabUtility.InstantiatePrefab ile: Lane (0,0,-4 merkezli olacak şekilde root (0,0,-4)), Hole (0,0,-9), GreenBar (0,0.4,-4), GreenBlock ×2 (±1.2,0.4,-3), GreenBlock_Fractured (2.2,0.2,-5 — kırık hali sergilenen yığın), Puck (0,0.01,2.6), Hearts (0,0.5,4.2 kameraya dönük). Şablon pattern: `docs/superpowers/plans/2026-08-14-fit-the-shape-case-assets.md` Task 6 Step 2.
- [ ] **Step 3: Görsel iterasyon ≤3** — çöp capture + screenshot; atlas önizlemeleriyle karşılaştır (lane öne uzanıyor, delik ileride net, yeşiller parlak, puck altta); screenshot'lar `<workspace>/task4-*.png`. Sonra scene-save.
- [ ] **Step 4: Commit** — `"feat: staged Buca scene (lane, hole, green blocks, puck)"` + Co-Authored-By.

---

### Task 5: VFX (MCP batch C#)

**Files:**
- Create: `Assets/Case4_Buca/VFX/{GreenShatter,HoleRing,StarTrail}.prefab`

**Interfaces:**
- Consumes: PFX_BucaSoft / PFX_BucaStar (Task 2).

- [ ] **Step 1: Batch C#** — MakePS helper (`docs/superpowers/plans/2026-08-14-fit-the-shape-case-assets.md` Task 7 Step 1; MakeMat atla, mat'ları LoadAssetAtPath):
  - **GreenShatter** (PFX_BucaSoft, ama renderMode Mesh + built-in Cube `{fileID: 10202...}` — YAML'da değil C#'ta: `psr.renderMode = ParticleSystemRenderMode.Mesh; psr.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");`): burst 14, box shape (0.8,0.8,0.8), speed 2-5, size 0.08-0.2, lifetime 0.5-0.9, gravity 1.5, startColor (0.22,0.88,0.42,1)↔(0.14,0.64,0.3,1), rotationOverLifetime ±360°.
  - **HoleRing** (PFX_BucaSoft): tek particle, startSize 0.5, sizeOverLifetime →4 (EaseInOut), lifetime 0.35, alpha 0.8→0.
  - **StarTrail** (PFX_BucaStar): rateOverTime 26 (burst yok — iz için), cone angle 6 r 0.05, speed 0.4-0.9, size 0.12-0.3, lifetime 0.5-0.9, gravity 0, alpha 0.9→0, playOnAwake false loop **true** (trail sürekli; aday puck'a parent'lar).
  GreenShatter/HoleRing: playOnAwake false, loop false.
- [ ] **Step 2: Doğrula** — assets-find 3 prefab; konsol 0 error (özellikle submesh hatası YOK — Cube tek submesh); commit `"feat: add Buca particle VFX (shatter, ring, trail)"` + Co-Authored-By.

---

### Task 6: README + final denetim

**Files:**
- Modify: `README.md`

- [ ] **Step 1: README** — klasör bloğuna:
```markdown
      Case4_Buca/          → Materials, Textures, Prefabs (lane/hole/puck/green blocks + fractured), VFX, Scenes (staged sahne hazır)
```
VFX/SFX notuna: "Case 4'te yeşil blokların önceden bölünmüş (fractured) varyantları `Prefabs/`dedir; parçalar Rigidbody'lidir (isKinematic açık başlar)."
- [ ] **Step 2: Denetim** — (a) GUID benzersizlik assert'i (tüm Assets/); (b) referential integrity: Case4 .prefab/.unity/.mat içindeki her guid repo-içi .meta'da VEYA bilinen paket/builtin GUID'lerinde (`0000000000000000e000000000000000`, `0000000000000000f000000000000000`, URP/TMP script guid'leri) çözülmeli — python sweep, 0 dangling; (c) metin grep (`toony|tcp2|JMO|epic toon|dotween|odin|rayfire|allin1|lean|layer lab|neonplay|buca_rip`) Case4 klasöründe → 0 gerçek eşleşme; (d) rip-izolasyon: `python3` ile Case4'teki her .png'nin MD5'i `<SCRATCH>/buca_rip` içindeki hiçbir dosyayla eşleşmemeli.
- [ ] **Step 3:** MCP assets-refresh → konsol 0 error.
- [ ] **Step 4: Commit** — `git add README.md` → `"docs: add Case4 to README, final audit"` + Co-Authored-By.
