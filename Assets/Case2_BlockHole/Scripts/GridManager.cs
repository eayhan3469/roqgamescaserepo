using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace BlockHole
{
    public enum GridCellType
    {
        Empty = 0,
        BlockedByWall = 1,
        OccupiedByBlock = 2,
        Hole = 3
    }

    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [Header("Grid Configuration")]
        [SerializeField] private int width = 7;
        [SerializeField] private int height = 8;
        [SerializeField] private float tileSize = 1.0f;
        [SerializeField] private Vector3 originWorldPosition = new Vector3(0.5f, 0.03f, 1.0f);

        [Header("Scene References")]
        [SerializeField] private Transform floorRoot;
        [SerializeField] private Transform holesRoot;
        [SerializeField] private Transform blocksRoot;

        [Header("Floor Tile Prefabs & Materials for Hole Closing")]
        [SerializeField] private Material floorLightMaterial;
        [SerializeField] private Material floorDarkMaterial;

        [Header("Endless Game Loop Timings")]
        [Tooltip("Delay after last block enters hole before celebration starts (waits for final hole to seal).")]
        [SerializeField] private float victoryDelay = 0.90f;

        [Tooltip("Delay after victory fanfare before holes start reopening.")]
        [SerializeField] private float respawnStartDelay = 1.6f;

        [Tooltip("Stagger interval between each reopening floor tile (pıt-pıt-pıt rhythm).")]
        [SerializeField] private float tilePopInterval = 0.065f;

        [Tooltip("Stagger interval between each respawning block dropping from above.")]
        [SerializeField] private float blockSpawnInterval = 0.14f;

        private GridCellType[,] gridMap;
        private BlockDraggable[,] blockOccupancyMap;
        private List<BlockDraggable> activeBlocks = new List<BlockDraggable>();
        private List<HoleTarget> allHoles = new List<HoleTarget>();
        private HashSet<Vector2Int> initialHoleCells = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> currentHoleCells = new HashSet<Vector2Int>();
        private List<GameObject> blockTemplates = new List<GameObject>();
        private List<Vector2Int> blockInitialAnchors = new List<Vector2Int>();
        private List<GameObject> closedTileInstances = new List<GameObject>();
        private ParticleSystem leftConfettiCannon;
        private ParticleSystem rightConfettiCannon;
        private bool isInitialized = false;
        private bool isRespawning = false;

        public int Width => width;
        public int Height => height;
        public float TileSize => tileSize;
        public Vector3 OriginWorldPosition => originWorldPosition;
        public Transform FloorRoot => floorRoot;
        public List<BlockDraggable> AllBlocks => activeBlocks;
        public List<HoleTarget> AllHoles => allHoles;

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

            InitializePostProcessing();
            InitializeAudioManager();
            InitializeGrid();
        }

        private void InitializePostProcessing()
        {
            if (FindFirstObjectByType<BlockHolePostProcessing>() == null)
            {
                GameObject ppGo = new GameObject("Global Post Processing (Bloom & Vibrancy)");
                ppGo.AddComponent<BlockHolePostProcessing>();
            }
        }

        private void InitializeAudioManager()
        {
            if (FindFirstObjectByType<BlockHoleAudioManager>() == null)
            {
                GameObject audioGo = new GameObject("AudioManager");
                audioGo.AddComponent<BlockHoleAudioManager>();
            }
        }

        public void InitializeGrid()
        {
            if (isInitialized) return;

            if (floorRoot == null)
            {
                floorRoot = GameObject.Find("Floor")?.transform ?? GameObject.Find("Board/Floor")?.transform;
            }
            if (holesRoot == null)
            {
                holesRoot = GameObject.Find("Holes")?.transform ?? GameObject.Find("Board/Holes")?.transform;
            }
            if (blocksRoot == null)
            {
                blocksRoot = GameObject.Find("Blocks")?.transform ?? GameObject.Find("Board/Blocks")?.transform;
            }

            if (floorLightMaterial == null && floorRoot != null)
            {
                var t00 = floorRoot.Find("Tile_0_0");
                if (t00 != null) floorLightMaterial = t00.GetComponent<MeshRenderer>()?.sharedMaterial;
            }
            if (floorDarkMaterial == null && floorRoot != null)
            {
                var t10 = floorRoot.Find("Tile_1_0");
                if (t10 != null) floorDarkMaterial = t10.GetComponent<MeshRenderer>()?.sharedMaterial;
            }

            gridMap = new GridCellType[width, height];
            blockOccupancyMap = new BlockDraggable[width, height];
            initialHoleCells.Clear();
            currentHoleCells.Clear();

            // 1. Determine Floor & Holes based on floor tiles
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Transform tile = floorRoot != null ? floorRoot.Find($"Tile_{x}_{y}") : null;
                    if (tile != null && tile.gameObject.activeSelf)
                    {
                        gridMap[x, y] = GridCellType.Empty;
                    }
                    else
                    {
                        gridMap[x, y] = GridCellType.Hole;
                        initialHoleCells.Add(new Vector2Int(x, y));
                        currentHoleCells.Add(new Vector2Int(x, y));
                    }
                }
            }

            // 2. Discover all HoleTarget components
            allHoles.Clear();
            if (holesRoot != null)
            {
                foreach (Transform child in holesRoot)
                {
                    HoleTarget hole = child.GetComponent<HoleTarget>();
                    if (hole != null)
                    {
                        allHoles.Add(hole);
                        hole.InitializeVisuals();
                    }
                }
            }

            // 3. Discover and register all BlockDraggable instances & create respawn templates
            activeBlocks.Clear();
            blockTemplates.Clear();
            blockInitialAnchors.Clear();

            if (blocksRoot != null)
            {
                List<Transform> initialBlockTransforms = new List<Transform>();
                foreach (Transform child in blocksRoot)
                {
                    if (child.name.EndsWith("_Template")) continue;
                    initialBlockTransforms.Add(child);
                }

                foreach (Transform child in initialBlockTransforms)
                {
                    BlockDraggable block = child.GetComponent<BlockDraggable>();
                    if (block != null)
                    {
                        activeBlocks.Add(block);
                        block.Initialize();
                        RegisterBlock(block, block.CurrentAnchorGridPos);

                        // Create inactive clone as pristine template for endless respawn
                        GameObject template = Instantiate(child.gameObject, blocksRoot);
                        template.name = child.name + "_Template";
                        template.SetActive(false);
                        blockTemplates.Add(template);
                        blockInitialAnchors.Add(block.CurrentAnchorGridPos);
                    }
                }
            }

            isInitialized = true;
        }

        public Vector3 GridToWorldPosition(Vector2Int gridPos)
        {
            return originWorldPosition + new Vector3(gridPos.x * tileSize, 0f, gridPos.y * tileSize);
        }

        public Vector2Int WorldToGridPosition(Vector3 worldPos)
        {
            int gx = Mathf.RoundToInt((worldPos.x - originWorldPosition.x) / tileSize);
            int gy = Mathf.RoundToInt((worldPos.z - originWorldPosition.z) / tileSize);
            return new Vector2Int(gx, gy);
        }

        public bool IsInBounds(Vector2Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < width && gridPos.y >= 0 && gridPos.y < height;
        }

        public bool IsHoleCell(Vector2Int gridPos)
        {
            return currentHoleCells.Contains(gridPos);
        }

        public GridCellType GetCellType(Vector2Int gridPos)
        {
            if (!IsInBounds(gridPos)) return GridCellType.BlockedByWall;
            return gridMap[gridPos.x, gridPos.y];
        }

        public HoleTarget GetMatchingHoleForBlock(BlockDraggable block)
        {
            if (block == null) return null;

            for (int i = 0; i < allHoles.Count; i++)
            {
                HoleTarget hole = allHoles[i];
                if (hole != null && !hole.IsFilled && hole.Matches(block))
                {
                    return hole;
                }
            }

            return null;
        }

        public bool CanShapeOccupy(List<Vector2Int> gridPositions, BlockDraggable block, bool ignoreHoles = false)
        {
            if (gridPositions == null || gridPositions.Count == 0) return false;

            for (int i = 0; i < gridPositions.Count; i++)
            {
                Vector2Int pos = gridPositions[i];
                if (!IsInBounds(pos)) return false;

                GridCellType type = gridMap[pos.x, pos.y];

                if (type == GridCellType.BlockedByWall)
                {
                    return false;
                }

                if (type == GridCellType.Hole && !ignoreHoles)
                {
                    return false;
                }

                if (type == GridCellType.OccupiedByBlock)
                {
                    BlockDraggable occupant = blockOccupancyMap[pos.x, pos.y];
                    if (occupant != null && occupant != block)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public Vector2Int FindNearestValidAnchor(BlockDraggable block, Vector2Int desiredAnchor)
        {
            if (CanShapeOccupy(block.GetWorldFootprint(desiredAnchor), block, false))
            {
                return desiredAnchor;
            }

            for (int r = 1; r < Mathf.Max(width, height); r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        Vector2Int candidate = new Vector2Int(desiredAnchor.x + dx, desiredAnchor.y + dy);
                        if (CanShapeOccupy(block.GetWorldFootprint(candidate), block, false))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return desiredAnchor;
        }

        public void RegisterBlock(BlockDraggable block, Vector2Int anchorPos)
        {
            if (block == null) return;
            List<Vector2Int> occupied = block.GetWorldFootprint(anchorPos);

            for (int i = 0; i < occupied.Count; i++)
            {
                Vector2Int p = occupied[i];
                if (IsInBounds(p))
                {
                    gridMap[p.x, p.y] = GridCellType.OccupiedByBlock;
                    blockOccupancyMap[p.x, p.y] = block;
                }
            }
        }

        public void UnregisterBlock(BlockDraggable block)
        {
            if (block == null) return;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (blockOccupancyMap[x, y] == block)
                    {
                        blockOccupancyMap[x, y] = null;
                        gridMap[x, y] = currentHoleCells.Contains(new Vector2Int(x, y)) ? GridCellType.Hole : GridCellType.Empty;
                    }
                }
            }
        }

        public void UpdateBlockPosition(BlockDraggable block, Vector2Int newAnchorPos)
        {
            UnregisterBlock(block);
            RegisterBlock(block, newAnchorPos);
        }

        public void OnBlockSwallowed(BlockDraggable block)
        {
            if (block != null && activeBlocks.Contains(block))
            {
                activeBlocks.Remove(block);
            }

            CheckLevelCompletion();
        }

        public void CheckLevelCompletion()
        {
            if (isRespawning) return;

            // Remove any destroyed/null blocks from list
            activeBlocks.RemoveAll(b => b == null || b.IsDroppedInHole);

            if (activeBlocks.Count == 0)
            {
                isRespawning = true;
                StartCoroutine(VictoryAndRespawnRoutine());
            }
        }

        private IEnumerator VictoryAndRespawnRoutine()
        {
            yield return new WaitForSeconds(victoryDelay);

            // 🌟 1. AŞAMA: ZAFER KUTLAMASI (Victory Fanfare + Dual Confetti Cannons + Grid Ripple Wave)
            if (BlockHoleAudioManager.Instance != null)
            {
                BlockHoleAudioManager.Instance.PlayVictorySound();
            }

            // A) Çift Köşe Zafer Konfetisi Fırlat
            SpawnDualVictoryCannons();

            // B) Zemin Karolarında Dairesel Su Dalgası (Concentric Floor Ripple Wave)
            TriggerGridRippleWave();

            // C) Kamera Kutlama Mikro-Vuruşu
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.DOPunchPosition(new Vector3(0f, 0.18f, -0.06f), 0.50f, 6, 0.4f);
            }

            yield return new WaitForSeconds(respawnStartDelay);

            // 🌟 2. AŞAMA: "PIT PIT PIT" DELİKLERİN GERİ AÇILMASI (Staggered Floor Tile Sinking)
            // Kapanmış tüm yer karolarını pıt-pıt-pıt ritmiyle aşağı indir ve gizle
            for (int i = 0; i < closedTileInstances.Count; i++)
            {
                GameObject tile = closedTileInstances[i];
                if (tile != null && tile.activeSelf)
                {
                    float pitch = 0.90f + (i % 8) * 0.04f;
                    if (BlockHoleAudioManager.Instance != null)
                    {
                        BlockHoleAudioManager.Instance.PlayTilePopSound(pitch);
                    }

                    tile.transform.DOMoveY(-1.2f, 0.20f).SetEase(Ease.InBack).OnComplete(() =>
                    {
                        tile.SetActive(false);
                    });

                    yield return new WaitForSeconds(tilePopInterval);
                }
            }
            closedTileInstances.Clear();

            // Orijinal delik hücrelerini tekrar matrise işlet
            currentHoleCells.Clear();
            foreach (var cell in initialHoleCells)
            {
                currentHoleCells.Add(cell);
                gridMap[cell.x, cell.y] = GridCellType.Hole;
            }

            // Delik objelerini ("HoleTarget") tatlı bir yaylanma (Pop-Scale) ile geri aç
            foreach (HoleTarget hole in allHoles)
            {
                if (hole != null)
                {
                    hole.ResetHole();
                }
            }

            yield return new WaitForSeconds(0.35f);

            // 🌟 3. AŞAMA: ŞEKİLLERİN GÜZEL BİR ŞEKİLDE YAYLANARAK SPAWN OLMASI (Juicy Bouncy Spawn)
            for (int i = 0; i < blockTemplates.Count; i++)
            {
                GameObject template = blockTemplates[i];
                Vector2Int anchor = blockInitialAnchors[i];

                if (template != null)
                {
                    GameObject newBlockGo = Instantiate(template, blocksRoot);
                    newBlockGo.name = template.name.Replace("_Template", "");
                    newBlockGo.SetActive(true);

                    BlockDraggable newBlock = newBlockGo.GetComponent<BlockDraggable>();
                    if (newBlock != null)
                    {
                        newBlock.CurrentAnchorGridPos = anchor;
                        newBlock.Initialize();

                        Vector3 targetWorldPos = newBlockGo.transform.position;
                        Vector3 spawnWorldPos = targetWorldPos + Vector3.up * 4.0f;
                        Vector3 originalScale = newBlockGo.transform.localScale;

                        newBlockGo.transform.position = spawnWorldPos;
                        newBlockGo.transform.localScale = Vector3.zero;

                        // Havadan sekerek düşüş ve ezilip-yaylanma (Squash & Stretch)
                        newBlockGo.transform.DOMove(targetWorldPos, 0.42f).SetEase(Ease.OutBounce).OnComplete(() =>
                        {
                            if (BlockHoleAudioManager.Instance != null)
                            {
                                BlockHoleAudioManager.Instance.PlaySnapBackSound();
                            }
                            newBlockGo.transform.DOPunchScale(new Vector3(0.12f, -0.16f, 0.12f), 0.22f, 6, 0.5f);
                        });

                        newBlockGo.transform.DOScale(originalScale, 0.35f).SetEase(Ease.OutBack, 1.4f);

                        RegisterBlock(newBlock, anchor);
                        activeBlocks.Add(newBlock);
                    }

                    yield return new WaitForSeconds(blockSpawnInterval);
                }
            }

            isRespawning = false;
        }

        /// <summary>
        /// Sol alt köşeden (0,0) başlayıp sağ üst köşeye (6,7) doğru stadyum dalgası gibi süpüren ultra belirgin zemin yaylanması.
        /// </summary>
        public void TriggerGridRippleWave()
        {
            if (floorRoot == null) return;

            HashSet<int> playedSteps = new HashSet<int>();

            foreach (Transform tile in floorRoot)
            {
                if (tile == null || !tile.gameObject.activeSelf) continue;

                Vector2 tileCoord = new Vector2(
                    Mathf.Round((tile.position.x - originWorldPosition.x) / tileSize),
                    Mathf.Round((tile.position.z - originWorldPosition.z) / tileSize)
                );

                // Sol alt (0,0) -> Sağ üst (width, height) çapraz adım mesafesi
                int diagonalStep = Mathf.RoundToInt(Mathf.Max(0f, tileCoord.x + tileCoord.y));
                float delay = diagonalStep * 0.065f;

                tile.DOKill();
                tile.localPosition = new Vector3(tile.localPosition.x, 0.0f, tile.localPosition.z);

                Sequence tileSeq = DOTween.Sequence();
                tileSeq.SetTarget(tile);
                tileSeq.SetDelay(delay);

                // Dalga sırası her çapraz adıma ulaştığında tatlı tırmanan bir tıkırtı sesi
                if (!playedSteps.Contains(diagonalStep))
                {
                    playedSteps.Add(diagonalStep);
                    int stepIndex = diagonalStep;
                    tileSeq.AppendCallback(() =>
                    {
                        if (BlockHoleAudioManager.Instance != null)
                        {
                            BlockHoleAudioManager.Instance.PlayTilePopSound(0.82f + (stepIndex % 10) * 0.045f);
                        }
                    });
                }

                // Ultra belirgin 75 cm tepe yüksekliği ve güçlü geri yaylanma
                tileSeq.Append(tile.DOLocalMoveY(0.75f, 0.16f).SetEase(Ease.OutQuad));
                tileSeq.Join(tile.DOPunchScale(new Vector3(0.08f, 0.22f, 0.08f), 0.38f, 4, 0.5f));
                tileSeq.Append(tile.DOLocalMoveY(0.0f, 0.28f).SetEase(Ease.OutBack, 1.85f));
            }
        }

        /// <summary>
        /// Ekranın sol ve sağ alt köşelerinden sahneye doğru fışkıran çok renkli şenlik konfeti topları.
        /// </summary>
        public void SpawnDualVictoryCannons()
        {
            Camera cam = Camera.main;
            Vector3 centerBoardPos = originWorldPosition + new Vector3(width * tileSize * 0.5f, 0.2f, height * tileSize * 0.5f);

            Vector3 leftCorner = originWorldPosition + new Vector3(-1.8f, 0.1f, -0.8f);
            Vector3 rightCorner = originWorldPosition + new Vector3(width * tileSize + 1.8f, 0.1f, -0.8f);

            Vector3 leftAim = (centerBoardPos - leftCorner).normalized + Vector3.up * 1.3f;
            Vector3 rightAim = (centerBoardPos - rightCorner).normalized + Vector3.up * 1.3f;

            if (leftConfettiCannon == null)
            {
                leftConfettiCannon = CreateSingleCannonSystem("LeftConfettiCannon");
            }
            if (rightConfettiCannon == null)
            {
                rightConfettiCannon = CreateSingleCannonSystem("RightConfettiCannon");
            }

            if (leftConfettiCannon != null)
            {
                leftConfettiCannon.transform.position = leftCorner;
                leftConfettiCannon.transform.rotation = Quaternion.LookRotation(leftAim);
                leftConfettiCannon.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                leftConfettiCannon.gameObject.SetActive(true);
                leftConfettiCannon.Play(true);
            }

            if (rightConfettiCannon != null)
            {
                rightConfettiCannon.transform.position = rightCorner;
                rightConfettiCannon.transform.rotation = Quaternion.LookRotation(rightAim);
                rightConfettiCannon.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                rightConfettiCannon.gameObject.SetActive(true);
                rightConfettiCannon.Play(true);
            }
        }

        private ParticleSystem CreateSingleCannonSystem(string cannonName)
        {
            GameObject root = new GameObject("VFX_" + cannonName);
            root.transform.SetParent(transform);

            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 2.0f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10.0f, 16.0f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.24f, 0.44f);
            main.startSizeZ = 0.04f;
            main.gravityModifier = 0.85f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var startColor = main.startColor;
            startColor.mode = ParticleSystemGradientMode.RandomColor;
            Gradient festiveGrad = new Gradient();
            festiveGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1.0f, 0.85f, 0.1f), 0.0f),  // Gold
                    new GradientColorKey(new Color(1.0f, 0.2f, 0.6f), 0.25f),  // Hot Pink
                    new GradientColorKey(new Color(0.1f, 0.9f, 1.0f), 0.50f),  // Cyan
                    new GradientColorKey(new Color(0.3f, 1.0f, 0.4f), 0.75f),  // Lime
                    new GradientColorKey(new Color(1.0f, 0.5f, 0.1f), 1.0f)   // Orange
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0f), new GradientAlphaKey(1.0f, 1f) }
            );
            startColor.gradient = festiveGrad;
            main.startColor = startColor;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, 85),
                new ParticleSystem.Burst(0.15f, 55)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18.0f;
            shape.radius = 0.2f;

            var rotBySpeed = ps.rotationBySpeed;
            rotBySpeed.enabled = true;
            rotBySpeed.x = new ParticleSystem.MinMaxCurve(3.0f, 7.0f);
            rotBySpeed.y = new ParticleSystem.MinMaxCurve(3.0f, 7.0f);
            rotBySpeed.z = new ParticleSystem.MinMaxCurve(3.0f, 7.0f);

            var psRenderer = root.GetComponent<ParticleSystemRenderer>();
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Mobile/Particles/Additive"));

            return ps;
        }

        /// <summary>
        /// Instantiates and pops up a solid floor tile to close a hole cell with a punchy bounce.
        /// </summary>
        public void CloseHoleCell(Vector2Int cell)
        {
            if (!IsInBounds(cell)) return;

            currentHoleCells.Remove(cell);
            gridMap[cell.x, cell.y] = GridCellType.Empty;

            if (floorRoot == null) return;

            string tileName = $"Tile_{cell.x}_{cell.y}";
            Transform existingTile = floorRoot.Find(tileName);
            GameObject tileGo;

            if (existingTile != null)
            {
                tileGo = existingTile.gameObject;
                tileGo.SetActive(true);
            }
            else
            {
                tileGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tileGo.name = tileName;
                tileGo.transform.SetParent(floorRoot, false);
                tileGo.transform.localScale = new Vector3(1.0f, 0.06f, 1.0f);

                Material mat = ((cell.x + cell.y) % 2 == 0) ? floorLightMaterial : floorDarkMaterial;
                if (mat != null)
                {
                    tileGo.GetComponent<MeshRenderer>().sharedMaterial = mat;
                }
            }

            if (!closedTileInstances.Contains(tileGo))
            {
                closedTileInstances.Add(tileGo);
            }

            Vector3 finalWorldPos = new Vector3(originWorldPosition.x + cell.x * tileSize, 0.0f, originWorldPosition.z + cell.y * tileSize);
            Vector3 spawnWorldPos = finalWorldPos + Vector3.down * 1.2f;

            tileGo.transform.position = spawnWorldPos;
            tileGo.transform.DOKill();
            tileGo.transform.DOMoveY(0.0f, 0.26f).SetEase(Ease.OutBack, 1.45f);
        }
    }
}
