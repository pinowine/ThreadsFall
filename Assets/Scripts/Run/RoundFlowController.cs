using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RoundFlowController : MonoBehaviour
{
    [Header("Rounds")]
    [SerializeField] private List<RoundDefinition> rounds = new();

    [Header("Tetris")]
    [SerializeField] private TetrisBoardController boardController;
    [SerializeField] private RoundPieceProvider pieceProvider;

    [Header("Run")]
    [SerializeField] private RunStatsController statsController;

    [Header("UI")]
    [SerializeField] private RoundPiecePreviewView piecePreviewView;
    [SerializeField] private ShutterPanelSwitcher panelSwitcher;
    [SerializeField] private BossPanelView bossPanelView;
    [SerializeField] private IntermissionPanelView intermissionPanelView;
    [SerializeField] private RunStatsHudView statsHudView;

    [Header("Debug")]
    [SerializeField] private bool enableDebugHotkeys;

    [Header("Round Randomization")]
    [SerializeField] private bool randomizeRoundsEachRun = true;
    [SerializeField] private bool shuffleRoundOrder = true;
    [SerializeField] private bool randomizeRoundPieces = true;
    [SerializeField] private bool randomizeBossesEachRun = true;
    [SerializeField] private int minimumRandomPiecesPerRound = 5;
    [SerializeField] private int maximumRandomPiecesPerRound = 10;

    [Header("Intermission Shop")]
    [SerializeField] private bool enableIntermissionShopOffers = true;
    [SerializeField] private int shopOffersPerRound = 3;
    [SerializeField] private List<ShopItemTemplate> shopItemTemplates = new()
    {
        new ShopItemTemplate("deep_breath_debt", "Deep Breath Debt", 1, 2),
        new ShopItemTemplate("focus_tab", "Focus Tab", 2, 3),
        new ShopItemTemplate("panic_buffer", "Panic Buffer", 3, 5),
        new ShopItemTemplate("mercy_marker", "Mercy Marker", 1, 4),
        new ShopItemTemplate("late_round_push", "Late Round Push", 2, 6)
    };

    public event Action<RunGameState> StateChanged;

    public RunGameState CurrentState { get; private set; } = RunGameState.RunStart;
    public int CurrentRoundIndex => currentRoundIndex;

    [Serializable]
    public sealed class ShopItemTemplate
    {
        public string itemId;
        public string displayName;
        public int composureCost;
        public int noiseIncrease;

        public ShopItemTemplate()
        {
        }

        public ShopItemTemplate(string itemId, string displayName, int composureCost, int noiseIncrease)
        {
            this.itemId = itemId;
            this.displayName = displayName;
            this.composureCost = composureCost;
            this.noiseIncrease = noiseIncrease;
        }

        public string GetOptionText()
        {
            return $"{displayName}  -{composureCost} composure, +{noiseIncrease} noise";
        }
    }

    private static readonly TetrominoType[] AllTetrominoTypes =
    {
        TetrominoType.I,
        TetrominoType.O,
        TetrominoType.T,
        TetrominoType.S,
        TetrominoType.Z,
        TetrominoType.J,
        TetrominoType.L
    };

    private static readonly BossId[] RandomBossIds =
    {
        BossId.FalseHelper,
        BossId.Spammer,
        BossId.Algorithm
    };

    private readonly List<RoundDefinition> runtimeRounds = new();
    private readonly List<TetrominoType> reusablePieceBag = new();
    private readonly List<ShopItemTemplate> currentShopOffers = new();
    private int currentRoundIndex = -1;
    private int currentRunSeed;
    private bool runEnded;
    private bool awaitingShopDecision;
    private System.Random runRandom;
    // shop input opens after the panel switch finishes
    private bool acceptingShopInput;

    private void OnEnable()
    {
        if (boardController != null)
        {
            boardController.LinesCleared += HandleLinesCleared;
            boardController.RoundPiecesExhausted += HandleRoundPiecesExhausted;
            boardController.GameOver += HandleGameOver;
        }

        if (panelSwitcher != null)
            panelSwitcher.SwitchCompleted += HandlePanelSwitchCompleted;
    }

    private void OnDisable()
    {
        if (boardController != null)
        {
            boardController.LinesCleared -= HandleLinesCleared;
            boardController.RoundPiecesExhausted -= HandleRoundPiecesExhausted;
            boardController.GameOver -= HandleGameOver;
        }

        if (panelSwitcher != null)
            panelSwitcher.SwitchCompleted -= HandlePanelSwitchCompleted;
    }

    private void Start()
    {
        StartRun();
    }

    private void Update()
    {
        HandleShopDecisionInput();

        if (!enableDebugHotkeys)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.rKey.wasPressedThisFrame)
            RestartFullRunForDebug();

        if (keyboard.nKey.wasPressedThisFrame)
            ForceAdvanceForDebug();
    }

    public void StartRun()
    {
        if (!ValidateRequiredReferences())
            return;

        runEnded = false;
        awaitingShopDecision = false;
        acceptingShopInput = false;
        currentRoundIndex = -1;
        PrepareRuntimeRounds();

        boardController.SetBoardActive(false);
        statsController.ResetRun();

        if (statsHudView != null)
            statsHudView.SetStatsController(statsController);

        EnterState(RunGameState.RunStart);
        StartNextRoundInternal();
    }

    public void RequestNextRoundFromShop()
    {
        if (CurrentState == RunGameState.GameOver || CurrentState == RunGameState.RunComplete)
        {
            RestartRunWithoutPlaymode();
            return;
        }

        if (CurrentState != RunGameState.IntermissionShop)
            return;

        if (awaitingShopDecision)
            return;

        if (!acceptingShopInput)
            return;

        acceptingShopInput = false;

        if (intermissionPanelView != null)
            intermissionPanelView.SetNextRoundButtonInteractable(false);

        StartNextRoundInternal();
    }

    // kept for existing scene button bindings; new bindings should call RequestNextRoundFromShop
    public void StartNextRound()
    {
        RequestNextRoundFromShop();
    }

    private void StartNextRoundInternal()
    {
        if (runEnded)
            return;

        acceptingShopInput = false;
        currentRoundIndex++;

        IReadOnlyList<RoundDefinition> activeRounds = GetActiveRounds();

        if (currentRoundIndex >= activeRounds.Count)
        {
            EnterRunComplete();
            return;
        }

        RoundDefinition round = activeRounds[currentRoundIndex];
        EnterRoundPreparation(round);
    }

    private void EnterRoundPreparation(RoundDefinition round)
    {
        EnterState(RunGameState.RoundPreparation);

        IReadOnlyList<TetrominoType> roundPieces = round.pieces != null
            ? round.pieces
            : Array.Empty<TetrominoType>();

        statsController.SetCurrentRoundIndex(currentRoundIndex + 1);
        pieceProvider.SetRoundPieces(roundPieces);

        if (piecePreviewView != null)
            piecePreviewView.ShowRoundPieces(roundPieces);

        if (bossPanelView != null)
            bossPanelView.ShowBoss(round.bossId);

        if (intermissionPanelView != null)
        {
            intermissionPanelView.SetNextRoundButtonVisible(false);
            intermissionPanelView.SetNextRoundButtonInteractable(false);
        }

        GameEvents.RoundChanged(currentRoundIndex + 1);
        GameEvents.BossChanged(round.bossId);

        if (panelSwitcher != null)
            panelSwitcher.SwitchToBoss();

        Debug.Log($"Round started: {round.roundId}, remaining pieces: {pieceProvider.RemainingCount}");

        EnterBossPresentation(round);
    }

    private void EnterBossPresentation(RoundDefinition round)
    {
        EnterState(RunGameState.BossPresentation);
        // kept as a state hook for a future pause or animation
        EnterRoundActive(round);
    }

    private void EnterRoundActive(RoundDefinition round)
    {
        EnterState(RunGameState.RoundActive);
        boardController.SetBoardActive(true);

        if (pieceProvider.RemainingCount <= 0)
        {
            EnterRoundResolution();
            return;
        }

        bool spawned = boardController.SpawnPieceFromProvider();

        if (!spawned && CurrentState != RunGameState.GameOver)
            EnterRoundResolution(playerLost: true);
    }

    private void EnterRoundResolution(bool playerLost = false)
    {
        if (runEnded)
            return;

        boardController.SetBoardActive(false);
        acceptingShopInput = false;
        EnterState(RunGameState.RoundResolution);

        bool completesRun = !playerLost && currentRoundIndex >= GetActiveRounds().Count - 1;

        ContinueAfterRoundResolution(playerLost, completesRun);
    }

    private void EnterIntermissionShop()
    {
        acceptingShopInput = false;
        awaitingShopDecision = false;
        EnterState(RunGameState.IntermissionShop);

        PrepareShopDecision();

        if (panelSwitcher != null)
            panelSwitcher.SwitchToShop();
        else
            EnableShopInputIfReady();

        GameEvents.ShopOpened();
        Debug.Log("Enter Shop.");
    }

    private void EnterRunComplete()
    {
        runEnded = true;
        acceptingShopInput = false;
        awaitingShopDecision = false;

        boardController.SetBoardActive(false);
        EnterState(RunGameState.RunComplete);

        if (intermissionPanelView != null)
            intermissionPanelView.ShowRunComplete(statsController);

        if (panelSwitcher != null)
            panelSwitcher.SwitchToShop();

        GameEvents.RunComplete();
        Debug.Log("Run Complete.");
    }

    private void EnterGameOver()
    {
        if (CurrentState == RunGameState.GameOver)
            return;

        runEnded = true;
        acceptingShopInput = false;
        awaitingShopDecision = false;

        if (boardController != null)
            boardController.SetBoardActive(false);

        EnterState(RunGameState.GameOver);

        if (intermissionPanelView != null)
            intermissionPanelView.ShowGameOver(statsController);

        if (panelSwitcher != null)
            panelSwitcher.SwitchToShop();

        Debug.Log("Game Over.");
    }

    private void HandleLinesCleared(int linesCleared)
    {
        if (statsController != null)
            statsController.ApplyLineClearReward(linesCleared);
    }

    private void HandleRoundPiecesExhausted()
    {
        if (CurrentState != RunGameState.RoundActive)
            return;

        EnterRoundResolution();
    }

    private void HandleGameOver()
    {
        EnterRoundResolution(playerLost: true);
    }

    private void HandlePanelSwitchCompleted(GameObject shownPanel)
    {
        if (CurrentState == RunGameState.IntermissionShop)
            EnableShopInputIfReady();
    }

    private void EnableShopInputIfReady()
    {
        if (!awaitingShopDecision)
            EnableShopInput();
    }

    private void EnableShopInput()
    {
        acceptingShopInput = true;

        if (intermissionPanelView != null)
        {
            intermissionPanelView.SetNextRoundButtonVisible(true);
            intermissionPanelView.SetNextRoundButtonInteractable(true);
        }
    }

    private void ContinueAfterRoundResolution(bool playerLost, bool completesRun)
    {
        if (playerLost)
        {
            EnterGameOver();
            return;
        }

        if (completesRun)
        {
            EnterRunComplete();
            return;
        }

        EnterIntermissionShop();
    }

    public void SubmitShopDecision(int optionId)
    {
        if (!awaitingShopDecision || CurrentState != RunGameState.IntermissionShop)
            return;

        if (optionId == 0 || optionId == 4)
        {
            CompleteShopDecision("Shop skipped");
            return;
        }

        int offerIndex = optionId - 1;

        if (offerIndex < 0 || offerIndex >= currentShopOffers.Count)
            return;

        ShopItemTemplate item = currentShopOffers[offerIndex];
        int composureCost = Mathf.Max(0, item.composureCost);

        if (!statsController.TrySpendComposure(composureCost))
        {
            Debug.LogWarning($"Shop item needs {composureCost} composure: {item.displayName}");
            return;
        }

        int noiseIncrease = Mathf.Max(0, item.noiseIncrease);
        statsController.AdjustNoise(noiseIncrease);
        CompleteShopDecision($"Bought {item.displayName}\n-{composureCost} composure, +{noiseIncrease} noise");
    }

    private void PrepareShopDecision()
    {
        currentShopOffers.Clear();

        if (!enableIntermissionShopOffers)
        {
            if (intermissionPanelView != null)
                intermissionPanelView.ShowShop(statsController, true);
            return;
        }

        BuildShopOffers();
        awaitingShopDecision = currentShopOffers.Count > 0;

        if (intermissionPanelView != null)
            intermissionPanelView.ShowShop(statsController, BuildShopOptionText(), !awaitingShopDecision);

        if (!awaitingShopDecision)
            Debug.Log("Shop has no offers. Continue to next round.");
    }

    private void BuildShopOffers()
    {
        List<ShopItemTemplate> pool = new();

        for (int i = 0; i < shopItemTemplates.Count; i++)
        {
            ShopItemTemplate item = shopItemTemplates[i];

            if (item == null || string.IsNullOrWhiteSpace(item.displayName))
                continue;

            pool.Add(item);
        }

        Shuffle(pool);

        int offerCount = Mathf.Clamp(shopOffersPerRound, 1, 3);
        offerCount = Mathf.Min(offerCount, pool.Count);

        for (int i = 0; i < offerCount; i++)
        {
            currentShopOffers.Add(pool[i]);
        }
    }

    private List<string> BuildShopOptionText()
    {
        List<string> optionText = new();

        for (int i = 0; i < currentShopOffers.Count; i++)
        {
            optionText.Add(currentShopOffers[i].GetOptionText());
        }

        return optionText;
    }

    private void CompleteShopDecision(string result)
    {
        awaitingShopDecision = false;

        if (intermissionPanelView != null)
            intermissionPanelView.ShowShopResult(statsController, result);

        EnableShopInput();
        Debug.Log(result);
    }

    private void HandleShopDecisionInput()
    {
        if (!awaitingShopDecision || CurrentState != RunGameState.IntermissionShop)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            SubmitShopDecision(1);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            SubmitShopDecision(2);
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            SubmitShopDecision(3);
        else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame || keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame)
            SubmitShopDecision(4);
    }

    private void EnterState(RunGameState state)
    {
        CurrentState = state;
        StateChanged?.Invoke(CurrentState);
        GameEvents.RunStateChanged(CurrentState);
    }

    private void RestartFullRunForDebug()
    {
        RestartRunWithoutPlaymode();
    }

    private void RestartRunWithoutPlaymode()
    {
        if (boardController != null)
            boardController.ResetBoardForNewRun();

        StartRun();
    }

    private void ForceAdvanceForDebug()
    {
        if (CurrentState == RunGameState.RoundActive)
        {
            boardController.CancelActivePieceForDebug();
            EnterRoundResolution();
            return;
        }

        if (CurrentState == RunGameState.IntermissionShop)
            RequestNextRoundFromShop();
    }

    private void PrepareRuntimeRounds()
    {
        currentRunSeed = Guid.NewGuid().GetHashCode();
        runRandom = new System.Random(currentRunSeed);
        runtimeRounds.Clear();

        if (!randomizeRoundsEachRun)
        {
            Debug.Log($"Run seed: {currentRunSeed}");
            return;
        }

        for (int i = 0; i < rounds.Count; i++)
        {
            runtimeRounds.Add(CreateRuntimeRound(rounds[i], i));
        }

        if (shuffleRoundOrder)
            Shuffle(runtimeRounds);

        for (int i = 0; i < runtimeRounds.Count; i++)
        {
            string baseId = string.IsNullOrWhiteSpace(runtimeRounds[i].roundId)
                ? FallbackRoundId(i)
                : runtimeRounds[i].roundId;

            runtimeRounds[i].roundId = $"{baseId}_seed{currentRunSeed:X8}_slot{i + 1:00}";
        }

        Debug.Log($"Run seed: {currentRunSeed}");
    }

    private IReadOnlyList<RoundDefinition> GetActiveRounds()
    {
        return randomizeRoundsEachRun ? runtimeRounds : rounds;
    }

    private RoundDefinition CreateRuntimeRound(RoundDefinition source, int sourceIndex)
    {
        RoundDefinition round = new()
        {
            roundId = source != null && !string.IsNullOrWhiteSpace(source.roundId)
                ? source.roundId
                : FallbackRoundId(sourceIndex),
            displayNameKey = source != null ? source.displayNameKey : string.Empty,
            bossId = randomizeBossesEachRun ? GetRandomBoss() : source != null ? source.bossId : BossId.None,
            pieces = CreateRuntimePieces(source),
            targetLines = source != null ? source.targetLines : 0,
            targetScore = source != null ? source.targetScore : 0,
            rewardAttention = source != null ? source.rewardAttention : 0
        };

        return round;
    }

    private List<TetrominoType> CreateRuntimePieces(RoundDefinition source)
    {
        if (!randomizeRoundPieces)
        {
            return source != null && source.pieces != null
                ? new List<TetrominoType>(source.pieces)
                : new List<TetrominoType>();
        }

        int minPieces = Mathf.Max(1, minimumRandomPiecesPerRound);
        int maxPieces = Mathf.Max(minPieces, maximumRandomPiecesPerRound);
        int pieceCount = NextInclusive(minPieces, maxPieces);

        return CreateRandomPieceSequence(pieceCount);
    }

    private List<TetrominoType> CreateRandomPieceSequence(int pieceCount)
    {
        List<TetrominoType> pieces = new();

        while (pieces.Count < pieceCount)
        {
            reusablePieceBag.Clear();
            reusablePieceBag.AddRange(AllTetrominoTypes);
            Shuffle(reusablePieceBag);

            for (int i = 0; i < reusablePieceBag.Count && pieces.Count < pieceCount; i++)
            {
                pieces.Add(reusablePieceBag[i]);
            }
        }

        return pieces;
    }

    private BossId GetRandomBoss()
    {
        return RandomBossIds[NextInclusive(0, RandomBossIds.Length - 1)];
    }

    private int NextInclusive(int minInclusive, int maxInclusive)
    {
        int min = Mathf.Min(minInclusive, maxInclusive);
        int max = Mathf.Max(minInclusive, maxInclusive);
        runRandom ??= new System.Random(Guid.NewGuid().GetHashCode());
        return runRandom.Next(min, max + 1);
    }

    private void Shuffle<T>(IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = runRandom.Next(0, i + 1);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }

    private static string FallbackRoundId(int index)
    {
        return "round_" + (index + 1).ToString("00");
    }

    private bool ValidateRequiredReferences()
    {
        if (boardController == null)
        {
            Debug.LogError("RoundFlowController requires a TetrisBoardController.");
            return false;
        }

        if (pieceProvider == null)
        {
            Debug.LogError("RoundFlowController requires a RoundPieceProvider.");
            return false;
        }

        if (statsController == null)
        {
            Debug.LogError("RoundFlowController requires a RunStatsController.");
            return false;
        }

        if (statsHudView == null)
            Debug.LogWarning("RunStatsHudView is not bound. HUD stats will not update.");

        return true;
    }
}
