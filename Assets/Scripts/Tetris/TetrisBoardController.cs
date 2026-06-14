using System;
using System.Collections.Generic;
using UnityEngine;

public class TetrisBoardController : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 20;
    [SerializeField] private float cellSize = 0.8f;
    [SerializeField] private Transform lockedBlockRoot;
    [SerializeField] private Transform activeBlockRoot;
    [SerializeField] private Transform borderBlockRoot;
    [SerializeField] private GameObject blockPrefab;
    [SerializeField] private bool generateBorderBlocks = true;

    [Header("Input")]
    [SerializeField] private TetrisInputReader inputReader;

    [Header("Piece Provider")]
    [SerializeField] private MonoBehaviour pieceProviderBehaviour;

    [Header("Gameplay")]
    [SerializeField] private bool spawnOnStart;
    [SerializeField] private bool autoSpawnAfterLock = true;
    [SerializeField] private bool allowMoveUpForDebug;
    [SerializeField] private bool enableDebugPieceSpawn;
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
    [SerializeField] private Color borderBlockColor = Color.white;
    [SerializeField] private Color garbageBlockColor = new(0.45f, 0.45f, 0.45f, 1f);
    // gap around each cell so the black bg works as grid lines
    [SerializeField] private float cellVisualInset = 0.08f;
    [SerializeField] private Color corruptedTint = new(0.55f, 0.3f, 0.7f, 1f);
    [SerializeField] private Color glitchedTint = new(0.5f, 0.95f, 1f, 1f);

    public event Action PieceLocked;
    public event Action<int> LinesCleared;
    public event Action RoundPiecesExhausted;
    public event Action GameOver;
    public event Action BoardChanged;

    public bool IsBoardActive { get; private set; }
    public bool HasActivePiece => hasActivePiece;
    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    public Vector3 BoardWorldMin => transform.position + new Vector3(-0.5f * cellSize, -0.5f * cellSize, 0f);
    public Vector3 BoardWorldMax => transform.position + new Vector3((width - 0.5f) * cellSize, (height - 0.5f) * cellSize, 0f);

    private Transform[,] grid;
    private IPieceProvider pieceProvider;

    private TetrominoType activeType;
    private Vector2Int activePosition;
    private Vector2Int[] activeCells;
    private readonly List<Transform> activeVisuals = new();

    private float fallTimer;
    private bool hasActivePiece;
    private bool gameOverRaised;
    private PieceTag activePieceTags;
    private float corruptedPieceChance;
    private float glitchedPieceChance;
    private float fallSpeedMultiplier = 1f;
    // boss skill: while this clock runs, rotation input is ignored
    private float rotationLockedUntil = -1f;
    private readonly HashSet<Transform> corruptedBlocks = new();
    private readonly Dictionary<TetrominoType, int> lockedTypeCounts = new();
    private MaterialPropertyBlock blockPropertyBlock;
    private static Sprite solidBorderSprite;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        grid = new Transform[width, height];
        pieceProvider = pieceProviderBehaviour as IPieceProvider;
        blockPropertyBlock = new MaterialPropertyBlock();

        if (pieceProvider == null)
            Debug.LogWarning("Piece provider is missing or does not implement IPieceProvider.");

        if (lockedBlockRoot == null)
        {
            GameObject root = new("Locked Blocks");
            root.transform.SetParent(transform);
            lockedBlockRoot = root.transform;
        }

        if (activeBlockRoot == null)
        {
            GameObject root = new("Active Blocks");
            root.transform.SetParent(transform);
            activeBlockRoot = root.transform;
        }

        if (borderBlockRoot == null)
        {
            GameObject root = new("Border Blocks");
            root.transform.SetParent(transform);
            borderBlockRoot = root.transform;
        }

        RebuildBorderBlocks();
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
        if (!IsBoardActive || !hasActivePiece)
            return;

        fallTimer += Time.deltaTime;

        if (fallTimer >= fallInterval / Mathf.Max(0.25f, fallSpeedMultiplier))
        {
            fallTimer = 0f;
            StepDownByGravity();
        }
    }

    public void SetBoardActive(bool active)
    {
        IsBoardActive = active;

        if (!active)
            fallTimer = 0f;
    }

    public bool SpawnPieceFromProvider()
    {
        return TrySpawnPieceFromProvider(out _);
    }

    public bool TrySpawnPieceFromProvider(out bool providerExhausted)
    {
        // Callers need to distinguish an empty round from a blocked spawn
        providerExhausted = false;

        if (pieceProvider == null)
            return false;

        if (!pieceProvider.TryGetNextPieceType(out TetrominoType type))
        {
            providerExhausted = true;
            return false;
        }

        return SpawnPiece(type);
    }

    public bool SpawnPiece(TetrominoType type)
    {
        if (hasActivePiece)
            return false;

        if (blockPrefab == null)
        {
            Debug.LogError("Block prefab is missing.");
            return false;
        }

        activeType = type;
        activeCells = TetrominoShape.GetCells(type);
        activePieceTags = RollPieceTags();
        activePosition = new Vector2Int(width / 2, height - 2);
        if (!IsValidPosition(activePosition, activeCells))
        {
            RaiseGameOver("Game Over: cannot spawn piece.");
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
        if (!IsBoardActive || !enableDebugPieceSpawn)
            return;

        if (hasActivePiece && replaceActivePieceOnDebugSpawn)
        {
            ClearActiveVisuals();
            hasActivePiece = false;
        }

        SpawnPieceFromProvider();
    }

    public void CancelActivePieceForDebug()
    {
        ClearActiveVisuals();
        hasActivePiece = false;
        fallTimer = 0f;
    }

    public void ResetBoardForNewRun()
    {
        CancelActivePieceForDebug();
        ClearLockedBlocks();
        gameOverRaised = false;
        activePieceTags = PieceTag.None;
        corruptedBlocks.Clear();
        lockedTypeCounts.Clear();
        SetBoardActive(false);
        RaiseBoardChanged();
    }

    // effect system feeds these each round
    public void ResetRoundModifiers()
    {
        corruptedPieceChance = 0f;
        glitchedPieceChance = 0f;
        fallSpeedMultiplier = 1f;
        rotationLockedUntil = -1f;
    }

    public bool IsRotationLocked => Time.time < rotationLockedUntil;

    // boss skill hook, freezes rotation for a few seconds
    public void LockRotation(float seconds)
    {
        if (seconds <= 0f)
            return;

        rotationLockedUntil = Mathf.Max(rotationLockedUntil, Time.time + seconds);
    }

    // boss skill hook, junk cells appear in the danger zone just under the spawn rows
    public void AddGarbageCellsNearTop(int count)
    {
        if (blockPrefab == null || grid == null || count <= 0)
            return;

        int rowMax = height - 3;
        int rowMin = Mathf.Max(0, rowMax - 2);
        List<Vector2Int> openCells = new();

        for (int y = rowMin; y <= rowMax; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (grid[x, y] == null)
                    openCells.Add(new Vector2Int(x, y));
            }
        }

        int placed = 0;

        while (placed < count && openCells.Count > 0)
        {
            int pick = UnityEngine.Random.Range(0, openCells.Count);
            CreateGarbageBlock(openCells[pick]);
            openCells.RemoveAt(pick);
            placed++;
        }

        if (placed > 0)
            RaiseBoardChanged();
    }

    // boss skill hook, deletes the lowest occupied row without paying any reward
    public bool RemoveBottomLineNoReward()
    {
        if (grid == null)
            return false;

        for (int y = 0; y < height; y++)
        {
            bool hasBlock = false;

            for (int x = 0; x < width; x++)
            {
                if (grid[x, y] != null)
                {
                    hasBlock = true;
                    break;
                }
            }

            if (!hasBlock)
                continue;

            ClearLine(y);
            ShiftRowsDown(y + 1);
            RaiseBoardChanged();
            return true;
        }

        return false;
    }

    // boss debuff hook, positive percent speeds the fall up, negative slows it
    public void AddFallSpeedPercent(int percent)
    {
        fallSpeedMultiplier = Mathf.Clamp(fallSpeedMultiplier * (1f + percent / 100f), 0.25f, 4f);
    }

    public float FallSpeedMultiplier => fallSpeedMultiplier;

    public float GetBoardSpareScore()
    {
        if (grid == null || width <= 0 || height <= 0)
            return 0.5f;

        int totalCells = width * height;
        int emptyCells = 0;
        int topRows = Mathf.Min(4, height);
        int topStart = height - topRows;
        int topFilled = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (grid[x, y] == null)
                {
                    emptyCells++;
                    continue;
                }

                if (y >= topStart)
                    topFilled++;
            }
        }

        float emptyScore = (float)emptyCells / totalCells;
        float topCrowding = topRows > 0 ? (float)topFilled / (width * topRows) : 0f;
        return Mathf.Clamp01(emptyScore - 0.25f * topCrowding);
    }

    public void AddRoundTagChance(PieceTag tag, float chance)
    {
        if ((tag & PieceTag.Corrupted) != 0)
            corruptedPieceChance = Mathf.Clamp01(Mathf.Max(corruptedPieceChance, chance));

        if ((tag & PieceTag.Glitched) != 0)
            glitchedPieceChance = Mathf.Clamp01(Mathf.Max(glitchedPieceChance, chance));
    }

    public int GetLockedTypeCount(TetrominoType type)
    {
        return lockedTypeCounts.TryGetValue(type, out int count) ? count : 0;
    }

    // boss effect hook, drops locked junk cells into the lowest open rows
    public void AddGarbageCells(int count)
    {
        if (blockPrefab == null || grid == null || count <= 0)
            return;

        List<int> emptyColumns = new();
        int remaining = count;

        for (int y = 0; y < height && remaining > 0; y++)
        {
            emptyColumns.Clear();

            for (int x = 0; x < width; x++)
            {
                if (grid[x, y] == null)
                    emptyColumns.Add(x);
            }

            // always leave one gap so garbage can never complete a line by itself
            int fillable = Mathf.Min(remaining, emptyColumns.Count - 1);

            for (int i = 0; i < fillable; i++)
            {
                int pick = UnityEngine.Random.Range(0, emptyColumns.Count);
                int column = emptyColumns[pick];
                emptyColumns.RemoveAt(pick);
                CreateGarbageBlock(new Vector2Int(column, y));
                remaining--;
            }
        }

        if (remaining < count)
            RaiseBoardChanged();
    }

    private void CreateGarbageBlock(Vector2Int cell)
    {
        GameObject block = Instantiate(blockPrefab, lockedBlockRoot);
        block.name = $"Garbage_{cell.x}_{cell.y}";
        block.SetActive(true);
        block.transform.localScale = GetCellVisualScale();
        block.transform.position = CellToWorld(cell);

        SpriteRenderer spriteRenderer = block.GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer != null)
            ConfigureBlockRenderer(spriteRenderer, garbageBlockColor);

        grid[cell.x, cell.y] = block.transform;
    }

    private void HandleMovePressed(Vector2Int direction)
    {
        if (!IsBoardActive || !hasActivePiece)
            return;

        if (direction == Vector2Int.up && !allowMoveUpForDebug)
            return;

        TryMove(direction);
    }

    private void HandleRotateClockwise()
    {
        if (!IsBoardActive || !hasActivePiece)
            return;

        TryRotate(clockwise: true);
    }

    private void HandleRotateCounterClockwise()
    {
        if (!IsBoardActive || !hasActivePiece)
            return;

        TryRotate(clockwise: false);
    }

    private void HandleHardDrop()
    {
        if (!IsBoardActive || !hasActivePiece)
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
        if (!hasActivePiece || !TetrominoShape.CanRotate(activeType))
            return;

        if (IsRotationLocked)
            return;

        // glitched piece sometimes just spins the other way
        if ((activePieceTags & PieceTag.Glitched) != 0 && UnityEngine.Random.value < 0.3f)
            clockwise = !clockwise;

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

            if ((activePieceTags & PieceTag.Corrupted) != 0)
                corruptedBlocks.Add(visual);
        }

        lockedTypeCounts[activeType] = GetLockedTypeCount(activeType) + 1;
        activePieceTags = PieceTag.None;
        activeVisuals.Clear();
        hasActivePiece = false;

        PieceLocked?.Invoke();
        GameEvents.PieceLocked();

        int cleared = ClearFullLines();

        if (cleared > 0)
        {
            LinesCleared?.Invoke(cleared);
            GameEvents.LineCleared(cleared);
        }

        RaiseBoardChanged();

        if (IsTouchingTop())
        {
            RaiseGameOver("Game Over: blocks touched the top.");
            return;
        }

        if (!autoSpawnAfterLock)
            return;

        bool spawned = TrySpawnPieceFromProvider(out bool providerExhausted);

        if (!spawned && providerExhausted)
        {
            SetBoardActive(false);
            RoundPiecesExhausted?.Invoke();
            GameEvents.RoundPiecesExhausted();
        }
    }

    private int ClearFullLines()
    {
        int cleared = 0;
        int corruptedLines = 0;

        for (int y = 0; y < height; y++)
        {
            if (!IsLineFull(y))
                continue;

            if (ClearLine(y))
                corruptedLines++;

            ShiftRowsDown(y + 1);
            cleared++;
            // re-check this y because the row above just shifted into it
            y--;
        }

        if (cleared > 0)
            Debug.Log($"Cleared lines: {cleared}");

        if (corruptedLines > 0)
            GameEvents.CorruptedLineCleared(corruptedLines);

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

    // returns true if the line had a corrupted block in it
    private bool ClearLine(int y)
    {
        bool hadCorrupted = false;

        for (int x = 0; x < width; x++)
        {
            if (grid[x, y] == null)
                continue;

            if (corruptedBlocks.Remove(grid[x, y]))
                hadCorrupted = true;

            DestroyObject(grid[x, y].gameObject);
            grid[x, y] = null;
        }

        return hadCorrupted;
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

    private PieceTag RollPieceTags()
    {
        PieceTag tags = PieceTag.None;

        if (corruptedPieceChance > 0f && UnityEngine.Random.value < corruptedPieceChance)
            tags |= PieceTag.Corrupted;

        if (glitchedPieceChance > 0f && UnityEngine.Random.value < glitchedPieceChance)
            tags |= PieceTag.Glitched;

        return tags;
    }

    private Color ApplyTagTint(Color baseColor)
    {
        Color tinted = baseColor;

        if ((activePieceTags & PieceTag.Corrupted) != 0)
            tinted = Color.Lerp(tinted, corruptedTint, 0.6f);

        if ((activePieceTags & PieceTag.Glitched) != 0)
            tinted = Color.Lerp(tinted, glitchedTint, 0.5f);

        return tinted;
    }

    private void CreateActiveVisuals()
    {
        ClearActiveVisuals();

        Color color = ApplyTagTint(GetColor(activeType));

        for (int i = 0; i < activeCells.Length; i++)
        {
            GameObject block = Instantiate(blockPrefab, activeBlockRoot);
            block.name = $"Active_{activeType}_{i}";
            block.SetActive(true);
            block.transform.localScale = GetCellVisualScale();

            SpriteRenderer spriteRenderer = block.GetComponentInChildren<SpriteRenderer>(true);

            if (spriteRenderer != null)
            {
                ConfigureBlockRenderer(spriteRenderer, color);
            }

            activeVisuals.Add(block.transform);
        }
    }

    private void ConfigureBlockRenderer(SpriteRenderer spriteRenderer, Color color)
    {
        spriteRenderer.color = color;
        spriteRenderer.transform.localPosition = Vector3.zero;
        spriteRenderer.gameObject.SetActive(true);

        spriteRenderer.GetPropertyBlock(blockPropertyBlock);
        // set both common shader color slots so Sprite and URP materials behave the same
        blockPropertyBlock.SetColor(BaseColorId, color);
        blockPropertyBlock.SetColor(ColorId, color);
        spriteRenderer.SetPropertyBlock(blockPropertyBlock);
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
                DestroyObject(activeVisuals[i].gameObject);
        }

        activeVisuals.Clear();
    }

    private void ClearLockedBlocks()
    {
        List<GameObject> blocks = new();

        // collect first so destruction does not mutate the hierarchy while we enumerate it
        if (lockedBlockRoot != null)
        {
            foreach (Transform child in lockedBlockRoot)
            {
                blocks.Add(child.gameObject);
            }
        }

        foreach (GameObject block in blocks)
        {
            if (block != null)
                DestroyObject(block);
        }

        grid = new Transform[width, height];
    }

    private void RebuildBorderBlocks()
    {
        ClearBorderBlocks();

        if (!generateBorderBlocks || blockPrefab == null || borderBlockRoot == null)
            return;

        // border blocks stay outside the grid so they never affect collision or line clears
        for (int x = -1; x <= width; x++)
        {
            CreateBorderBlock(new Vector2Int(x, -1));
            CreateBorderBlock(new Vector2Int(x, height));
        }

        for (int y = 0; y < height; y++)
        {
            CreateBorderBlock(new Vector2Int(-1, y));
            CreateBorderBlock(new Vector2Int(width, y));
        }
    }

    private void CreateBorderBlock(Vector2Int cell)
    {
        GameObject block = Instantiate(blockPrefab, borderBlockRoot);
        block.name = $"Border_{cell.x}_{cell.y}";
        block.SetActive(true);
        block.transform.localScale = GetBlockScale();
        block.transform.position = CellToWorld(cell);

        SpriteRenderer spriteRenderer = block.GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer != null)
            ConfigureBorderRenderer(spriteRenderer);
    }

    private void ConfigureBorderRenderer(SpriteRenderer spriteRenderer)
    {
        spriteRenderer.sprite = ResolveSolidBorderSprite();
        ConfigureBlockRenderer(spriteRenderer, borderBlockColor);
    }

    private Sprite ResolveSolidBorderSprite()
    {
        if (solidBorderSprite != null)
            return solidBorderSprite;

        Texture2D texture = Texture2D.whiteTexture;
        Rect rect = new(0f, 0f, texture.width, texture.height);
        // solid white keeps prefab texture shading from turning the frame gray
        solidBorderSprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), texture.width);
        return solidBorderSprite;
    }

    private void ClearBorderBlocks()
    {
        if (borderBlockRoot == null)
            return;

        List<GameObject> blocks = new();

        foreach (Transform child in borderBlockRoot)
        {
            blocks.Add(child.gameObject);
        }

        foreach (GameObject block in blocks)
        {
            if (block != null)
                DestroyObject(block);
        }
    }

    private void RaiseBoardChanged()
    {
        BoardChanged?.Invoke();
        GameEvents.BoardChanged();
    }

    private void RaiseGameOver(string message)
    {
        if (gameOverRaised)
            return;

        gameOverRaised = true;
        Debug.Log(message);
        SetBoardActive(false);
        GameOver?.Invoke();
        GameEvents.GameOver();
    }

    private void DestroyObject(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        return transform.position + new Vector3(cell.x * cellSize, cell.y * cellSize, 0f);
    }

    private Vector3 GetBlockScale()
    {
        return Vector3.one * Mathf.Max(0.05f, cellSize);
    }

    private Vector3 GetCellVisualScale()
    {
        // smaller than the cell so each block gets its black outline
        return Vector3.one * Mathf.Max(0.05f, cellSize - cellVisualInset);
    }

    private Vector2Int WorldToCell(Vector3 worldPosition)
    {
        // blocks sit on cell centers
        Vector3 local = worldPosition - transform.position;

        int x = Mathf.RoundToInt(local.x / cellSize);
        int y = Mathf.RoundToInt(local.y / cellSize);

        return new Vector2Int(x, y);
    }

    private Color GetColor(TetrominoType type)
    {
        return type switch
        {
            TetrominoType.I => colorI,
            TetrominoType.O => colorO,
            TetrominoType.T => colorT,
            TetrominoType.S => colorS,
            TetrominoType.Z => colorZ,
            TetrominoType.J => colorJ,
            TetrominoType.L => colorL,
            _ => Color.white,
        };
    }
}
