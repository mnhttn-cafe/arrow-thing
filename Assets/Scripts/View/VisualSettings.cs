using UnityEngine;

[CreateAssetMenu(fileName = "VisualSettings", menuName = "Arrow Thing/Visual Settings")]
public sealed class VisualSettings : ScriptableObject
{
    [Header("Colors")]
    public Color backgroundColor = new Color(0.102f, 0.102f, 0.180f); // #1A1A2E
    public Color gridDotColor = new Color(0.180f, 0.227f, 0.349f); // #2E3A59
    public Color arrowBodyColor = new Color(0.816f, 0.847f, 0.910f); // #D0D8E8

    [Header("Sprites")]
    public Sprite boardDotSprite;

    [Header("Arrow Geometry")]
    public float arrowBodyWidth = 0.5f;
    public float arrowHeadLength = 0.35f;
    public float arrowHeadWidthMultiplier = 1.2f;

    [Header("Grid")]
    public float gridDotScale = 0.15f;

    [Header("Materials")]
    public Material arrowBodyMaterial;
    public Material arrowHeadMaterial;

    [Header("Arrow Head Color")]
    public Color arrowHeadColor = new Color(0.816f, 0.847f, 0.910f); // #D0D8E8

    [Header("Reject Flash")]
    public Color rejectFlashColor = new Color(1f, 0.267f, 0.267f); // #FF4444
    public float rejectFlashDuration = 0.35f;
    public AnimationCurve rejectFlashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Clear Animation")]
    public float clearSlideDuration = 0.4f;
    public AnimationCurve clearSlideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float pathExtensionMultiplier = 1.5f;

    [Header("Bump Animation")]
    public float bumpSlideDuration = 0.15f;
    public AnimationCurve bumpSlideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float bumpDuration = 0.15f;
    public float bumpMagnitude = 0.15f;
    public AnimationCurve bumpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float bumpReturnDuration = 0.2f;
    public AnimationCurve bumpReturnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}
