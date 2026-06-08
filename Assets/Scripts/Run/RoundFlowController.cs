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

    [Header("Training Logs")]
    [SerializeField] private bool enableTrainingLogs = true;
    [SerializeField] private bool echoTrainingLogsToConsole = true;
    [SerializeField] private bool writeTrainingLogsToFile = true;
    [SerializeField] private int lossLookaheadPieces = 5;
    [SerializeField] private float noiseNormalizationMax = 100f;

    public event Action<RunGameState> StateChanged;

    public RunGameState CurrentState { get; private set; } = RunGameState.RunStart;
    public int CurrentRoundIndex => currentRoundIndex;

    private readonly RoundTrainingLogRecorder trainingLogRecorder = new();
    private int currentRoundIndex = -1;
    private bool runEnded;
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

        trainingLogRecorder.EndRun(playerLost: false);
    }

    private void Start()
    {
        StartRun();
    }

    private void Update()
    {
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
        acceptingShopInput = false;
        currentRoundIndex = -1;

        boardController.SetBoardActive(false);
        statsController.ResetRun();
        trainingLogRecorder.BeginRun(
            boardController,
            statsController,
            enableTrainingLogs,
            writeTrainingLogsToFile,
            echoTrainingLogsToConsole,
            lossLookaheadPieces,
            noiseNormalizationMax);

        if (statsHudView != null)
            statsHudView.SetStatsController(statsController);

        EnterState(RunGameState.RunStart);
        StartNextRoundInternal();
    }

    public void RequestNextRoundFromShop()
    {
        if (CurrentState != RunGameState.IntermissionShop)
            return;

        if (!acceptingShopInput)
            return;

        acceptingShopInput = false;

        if (intermissionPanelView != null)
            intermissionPanelView.SetNextRoundButtonInteractable(false);

        StartNextRoundInternal();
    }

    // kept for existing scene button bindings; new bindings should call RequestNextRoundFromShop.
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

        if (currentRoundIndex >= rounds.Count)
        {
            EnterRunComplete();
            return;
        }

        RoundDefinition round = rounds[currentRoundIndex];
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
        trainingLogRecorder.BeginRound(round);

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
        // kept as a state hook for a future pause, animation...
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
            EnterGameOver();
    }

    private void EnterRoundResolution()
    {
        if (runEnded)
            return;

        boardController.SetBoardActive(false);
        EnterState(RunGameState.RoundResolution);

        if (currentRoundIndex >= rounds.Count - 1)
        {
            EnterRunComplete();
            return;
        }

        EnterIntermissionShop();
    }

    private void EnterIntermissionShop()
    {
        acceptingShopInput = false;
        EnterState(RunGameState.IntermissionShop);

        if (intermissionPanelView != null)
            intermissionPanelView.ShowShop(statsController, false);

        if (panelSwitcher != null)
            panelSwitcher.SwitchToShop();
        else
            EnableShopInput();

        GameEvents.ShopOpened();
        Debug.Log("Enter Shop.");
    }

    private void EnterRunComplete()
    {
        runEnded = true;
        acceptingShopInput = false;

        boardController.SetBoardActive(false);
        trainingLogRecorder.EndRun(playerLost: false);
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
        trainingLogRecorder.EndRun(playerLost: true);

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
        EnterGameOver();
    }

    private void HandlePanelSwitchCompleted(GameObject shownPanel)
    {
        if (CurrentState == RunGameState.IntermissionShop)
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

    private void EnterState(RunGameState state)
    {
        CurrentState = state;
        StateChanged?.Invoke(CurrentState);
        GameEvents.RunStateChanged(CurrentState);
    }

    private void RestartFullRunForDebug()
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
