using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PieceTypeIconView : MonoBehaviour
{
    [SerializeField] private RectTransform blockRoot;
    [SerializeField] private Image blockPrefab;
    [SerializeField] private TMP_Text typeLabel;
    [SerializeField] private float blockSize = 8f;
    [SerializeField] private float labelFontSize = 8.5f;

    private readonly List<Image> spawnedBlocks = new();
    private TetrominoType boundType;

    private void Awake()
    {
        HideTemplateBlock();
    }

    private void OnValidate()
    {
        HideTemplateBlock();
    }

    private void OnEnable()
    {
        Loc.OnLocaleChanged += HandleLocaleChanged;

        if (spawnedBlocks.Count > 0 || typeLabel != null)
            SetLabel(boundType);
    }

    private void OnDisable()
    {
        Loc.OnLocaleChanged -= HandleLocaleChanged;
    }

    public void SetBlockSize(float size)
    {
        blockSize = size;
    }

    // noise static swallowed this slot, draw junk instead of a real piece
    public void BindObscured()
    {
        HideTemplateBlock();
        Clear();
        EnsureLayout();
        EnsureLabel();
        typeLabel.text = "▒▒▒";
        typeLabel.fontSize = labelFontSize;

        // a ragged clump of gray cells where the piece silhouette would be
        for (int i = 0; i < 4; i++)
        {
            Image block = Instantiate(blockPrefab, blockRoot);
            block.gameObject.SetActive(true);
            block.color = new Color(0.35f, 0.35f, 0.38f, 1f);

            RectTransform rect = block.rectTransform;
            rect.anchoredPosition = new Vector2((i % 2) * blockSize - blockSize * 0.5f, (i / 2) * blockSize - blockSize * 0.5f);
            rect.sizeDelta = new Vector2(blockSize - 1f, blockSize - 1f);
            spawnedBlocks.Add(block);
        }
    }

    public void Bind(TetrominoType type)
    {
        boundType = type;
        HideTemplateBlock();
        Clear();
        EnsureLayout();
        SetLabel(type);

        Vector2Int[] cells = TetrominoShape.GetCells(type);
        Vector2Int min = GetMin(cells);
        Vector2Int max = GetMax(cells);
        // normalize the bounds so every piece is centered inside the same icon cell
        Vector2 centerOffset = new Vector2(
            (max.x - min.x + 1) * blockSize,
            (max.y - min.y + 1) * blockSize
        ) * 0.5f;

        Color color = GetColor(type);

        foreach (var cell in cells)
        {
            Image block = Instantiate(blockPrefab, blockRoot);
            block.gameObject.SetActive(true);
            block.color = color;

            RectTransform rect = block.rectTransform;
            Vector2Int normalized = cell - min;

            rect.anchoredPosition = new Vector2(
                normalized.x * blockSize,
                normalized.y * blockSize
            ) - centerOffset + new Vector2(blockSize, blockSize) * 0.5f;

            // tiny inset so preview blocks get the same outlined look as the board
            rect.sizeDelta = new Vector2(blockSize - 1f, blockSize - 1f);
            spawnedBlocks.Add(block);
        }
    }

    private void Clear()
    {
        foreach (var block in spawnedBlocks)
        {
            if (block != null)
                Destroy(block.gameObject);
        }

        spawnedBlocks.Clear();
    }

    private Vector2Int GetMin(Vector2Int[] cells)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;

        foreach (var cell in cells)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.y < minY) minY = cell.y;
        }

        return new Vector2Int(minX, minY);
    }

    private Vector2Int GetMax(Vector2Int[] cells)
    {
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (var cell in cells)
        {
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxY) maxY = cell.y;
        }

        return new Vector2Int(maxX, maxY);
    }

    private void HideTemplateBlock()
    {
        if (blockPrefab != null)
            blockPrefab.gameObject.SetActive(false);
    }

    private Color GetColor(TetrominoType type)
    {
        return type switch
        {
            TetrominoType.I => Color.cyan,
            TetrominoType.O => Color.yellow,
            TetrominoType.T => new Color(0.65f, 0f, 1f),
            TetrominoType.S => Color.green,
            TetrominoType.Z => Color.red,
            TetrominoType.J => Color.blue,
            TetrominoType.L => new Color(1f, 0.55f, 0f),
            _ => Color.white
        };
    }

    private void EnsureLayout()
    {
        if (blockRoot != null)
        {
            blockRoot.anchorMin = new Vector2(0f, 0.3f);
            blockRoot.anchorMax = new Vector2(1f, 1f);
            blockRoot.pivot = new Vector2(0.5f, 0.5f);
            blockRoot.anchoredPosition = Vector2.zero;
            blockRoot.sizeDelta = new Vector2(-2f, -2f);
        }

        EnsureLabel();
    }

    private void EnsureLabel()
    {
        if (typeLabel != null)
            return;

        // the prefab can omit a label
        GameObject labelObject = new("TypeLabel");
        labelObject.transform.SetParent(transform, false);
        typeLabel = labelObject.AddComponent<TextMeshProUGUI>();
        typeLabel.raycastTarget = false;

        RectTransform labelRect = typeLabel.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = new Vector2(1f, 0.3f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;

        UiTheme.Style(typeLabel, labelFontSize, FontStyles.Normal, UiTheme.TextInverse, autoSize: true);
        // lore names run longer then letter types did, shrink instead of overlapping neighbors
        typeLabel.fontSizeMin = 4f;
        typeLabel.alignment = TextAlignmentOptions.Center;
        typeLabel.textWrappingMode = TextWrappingModes.NoWrap;
        typeLabel.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void SetLabel(TetrominoType type)
    {
        EnsureLabel();
        typeLabel.text = Loc.T(GetTypeLabelKey(type));
        typeLabel.fontSize = labelFontSize;
    }

    private string GetTypeLabelKey(TetrominoType type)
    {
        // pieces go by their lore name now, not boring letter types
        return PieceLore.NameKey(PieceLore.GetProperty(type));
    }

    private void HandleLocaleChanged(string locale)
    {
        if (spawnedBlocks.Count > 0 || typeLabel != null)
            SetLabel(boundType);
    }
}
