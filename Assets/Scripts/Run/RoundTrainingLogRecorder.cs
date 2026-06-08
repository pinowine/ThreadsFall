using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class RoundTrainingLogRecorder
{
    // emits one JSONL sample per locked piece; loss labels resolve after a short lookahead window.
    private const string LogFolderName = "TrainingLogs";
    private const string FallbackRoundPrefix = "round_";

    private readonly List<RoundTrainingLogEntry> pendingEntries = new();

    private TetrisBoardController boardController;
    private RunStatsController statsController;
    private RoundDefinition currentRound;

    private bool isRecording;
    private bool writeToFile;
    private bool echoToConsole;
    private int lossLookaheadPieces;
    private float noiseNormalizationMax;
    private string sessionFilePath;

    private int pieceIndex;
    private int piecesSinceLastClear;
    private int hardDropLocks;
    private int softDropInputs;
    private int actionInputs;
    private int rotationInputs;
    private float totalPlacementTime;

    public void BeginRun(
        TetrisBoardController board,
        RunStatsController stats,
        bool enabled,
        bool shouldWriteToFile,
        bool shouldEchoToConsole,
        int lookaheadPieces,
        float noiseMax)
    {
        EndRun(playerLost: false);

        boardController = board;
        statsController = stats;
        writeToFile = shouldWriteToFile;
        echoToConsole = shouldEchoToConsole;
        lossLookaheadPieces = Mathf.Max(0, lookaheadPieces);
        noiseNormalizationMax = Mathf.Max(1f, noiseMax);
        isRecording = enabled && boardController != null && statsController != null;

        if (!isRecording)
            return;

        boardController.PiecePlaced += HandlePiecePlaced;
        sessionFilePath = CreateSessionFilePath();
    }

    public void BeginRound(RoundDefinition round)
    {
        if (!isRecording)
            return;

        currentRound = round;
        pieceIndex = 0;
        piecesSinceLastClear = 0;
        hardDropLocks = 0;
        softDropInputs = 0;
        actionInputs = 0;
        rotationInputs = 0;
        totalPlacementTime = 0f;
    }

    public void EndRun(bool playerLost)
    {
        if (playerLost)
            MarkPendingEntriesAsLoss();

        FlushAllPendingEntries();
        UnsubscribeFromBoard();

        isRecording = false;
        currentRound = null;
        boardController = null;
        statsController = null;
        sessionFilePath = null;
    }

    private void HandlePiecePlaced(TetrisPiecePlacementInfo placement)
    {
        if (!isRecording || currentRound == null)
            return;

        pieceIndex++;

        if (placement.UsedHardDrop)
            hardDropLocks++;

        softDropInputs += Mathf.Max(0, placement.SoftDropInputCount);
        rotationInputs += Mathf.Max(0, placement.RotationInputCount);
        actionInputs += Mathf.Max(0, placement.MoveInputCount);
        actionInputs += Mathf.Max(0, placement.RotationInputCount);
        actionInputs += placement.UsedHardDrop ? 1 : 0;
        totalPlacementTime += Mathf.Max(0f, placement.TimeToPlace);

        piecesSinceLastClear = placement.LinesCleared > 0 ? 0 : piecesSinceLastClear + 1;

        TetrisBoardMetrics boardMetrics = boardController.CaptureMetrics();
        RoundTrainingLogEntry entry = BuildEntry(boardMetrics);

        // keep the last N samples pending so the future-loss label is known before export.
        pendingEntries.Add(entry);
        FlushResolvedEntries();
    }

    private RoundTrainingLogEntry BuildEntry(TetrisBoardMetrics boardMetrics)
    {
        int totalRoundPieces = currentRound.pieces != null ? currentRound.pieces.Count : 0;
        float roundProgress = totalRoundPieces > 0 ? Mathf.Clamp01((float)pieceIndex / totalRoundPieces) : 1f;
        float currentNoise = Mathf.Clamp01(statsController.Noise / noiseNormalizationMax);

        return new RoundTrainingLogEntry
        {
            roundId = GetRoundId(),
            pieceIndex = pieceIndex,
            boardHeightRatio = RoundMetric(boardMetrics.BoardHeightRatio),
            holesRatio = RoundMetric(boardMetrics.HolesRatio),
            bumpiness = boardMetrics.Bumpiness,
            deepestWell = boardMetrics.DeepestWell,
            piecesSinceLastClear = piecesSinceLastClear,
            hardDropRate = RoundMetric(SafeDivide(hardDropLocks, pieceIndex)),
            softDropRate = RoundMetric(SafeDivide(softDropInputs, actionInputs)),
            rotationRate = RoundMetric(SafeDivide(rotationInputs, pieceIndex)),
            averageTimeToPlace = RoundMetric(SafeDivide(totalPlacementTime, pieceIndex)),
            currentNoise = RoundMetric(currentNoise),
            currentComposure = statsController.Composure,
            roundProgress = RoundMetric(roundProgress),
            playerLostWithinNextNPieces = false,
            manualPressureLabel = RoundMetric(Mathf.Clamp01(currentRound.manualPressureLabel))
        };
    }

    private void FlushResolvedEntries()
    {
        while (pendingEntries.Count > lossLookaheadPieces)
        {
            FlushEntry(pendingEntries[0]);
            pendingEntries.RemoveAt(0);
        }
    }

    private void FlushAllPendingEntries()
    {
        for (int i = 0; i < pendingEntries.Count; i++)
        {
            FlushEntry(pendingEntries[i]);
        }

        pendingEntries.Clear();
    }

    private void MarkPendingEntriesAsLoss()
    {
        // only entries still inside the lookahead window can truthfully become positive labels
        for (int i = 0; i < pendingEntries.Count; i++)
        {
            pendingEntries[i].playerLostWithinNextNPieces = true;
        }
    }

    private void FlushEntry(RoundTrainingLogEntry entry)
    {
        string json = JsonUtility.ToJson(entry);

        if (echoToConsole)
            Debug.Log(json);

        if (!writeToFile || string.IsNullOrEmpty(sessionFilePath))
            return;

        try
        {
            File.AppendAllText(sessionFilePath, json + Environment.NewLine);
        }
        catch (Exception exception)
        {
            writeToFile = false;
            Debug.LogWarning($"Training log write failed: {exception.Message}");
        }
    }

    private string CreateSessionFilePath()
    {
        if (!writeToFile)
            return null;

        try
        {
            string folderPath = Path.Combine(Application.persistentDataPath, LogFolderName);
            Directory.CreateDirectory(folderPath);

            string fileName = $"round_training_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl";
            return Path.Combine(folderPath, fileName);
        }
        catch (Exception exception)
        {
            writeToFile = false;
            Debug.LogWarning($"Training log folder setup failed: {exception.Message}");
            return null;
        }
    }

    private void UnsubscribeFromBoard()
    {
        if (boardController != null)
            boardController.PiecePlaced -= HandlePiecePlaced;
    }

    private string GetRoundId()
    {
        if (!string.IsNullOrWhiteSpace(currentRound.roundId))
            return currentRound.roundId;

        return FallbackRoundPrefix + statsController.CurrentRoundIndex.ToString("00");
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        return denominator > 0f ? numerator / denominator : 0f;
    }

    private static float RoundMetric(float value)
    {
        return Mathf.Round(value * 1000f) / 1000f;
    }
}
