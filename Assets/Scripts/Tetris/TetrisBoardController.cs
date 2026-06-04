using System;
using System.Collections.Generic;
using UnityEngine;

public class TetrisBoardController : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 20;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Transform lockedBlockRoot;
    [SerializeField] private Transform activeBlockRoot;
    [SerializeField] private GameObject blockPrefab;

    [Header("Input")]
    [SerializeField] private TetrisInputReader inputReader;

    [Header("Piece Provider")]
    [SerializeField] private MonoBehaviour pieceProviderBehaviour;

    [Header("Gameplay")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool autoSpawnAfterLock = true;
    [SerializeField] private bool allowMoveUpForDebug = true;
    [SerializeField] private bool replaceActivePieceOnDebugSpawn = true;
    [SerializeField] private float fallInterval = 0.8f;

    [Header("Colors")]
    [SerializeField] private Color colorI = Color.cyan;
    [SerializeField] private Color colorO = Color.yellow;
    [SerializeField] private Color colorT = new(0.65f, 0f, 1f);
    [SerializeField] private Color colorS = Color.green;
    [SerializeField] private Color colorZ = Color.red;
    [SerializeField] private Color colorJ = Color.blue;
    [SerializeField] private Color colorL = new(1f, 0.55f, 0f);

    public event Action<int> LinesCleared;
    public event Action GameOver;

    private Transform[,] grid;
    private IPieceProvider pieceProvider;

    private TetrominoType activeType;
    private Vector2Int activePosition;
    private Vector2Int[] activeCells;
    private readonly List<Transform> activeVisuals = new();

    private float fallTimer;
    private bool hasActivePiece;

    private void Awake()
    {
        grid = new Transform[width, height];

        pieceProvider = pieceProviderBehaviour as IPieceProvider;

        if (pieceProvider == null)
        {
            Debug.LogWarning("Piece provider is missing or does not implement IPieceProvider.");
        }

        if (lockedBlockRoot == null)
        {
            GameObject root = new GameObject("Locked Blocks");
            root.transform.SetParent(transform);
            lockedBlockRoot = root.transform;
        }

        if (activeBlockRoot == null)
        {
            GameObject root = new("Active Blocks");
            root.transform.SetParent(transform);
            activeBlockRoot = root.transform;
        }
    }

    private void OnEnable()
    {
        if (inputReader == null)
            return;

        inputReader.MovePressed += HandleMovePressed;
        inputReader.RotateClockwisePressed += HandleRotateClockwise;
        inputReader.RotateCounterClockwisePressed += HandleRotateCounterClockwise;
        inputReader.HardDropPressed += HandleHardDrop;
        inputReader.DebugSpawnPressed += HandleDebugSpawn;
    }

    private void OnDisable()
    {
        if (inputReader == null)
            return;

        inputReader.MovePressed -= HandleMovePressed;
        inputReader.RotateClockwisePressed -= HandleRotateClockwise;
        inputReader.RotateCounterClockwisePressed -= HandleRotateCounterClockwise;
        inputReader.HardDropPressed -= HandleHardDrop;
        inputReader.DebugSpawnPressed -= HandleDebugSpawn;
    }

    private void Start()
    {
        if (spawnOnStart)
            SpawnPieceFromProvider();
    }

    private void Update()
    {
        if (!hasActivePiece)
            return;

        fallTimer += Time.deltaTime;

        if (fallTimer >= fallInterval)
        {
            fallTimer = 0f;
            StepDownByGravity();
        }
    }

    public bool SpawnPieceFromProvider()
    {
        if (pieceProvider == null)
            return false;

        return SpawnPiece(pieceProvider.GetNextPieceType());
    }

    public bool SpawnPiece(TetrominoType type)
    {
        if (hasActivePiece)
            return false;

        activeType = type;
        activeCells = TetrominoShape.GetCells(type);
        activePosition = new Vector2Int(width / 2, height - 2);

        if (!IsValidPosition(activePosition, activeCells))
        {
            Debug.Log("Game Over: cannot spawn piece.");
            GameOver?.Invoke();
            return false;
        }

        CreateActiveVisuals();
        RefreshActiveVisuals();

        hasActivePiece = true;
        fallTimer = 0f;

        return true;
    }

    public void ForceDebugSpawn()
    {
        if (hasActivePiece && replaceActivePieceOnDebugSpawn)
        {
            ClearActiveVisuals();
            hasActivePiece = false;
        }

        SpawnPieceFromProvider();
    }

    private void HandleMovePressed(Vector2Int direction)
    {
        if (!hasActivePiece)
            return;

        if (direction == Vector2Int.up && !allowMoveUpForDebug)
            return;

        TryMove(direction);
    }

    private void HandleRotateClockwise()
    {
        TryRotate(clockwise: true);
    }

    private void HandleRotateCounterClockwise()
    {
        TryRotate(clockwise: false);
    }

    private void HandleHardDrop()
    {
        if (!hasActivePiece)
            return;

        while (TryMove(Vector2Int.down))
        {
        }

        LockActivePiece();
    }

    private void HandleDebugSpawn()
    {
        ForceDebugSpawn();
    }

    private void StepDownByGravity()
    {
        if (!TryMove(Vector2Int.down))
            LockActivePiece();
    }

    private bool TryMove(Vector2Int direction)
    {
        Vector2Int nextPosition = activePosition + direction;

        if (!IsValidPosition(nextPosition, activeCells))
            return false;

        activePosition = nextPosition;
        RefreshActiveVisuals();

        return true;
    }

    private void TryRotate(bool clockwise)
    {
        if (!hasActivePiece)
            return;

        if (!TetrominoShape.CanRotate(activeType))
            return;

        Vector2Int[] rotated = new Vector2Int[activeCells.Length];

        for (int i = 0; i < activeCells.Length; i++)
        {
            rotated[i] = TetrominoShape.RotateCell(activeCells[i], clockwise);
        }

        if (!IsValidPosition(activePosition, rotated))
            return;

        activeCells = rotated;
        RefreshActiveVisuals();
    }

    private bool IsValidPosition(Vector2Int position, Vector2Int[] cells)
    {
        foreach (Vector2Int cell in cells)
        {
            Vector2Int boardCell = position + cell;

            if (boardCell.x < 0 || boardCell.x >= width)
                return false;

            if (boardCell.y < 0 || boardCell.y >= height)
                return false;

            if (grid[boardCell.x, boardCell.y] != null)
                return false;
        }

        return true;
    }

    private void LockActivePiece()
    {
        foreach (Transform visual in activeVisuals)
        {
            Vector2Int cell = WorldToCell(visual.position);

            if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height)
                continue;

            visual.SetParent(lockedBlockRoot);
            grid[cell.x, cell.y] = visual;
        }

        activeVisuals.Clear();
        hasActivePiece = false;

        int cleared = ClearFullLines();

        if (cleared > 0)
            LinesCleared?.Invoke(cleared);

        if (IsTouchingTop())
        {
            Debug.Log("Game Over: blocks touched the top.");
            GameOver?.Invoke();
            return;
        }

        if (autoSpawnAfterLock)
            SpawnPieceFromProvider();
    }

    private int ClearFullLines()
    {
        int cleared = 0;

        for (int y = 0; y < height; y++)
        {
            if (!IsLineFull(y))
                continue;

            ClearLine(y);
            ShiftRowsDown(y + 1);
            cleared++;

            y--;
        }

        if (cleared > 0)
            Debug.Log($"Cleared lines: {cleared}");

        return cleared;
    }

    private bool IsLineFull(int y)
    {
        for (int x = 0; x < width; x++)
        {
            if (grid[x, y] == null)
                return false;
        }

        return true;
    }

    private void ClearLine(int y)
    {
        for (int x = 0; x < width; x++)
        {
            if (grid[x, y] != null)
            {
                Destroy(grid[x, y].gameObject);
                grid[x, y] = null;
            }
        }
    }

    private void ShiftRowsDown(int startY)
    {
        for (int y = startY; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Transform block = grid[x, y];

                if (block == null)
                    continue;

                grid[x, y - 1] = block;
                grid[x, y] = null;

                block.position = CellToWorld(new Vector2Int(x, y - 1));
            }
        }
    }

    private bool IsTouchingTop()
    {
        int topY = height - 1;

        for (int x = 0; x < width; x++)
        {
            if (grid[x, topY] != null)
                return true;
        }

        return false;
    }

    private void CreateActiveVisuals()
    {
        ClearActiveVisuals();

        Color color = GetColor(activeType);

        for (int i = 0; i < activeCells.Length; i++)
        {
            GameObject block = Instantiate(blockPrefab, activeBlockRoot);
            block.name = $"Active_{activeType}_{i}";

            SpriteRenderer spriteRenderer = block.GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
                spriteRenderer.color = color;

            activeVisuals.Add(block.transform);
        }
    }

    private void RefreshActiveVisuals()
    {
        for (int i = 0; i < activeCells.Length; i++)
        {
            Vector2Int boardCell = activePosition + activeCells[i];
            activeVisuals[i].position = CellToWorld(boardCell);
        }
    }

    private void ClearActiveVisuals()
    {
        for (int i = activeVisuals.Count - 1; i >= 0; i--)
        {
            if (activeVisuals[i] != null)
                Destroy(activeVisuals[i].gameObject);
        }

        activeVisuals.Clear();
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        return transform.position + new Vector3(cell.x * cellSize, cell.y * cellSize, 0f);
    }

    private Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Vector3 local = worldPosition - transform.position;

        int x = Mathf.RoundToInt(local.x / cellSize);
        int y = Mathf.RoundToInt(local.y / cellSize);

        return new Vector2Int(x, y);
    }

    private Color GetColor(TetrominoType type)
    {
        switch (type)
        {
            case TetrominoType.I:
                return colorI;
            case TetrominoType.O:
                return colorO;
            case TetrominoType.T:
                return colorT;
            case TetrominoType.S:
                return colorS;
            case TetrominoType.Z:
                return colorZ;
            case TetrominoType.J:
                return colorJ;
            case TetrominoType.L:
                return colorL;
            default:
                return Color.white;
        }
    }
}