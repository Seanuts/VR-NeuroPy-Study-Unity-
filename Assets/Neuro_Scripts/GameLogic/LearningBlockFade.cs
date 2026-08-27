using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A camera-attached black overlay used while changing learning environments.
/// It is created by GameManager so the scene does not need a separate fade UI.
/// </summary>
[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
public class LearningBlockFade : MonoBehaviour
{
    CanvasGroup canvasGroup;

    public float Alpha => canvasGroup != null ? canvasGroup.alpha : 0f;

    public void Initialize(Camera targetCamera)
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = targetCamera;
        canvas.planeDistance = Mathf.Max(targetCamera.nearClipPlane + 0.01f, 0.1f);
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Image image = gameObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        RectTransform rect = transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = Mathf.Clamp01(alpha);
    }
}
