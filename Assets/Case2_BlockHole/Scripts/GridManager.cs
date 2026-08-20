using System;
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

        private GridCellType[,] gridMap;
        private BlockDraggable[,] blockOccupancyMap;
        private List<BlockDraggable> allBlocks = new List<BlockDraggable>();
        private List<HoleTarget> allHoles = new List<HoleTarget>();
        private HashSet<Vector2Int> holeCells = new HashSet<Vector2Int>();
        private bool isInitialized = false;

        public int Width => width;
        public int Height => height;
        public float TileSize => tileSize;
        public Vector3 OriginWorldPosition => originWorldPosition;
        public Transform FloorRoot => floorRoot;
        public List<BlockDraggable> AllBlocks => allBlocks;
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
            if (FindObjectOfType<BlockHolePostProcessing>() == null)
            {
                GameObject ppGo = new GameObject("Global Post Processing (Bloom & Vibrancy)");
                ppGo.AddComponent<BlockHolePostProcessing>();
            }
        }

        private void InitializeAudioManager()
        {
            if (FindObjectOfType<BlockHoleAudioManager>() == null)
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
            holeCells.Clear();

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
                        holeCells.Add(new Vector2Int(x, y));
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

            // 3. Discover and register all BlockDraggable instances
            allBlocks.Clear();
            if (blocksRoot != null)
            {
                foreach (Transform child in blocksRoot)
                {
                    BlockDraggable block = child.GetComponent<BlockDraggable>();
                    if (block != null)
                    {
                        allBlocks.Add(block);
                        block.Initialize();
                        RegisterBlock(block, block.CurrentAnchorGridPos);
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
            return holeCells.Contains(gridPos);
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
                        gridMap[x, y] = holeCells.Contains(new Vector2Int(x, y)) ? GridCellType.Hole : GridCellType.Empty;
                    }
                }
            }
        }

        public void UpdateBlockPosition(BlockDraggable block, Vector2Int newAnchorPos)
        {
            UnregisterBlock(block);
            RegisterBlock(block, newAnchorPos);
        }

        /// <summary>
        /// Instantiates and pops up a solid floor tile to close a hole cell with a punchy bounce.
        /// </summary>
        public void CloseHoleCell(Vector2Int cell)
        {
            if (!IsInBounds(cell)) return;

            holeCells.Remove(cell);
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

            Vector3 finalWorldPos = new Vector3(originWorldPosition.x + cell.x * tileSize, 0.0f, originWorldPosition.z + cell.y * tileSize);
            Vector3 spawnWorldPos = finalWorldPos + Vector3.down * 1.2f;

            tileGo.transform.position = spawnWorldPos;
            tileGo.transform.DOKill();
            tileGo.transform.DOMoveY(0.0f, 0.26f).SetEase(Ease.OutBack, 1.45f);
        }
    }
}
