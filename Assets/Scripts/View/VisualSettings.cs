using UnityEngine;

[CreateAssetMenu(fileName = "VisualSettings", menuName = "Arrow Thing/Visual Settings")]
public sealed class VisualSettings : ScriptableObject
{
    [Header("Colors")]
    public Color backgroundColor = new Color(0.102f, 0.102f, 0.180f);   // #1A1A2E
    public Color gridDotColor    = new Color(0.180f, 0.227f, 0.349f);   // #2E3A59
    public Color arrowBodyColor  = new Color(0.816f, 0.847f, 0.910f);   // #D0D8E8

    [Header("Sprites")]
    public Sprite? boardDotSprite;
    public Sprite? arrowHeadSprite;

    [Header("Materials")]
    public Material? arrowBodyMaterial;

    [Header("Reject Flash")]
    public Color rejectFlashColor = new Color(1f, 0.267f, 0.267f);      // #FF4444
    public float rejectFlashDuration = 0.35f;
    public AnimationCurve rejectFlashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
}
