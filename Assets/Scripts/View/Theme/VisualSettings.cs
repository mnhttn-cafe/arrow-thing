using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "VisualSettings", menuName = "Arrow Thing/Visual Settings")]
public sealed class VisualSettings : ScriptableObject
{
    // ── UI Theme ──────────────────────────────────────────────────────────

    [Header("UI Theme")]
    [Tooltip(
        "StyleSheet added to every UIDocument panel at runtime. Defines all --css-variable colours. Swap for a different theme."
    )]
    public StyleSheet themeUIStyleSheet;

    // ── Board Background ──────────────────────────────────────────────────

    [Header("Board Background")]
    public Color backgroundColor = new Color(0.102f, 0.102f, 0.180f); // #1A1A2E
    public Color gridDotColor = new Color(0.180f, 0.227f, 0.349f); // #2E3A59
    public float gridDotScale = 0.15f;
    public Sprite boardDotSprite;

    // ── Arrow Appearance ──────────────────────────────────────────────────

    [Header("Arrow Appearance")]
    public Color arrowBodyColor = new Color(0.816f, 0.847f, 0.910f); // #D0D8E8
    public Color arrowHeadColor = new Color(0.816f, 0.847f, 0.910f); // #D0D8E8
    public List<Color> arrowPalette = new()
    {
        new Color(0.396f, 0.573f, 0.816f), // #6592D0 — soft blue
        new Color(0.878f, 0.718f, 0.380f), // #E0B761 — warm amber
        new Color(0.467f, 0.757f, 0.537f), // #77C189 — soft green
        new Color(0.753f, 0.576f, 0.824f), // #C093D2 — light purple
        new Color(0.659f, 0.812f, 0.812f), // #A8CFCF — soft teal
        new Color(0.816f, 0.620f, 0.475f), // #D09E79 — warm tan
    };

    [Header("Arrow Geometry")]
    public float arrowBodyWidth = 0.5f;
    public float arrowHeadLength = 0.35f;
    public float arrowHeadWidthMultiplier = 1.2f;

    // ── Materials ─────────────────────────────────────────────────────────

    [Header("Materials")]
    public Material arrowBodyMaterial;
    public Material arrowHeadMaterial;
    public Material arrowTrailMaterial;

    // ── Trail ─────────────────────────────────────────────────────────────

    [Header("Trail")]
    public Color trailColor = new Color(0.816f, 0.847f, 0.910f, 0.18f); // arrow color, low alpha

    // ── Tint ──────────────────────────────────────────────────────────────

    [Header("Blocked Tint")]
    [Range(0f, 1f)]
    public float blockedTintIntensity = 0.28f;

    // ── Reject Flash ──────────────────────────────────────────────────────

    [Header("Reject Flash")]
    public Color rejectFlashColor = new Color(1f, 0.267f, 0.267f); // #FF4444
    public float rejectFlashDuration = 0.35f;
    public AnimationCurve rejectFlashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    // ── Clear Animation ───────────────────────────────────────────────────

    [Header("Clear Animation")]
    public float clearSlideDuration = 0.4f;
    public AnimationCurve clearSlideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float pathExtensionMultiplier = 1.5f;

    // ── Bump Animation ────────────────────────────────────────────────────

    [Header("Bump Animation")]
    public float bumpSlideDuration = 0.15f;
    public AnimationCurve bumpSlideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float bumpDuration = 0.15f;
    public float bumpMagnitude = 0.15f;
    public AnimationCurve bumpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float bumpReturnDuration = 0.2f;
    public AnimationCurve bumpReturnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}
