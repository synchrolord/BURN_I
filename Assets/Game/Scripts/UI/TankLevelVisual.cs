using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class TankLevelVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private SpriteRenderer targetRenderer;

    [Tooltip("Place sprites from emptiest to fullest.")]
    [SerializeField]
    private Sprite[] levelSprites;

    [Header("Editor Preview")]
    [SerializeField, Range(0f, 1f)]
    private float previewFill = 1f;

    private void Awake()
    {
        CacheRenderer();
        RefreshVisual();
    }

    private void OnValidate()
    {
        CacheRenderer();
        RefreshVisual();
    }

    public void SetAmount(float currentAmount, float maximumAmount)
    {
        float normalized = maximumAmount > 0f
            ? currentAmount / maximumAmount
            : 0f;

        SetNormalized(normalized);
    }

    public void SetNormalized(float normalizedAmount)
    {
        previewFill = Mathf.Clamp01(normalizedAmount);
        RefreshVisual();
    }

    private void CacheRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void RefreshVisual()
    {
        if (targetRenderer == null ||
            levelSprites == null ||
            levelSprites.Length == 0)
        {
            return;
        }

        int index = Mathf.RoundToInt(
            previewFill * (levelSprites.Length - 1)
        );

        index = Mathf.Clamp(
            index,
            0,
            levelSprites.Length - 1
        );

        targetRenderer.sprite = levelSprites[index];
    }
}