using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class RoundFlowController : MonoBehaviour
{
    [Header("Legacy Round Fallbacks")]
    [SerializeField] private List<RoundDefinition> rounds = new();

    [Header("Tetris")]
    [SerializeField] private TetrisBoardController boardController;
    [SerializeField] private RoundPieceProvider pieceProvider;

    [Header("Run")]
    [SerializeField] private RunStatsController statsController;
    [SerializeField] private RunEffectSystem effectController;
    [SerializeField] private RunShopController shopController;

    [Header("UI")]
    [SerializeField] private RoundPiecePreviewView piecePreviewView;
    [SerializeField] private ShutterPanelSwitcher panelSwitcher;
    [SerializeField] private BossPanelView bossPanelView;
    [SerializeField] private IntermissionPanelView intermissionPanelView;
    [SerializeField] private RunStatsHudView statsHudView;

    [Header("Boss Data")]
    [SerializeField] private BossCatalog bossCatalog;

    [Header("Debug")]
    [SerializeField] private bool enableDebugHotkeys = true;

    [Header("Run Route")]
    // route shape: opening searches, mini boss, mid searches, mini boss, late searches, final boss
    [SerializeField] private Vector2Int openingSearchNodes = new(3, 4);
    [SerializeField] private Vector2Int midSearchNodes = new(2, 3);
    [SerializeField] private Vector2Int lateSearchNodes = new(1, 2);
    [SerializeField] private int initialPieceCount = 10;
    [SerializeField] private int pieceCountIncreasePerNode = 5;
    // used only when boss catalog assets are missing
    [SerializeField] private List<BossId> miniBossPool = new() { BossId.FalseHelper, BossId.Spammer };
    [SerializeField] private List<BossId> finalBossPool = new() { BossId.Algorithm };

    [Header("Intermission Shop")]
    [SerializeField] private bool enableIntermissionShopOffers = true;

    public event Action<RunGameState> StateChanged;

    public RunGameState CurrentState { get; private set; } = RunGameState.RunStart;
    public int CurrentRoundIndex => currentRoundIndex;

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

    private readonly List<RuntimeRunNode> runtimeNodes = new();
    private readonly List<TetrominoType> reusablePieceBag = new();
    private readonly List<RunNodeType> reusableRouteShape = new();
    private readonly List<PieceTagSummary> reusableTagSummaries = new();
    private readonly HashSet<BossId> reusableUsedBosses = new();
    private readonly HashSet<string> reusableUsedBossKeys = new();
    private readonly List<BossId> reusableBossCandidates = new();
    private readonly List<BossDefinition> reusableBossDefinitionCandidates = new();
    private int currentRoundIndex = -1;
    private int currentRunSeed;
    private bool runEnded;
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
        if (!enableDebugHotkeys && !Application.isEditor)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        HandleDeveloperShortcuts(keyboard);

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
        acceptingShopInput = false;
        currentRoundIndex = -1;
        PrepareRuntimeNodes();

        boardController.SetBoardActive(false);
        statsController.ResetRun();
        effectController.Initialize(statsController, boardController, piecePreviewView);
        effectController.ResetRun();
        shopController.Initialize(statsController, effectController);
        shopController.ResetRun();

        if (statsHudView != null)
        {
            statsHudView.SetStatsController(statsController);
            statsHudView.SetShopController(shopController);
            statsHudView.SetEffectSystem(effectController);
        }

        if (bossPanelView != null)
        {
            bossPanelView.SetEffectController(effectController);
            bossPanelView.BindRoute(runtimeNodes);
        }

        if (panelSwitcher != null && bossPanelView != null)
            panelSwitcher.SetShutterTarget(bossPanelView.AvatarRect);

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

        if (!acceptingShopInput)
            return;

        acceptingShopInput = false;

        if (bossPanelView != null)
            bossPanelView.SetNextRoundButtonInteractable(false);

        if (intermissionPanelView != null)
            intermissionPanelView.SetNextRoundButtonInteractable(false);

        StartNextRoundInternal();
    }

    // kept for existing scene button bindings
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

        IReadOnlyList<RuntimeRunNode> activeNodes = runtimeNodes;

        if (currentRoundIndex >= activeNodes.Count)
        {
            EnterRunComplete();
            return;
        }

        RuntimeRunNode node = activeNodes[currentRoundIndex];
        EnterRoundPreparation(node);
    }

    private void EnterRoundPreparation(RuntimeRunNode node)
    {
        EnterState(RunGameState.RoundPreparation);

        IReadOnlyList<TetrominoType> nodePieces = node.Pieces != null
            ? node.Pieces
            : Array.Empty<TetrominoType>();

        statsController.SetCurrentRoundIndex(node.NodeIndex);
        statsController.ApplyRoundStartNoise();
        effectController.BeginRound(node.NodeIndex);
        // boss auras come online before the preview renders so hide and fake apply
        effectController.RegisterBossEffects(node.BossDefinition);
        // skills fire even on search nodes: the next boss up the route interferes early
        effectController.SetSkillSource(
            node.BossDefinition != null ? node.BossDefinition : FindUpcomingBossDefinition(currentRoundIndex),
            node.BossDefinition != null);
        effectController.Fire(EffectTrigger.RoundStart);
        pieceProvider.SetRoundPieces(nodePieces);

        if (piecePreviewView != null)
        {
            piecePreviewView.ShowRoundPieces(nodePieces);
            effectController.GetActiveTagSummaries(reusableTagSummaries);
            piecePreviewView.SetIncomingTags(reusableTagSummaries);
        }

        if (bossPanelView != null)
        {
            bossPanelView.SetEffectController(effectController);
            bossPanelView.ShowNode(node, currentRoundIndex, currentRoundIndex);
        }

        if (intermissionPanelView != null)
        {
            intermissionPanelView.SetNextRoundButtonVisible(false);
            intermissionPanelView.SetNextRoundButtonInteractable(false);
        }

        GameEvents.RoundChanged(node.NodeIndex);
        GameEvents.BossChanged(node.BossId);

        if (panelSwitcher != null)
            panelSwitcher.SwitchToBoss();

        Debug.Log($"Node started: {node.NodeId}, pieces: {pieceProvider.RemainingCount}");

        EnterBossPresentation(node);
    }

    private void EnterBossPresentation(RuntimeRunNode node)
    {
        EnterState(RunGameState.BossPresentation);
        // kept as a state hook for a future pause or animation
        EnterRoundActive(node);
    }

    private void EnterRoundActive(RuntimeRunNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

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
        effectController.Fire(EffectTrigger.RoundEnd);
        effectController.EndRound();

        bool completesRun = !playerLost && currentRoundIndex >= runtimeNodes.Count - 1;

        ContinueAfterRoundResolution(playerLost, completesRun);
    }

    private void EnterIntermissionShop()
    {
        acceptingShopInput = false;
        EnterState(RunGameState.IntermissionShop);

        if (enableIntermissionShopOffers)
            shopController.OpenShop(statsController.CurrentRoundIndex);

        if (bossPanelView != null)
        {
            int completedCount = currentRoundIndex + 1;
            int nextNodeIndex = Mathf.Clamp(completedCount, 0, runtimeNodes.Count - 1);
            bossPanelView.ShowShop(statsController, enableIntermissionShopOffers ? shopController : null, false, RequestNextRoundFromShop, nextNodeIndex, completedCount);
        }
        else if (intermissionPanelView != null)
        {
            intermissionPanelView.ShowShop(statsController, enableIntermissionShopOffers ? shopController : null, false);
        }

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

        boardController.SetBoardActive(false);
        EnterState(RunGameState.RunComplete);

        if (bossPanelView != null)
            bossPanelView.ShowRunComplete(statsController, RequestNextRoundFromShop);
        else if (intermissionPanelView != null)
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

        if (boardController != null)
            boardController.SetBoardActive(false);

        EnterState(RunGameState.GameOver);

        if (bossPanelView != null)
            bossPanelView.ShowGameOver(statsController, RequestNextRoundFromShop);
        else if (intermissionPanelView != null)
            intermissionPanelView.ShowGameOver(statsController);

        if (panelSwitcher != null)
            panelSwitcher.SwitchToShop();

        Debug.Log("Game Over.");
    }

    private void HandleLinesCleared(int linesCleared)
    {
        if (statsController != null)
        {
            int clearedBlocks = boardController != null ? linesCleared * boardController.Width : linesCleared * 10;
            statsController.ApplyLineClearReward(linesCleared, clearedBlocks);
        }
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
        EnableShopInput();
    }

    private void EnableShopInput()
    {
        acceptingShopInput = true;

        if (bossPanelView != null)
        {
            bossPanelView.SetNextRoundButtonVisible(true);
            bossPanelView.SetNextRoundButtonInteractable(true);
        }

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

    private void HandleDeveloperShortcuts(Keyboard keyboard)
    {
        if (WasPressed(keyboard.digit1Key, keyboard.numpad1Key))
            OpenShopForDebug();

        if (WasPressed(keyboard.digit2Key, keyboard.numpad2Key))
            statsController?.AddAttention(100);

        if (WasPressed(keyboard.digit3Key, keyboard.numpad3Key))
            statsController?.ApplyNoiseDelta(10);

        if (WasPressed(keyboard.digit4Key, keyboard.numpad4Key))
            statsController?.ApplyNoiseDelta(-10);

        if (WasPressed(keyboard.digit5Key, keyboard.numpad5Key))
            statsController?.ApplyComposureDelta(10);

        if (WasPressed(keyboard.digit6Key, keyboard.numpad6Key))
            statsController?.ApplyComposureDelta(-10);

        if (WasPressed(keyboard.digit7Key, keyboard.numpad7Key))
            SkipCurrentRoundToShopForDebug();

        if (WasPressed(keyboard.digit8Key, keyboard.numpad8Key))
            RestartFullRunForDebug();

        if (WasPressed(keyboard.digit9Key, keyboard.numpad9Key))
            EnterGameOver();

        if (WasPressed(keyboard.digit0Key, keyboard.numpad0Key))
            FastForwardToNextBossForDebug();
    }

    private bool WasPressed(KeyControl primary, KeyControl alternate)
    {
        return primary != null && primary.wasPressedThisFrame
            || alternate != null && alternate.wasPressedThisFrame;
    }

    private void OpenShopForDebug()
    {
        if (runEnded)
            runEnded = false;

        if (boardController != null)
        {
            boardController.CancelActivePieceForDebug();
            boardController.SetBoardActive(false);
        }

        EnterIntermissionShop();
    }

    private void SkipCurrentRoundToShopForDebug()
    {
        OpenShopForDebug();
    }

    private void FastForwardToNextBossForDebug()
    {
        if (runtimeNodes.Count <= 0)
            PrepareRuntimeNodes();

        int searchStart = Mathf.Clamp(currentRoundIndex + 1, 0, Mathf.Max(0, runtimeNodes.Count - 1));
        int targetIndex = -1;

        for (int i = searchStart; i < runtimeNodes.Count; i++)
        {
            if (runtimeNodes[i].IsBossNode)
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0)
            return;

        runEnded = false;
        acceptingShopInput = false;
        currentRoundIndex = targetIndex - 1;

        if (boardController != null)
            boardController.ResetBoardForNewRun();

        StartNextRoundInternal();
    }

    private void PrepareRuntimeNodes()
    {
        currentRunSeed = Guid.NewGuid().GetHashCode();
        runRandom = new System.Random(currentRunSeed);
        runtimeNodes.Clear();

        BuildRouteShape();
        int nodeCount = reusableRouteShape.Count;
        int finalNodeNumber = nodeCount;

        Dictionary<int, BossDefinition> miniBossesByNode = new();
        reusableUsedBosses.Clear();
        reusableUsedBossKeys.Clear();

        for (int i = 0; i < reusableRouteShape.Count; i++)
        {
            if (reusableRouteShape[i] != RunNodeType.MiniBoss)
                continue;

            int nodeNumber = i + 1;
            miniBossesByNode[nodeNumber] = DrawBossDefinition(RunNodeType.MiniBoss, reusableUsedBossKeys, GetAuthoredBossFallback(nodeNumber - 1, BossId.FalseHelper));
        }

        BossDefinition finalBoss = DrawBossDefinition(RunNodeType.FinalBoss, null, GetAuthoredBossFallback(finalNodeNumber - 1, BossId.Algorithm));
        BossId fallbackFinalBoss = finalBoss != null
            ? finalBoss.bossId
            : DrawBoss(finalBossPool, null, GetAuthoredBossFallback(finalNodeNumber - 1, BossId.Algorithm));

        for (int nodeNumber = 1; nodeNumber <= nodeCount; nodeNumber++)
        {
            RunNodeType nodeType = reusableRouteShape[nodeNumber - 1];
            BossDefinition bossDefinition = GetNodeBossDefinition(nodeNumber, nodeType, finalBoss, miniBossesByNode);
            BossId bossId = bossDefinition != null
                ? bossDefinition.bossId
                : GetNodeBoss(nodeNumber, nodeType, fallbackFinalBoss, miniBossesByNode);
            int pieceCount = GetPieceCountForNode(nodeNumber);
            string nodeId = $"node_{nodeNumber:00}_{nodeType.ToString().ToLowerInvariant()}_seed{currentRunSeed:X8}";

            runtimeNodes.Add(new RuntimeRunNode(
                nodeId,
                nodeNumber,
                nodeType,
                bossId,
                bossDefinition,
                GetDisplayNameKey(nodeType, bossId, bossDefinition),
                pieceCount,
                CreateRandomPieceSequence(pieceCount, bossDefinition)
            ));
        }

        Debug.Log($"Run seed: {currentRunSeed}, nodes: {runtimeNodes.Count}");
    }

    private void BuildRouteShape()
    {
        reusableRouteShape.Clear();
        AppendSearchNodes(openingSearchNodes);
        reusableRouteShape.Add(RunNodeType.MiniBoss);
        AppendSearchNodes(midSearchNodes);
        reusableRouteShape.Add(RunNodeType.MiniBoss);
        AppendSearchNodes(lateSearchNodes);
        reusableRouteShape.Add(RunNodeType.FinalBoss);
    }

    private void AppendSearchNodes(Vector2Int range)
    {
        int count = NextInclusive(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

        for (int i = 0; i < count; i++)
        {
            reusableRouteShape.Add(RunNodeType.Normal);
        }
    }

    private BossDefinition GetNodeBossDefinition(int nodeNumber, RunNodeType nodeType, BossDefinition finalBoss, Dictionary<int, BossDefinition> miniBossesByNode)
    {
        return nodeType switch
        {
            RunNodeType.FinalBoss => finalBoss,
            RunNodeType.MiniBoss => miniBossesByNode.TryGetValue(nodeNumber, out BossDefinition bossDefinition) ? bossDefinition : null,
            _ => null
        };
    }

    private BossId GetNodeBoss(int nodeNumber, RunNodeType nodeType, BossId finalBoss, Dictionary<int, BossDefinition> miniBossesByNode)
    {
        return nodeType switch
        {
            RunNodeType.FinalBoss => finalBoss,
            RunNodeType.MiniBoss => miniBossesByNode.TryGetValue(nodeNumber, out BossDefinition bossDefinition) && bossDefinition != null
                ? bossDefinition.bossId
                : DrawBoss(miniBossPool, reusableUsedBosses, GetAuthoredBossFallback(nodeNumber - 1, BossId.FalseHelper)),
            _ => BossId.None
        };
    }

    private int GetPieceCountForNode(int nodeNumber)
    {
        int count = initialPieceCount + Mathf.Max(0, nodeNumber - 1) * pieceCountIncreasePerNode;
        return Mathf.Max(1, count);
    }

    private string GetDisplayNameKey(RunNodeType nodeType, BossId bossId, BossDefinition bossDefinition)
    {
        if (bossDefinition != null && !string.IsNullOrWhiteSpace(bossDefinition.nameKey))
            return bossDefinition.nameKey;

        if (nodeType == RunNodeType.Normal)
            return "run.node.search.name";

        if (bossId != BossId.None)
            return "boss." + bossId.ToLocSegment() + ".name";

        return nodeType == RunNodeType.FinalBoss ? "run.node.final.name" : "run.node.mini_boss.name";
    }

    private List<TetrominoType> CreateRandomPieceSequence(int pieceCount, BossDefinition boss)
    {
        List<TetrominoType> pieces = new();
        bool biased = boss != null && boss.preferredPieces != null && boss.preferredPieces.Count > 0;

        if (biased)
        {
            // boss nodes lean toward the boss favorite piece properties
            float bias = Mathf.Max(1f, boss.preferredPieceBias);
            float totalWeight = 0f;
            float[] weights = new float[AllTetrominoTypes.Length];

            for (int i = 0; i < AllTetrominoTypes.Length; i++)
            {
                weights[i] = boss.preferredPieces.Contains(PieceLore.GetProperty(AllTetrominoTypes[i])) ? bias : 1f;
                totalWeight += weights[i];
            }

            while (pieces.Count < pieceCount)
            {
                float roll = (float)(runRandom.NextDouble() * totalWeight);
                float cumulative = 0f;

                for (int i = 0; i < AllTetrominoTypes.Length; i++)
                {
                    cumulative += weights[i];

                    if (roll <= cumulative)
                    {
                        pieces.Add(AllTetrominoTypes[i]);
                        break;
                    }
                }
            }

            return pieces;
        }

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

    private BossId DrawBoss(IReadOnlyList<BossId> pool, HashSet<BossId> usedBosses, BossId fallback)
    {
        reusableBossCandidates.Clear();

        if (pool != null)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                BossId bossId = pool[i];

                if (bossId == BossId.None)
                    continue;

                if (usedBosses == null || !usedBosses.Contains(bossId))
                    reusableBossCandidates.Add(bossId);
            }

            if (reusableBossCandidates.Count <= 0)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != BossId.None)
                        reusableBossCandidates.Add(pool[i]);
                }
            }
        }

        BossId chosen = reusableBossCandidates.Count > 0
            ? reusableBossCandidates[NextInclusive(0, reusableBossCandidates.Count - 1)]
            : fallback;

        if (usedBosses != null && chosen != BossId.None)
            usedBosses.Add(chosen);

        return chosen;
    }

    private BossDefinition DrawBossDefinition(RunNodeType nodeType, HashSet<string> usedBossKeys, BossId fallback)
    {
        BossCatalog activeCatalog = ResolveBossCatalog();

        if (activeCatalog == null)
            return null;

        activeCatalog.AppendAvailableBosses(reusableBossDefinitionCandidates, nodeType, usedBossKeys);

        if (reusableBossDefinitionCandidates.Count <= 0 && usedBossKeys != null)
            activeCatalog.AppendAvailableBosses(reusableBossDefinitionCandidates, nodeType, null);

        if (reusableBossDefinitionCandidates.Count <= 0)
            return activeCatalog.FindById(fallback);

        float totalWeight = 0f;

        for (int i = 0; i < reusableBossDefinitionCandidates.Count; i++)
        {
            totalWeight += Mathf.Max(0f, reusableBossDefinitionCandidates[i].routeWeight);
        }

        if (totalWeight <= 0f)
            return reusableBossDefinitionCandidates[0];

        float roll = (float)(runRandom.NextDouble() * totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < reusableBossDefinitionCandidates.Count; i++)
        {
            BossDefinition boss = reusableBossDefinitionCandidates[i];
            cumulative += Mathf.Max(0f, boss.routeWeight);

            if (roll <= cumulative)
            {
                if (usedBossKeys != null)
                    usedBossKeys.Add(boss.bossKey);

                return boss;
            }
        }

        BossDefinition fallbackBoss = reusableBossDefinitionCandidates[reusableBossDefinitionCandidates.Count - 1];

        if (usedBossKeys != null)
            usedBossKeys.Add(fallbackBoss.bossKey);

        return fallbackBoss;
    }

    private BossCatalog ResolveBossCatalog()
    {
        if (bossCatalog == null)
            bossCatalog = BossCatalog.ResolveDefault();

        return bossCatalog;
    }

    private BossDefinition FindUpcomingBossDefinition(int fromIndex)
    {
        for (int i = Mathf.Max(0, fromIndex + 1); i < runtimeNodes.Count; i++)
        {
            if (runtimeNodes[i] != null && runtimeNodes[i].IsBossNode && runtimeNodes[i].BossDefinition != null)
                return runtimeNodes[i].BossDefinition;
        }

        return null;
    }

    private BossId GetAuthoredBossFallback(int sourceIndex, BossId fallback)
    {
        if (rounds == null || sourceIndex < 0 || sourceIndex >= rounds.Count || rounds[sourceIndex] == null)
            return fallback;

        return rounds[sourceIndex].bossId != BossId.None ? rounds[sourceIndex].bossId : fallback;
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

    private bool ValidateRequiredReferences()
    {
        EnsureRunServices();

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

    private void EnsureRunServices()
    {
        if (effectController == null)
            effectController = GetComponent<RunEffectSystem>();

        if (effectController == null)
            effectController = gameObject.AddComponent<RunEffectSystem>();

        if (shopController == null)
            shopController = GetComponent<RunShopController>();

        if (shopController == null)
            shopController = gameObject.AddComponent<RunShopController>();

        if (bossPanelView != null)
            bossPanelView.SetEffectController(effectController);
    }
}
