using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SprayPreview2D : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer previewRenderer;

    private void Awake()
    {
        if (previewRenderer != null)
        {
            previewRenderer.enabled = false;
        }
    }

    public void OnSpray(InputValue value)
    {
        if (previewRenderer != null)
        {
            previewRenderer.enabled = value.isPressed;
        }
    }
}
