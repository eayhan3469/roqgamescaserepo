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
      Case1_FitTheShape/   → Models, Materials, Textures, Prefabs, VFX, Scenes (staged sahne hazır)
      Case2_BlockHole/     → Models, Materials, Textures, Prefabs (Blocks/Holes/Walls/Fractured), VFX, Scenes (staged sahne hazır)
      Case3_Stickerdom/    → Prefabs, Sprites (sticker + ghost), Textures, VFX, Scenes (staged sahne hazır)
      Case4_Buca/          → Materials, Textures, Prefabs (lane/hole/puck/green blocks + fractured), VFX, Scenes (staged sahne hazır)

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

Case 2'de blokların önceden kırılmış (pre-fractured) mesh parçaları
`Prefabs/Fractured/` altındadır — kırılma efektinizi bunlarla kurabilirsiniz.

Case 3'te her sticker'ın soluk `_ghost` varyantı yapışma hedefi olarak kullanılabilir.
