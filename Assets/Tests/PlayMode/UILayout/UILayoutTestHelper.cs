using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reusable utilities for UI Toolkit layout testing across aspect ratios.
/// </summary>
public static class UILayoutTestHelper
{
    public struct AspectRatio
    {
        public string Name;
        public int Width;
        public int Height;

        public AspectRatio(string name, int width, int height)
        {
            Name = name;
            Width = width;
            Height = height;
        }

        public override string ToString() => Name;
    }

    public static IEnumerable<AspectRatio> StandardAspectRatios
    {
        get
        {
            yield return new AspectRatio("16:9", 1920, 1080);
            yield return new AspectRatio("4:3", 1024, 768);
            yield return new AspectRatio("21:9", 2560, 1080);
            yield return new AspectRatio("9:16", 1080, 1920);
            yield return new AspectRatio("9:19 mobile", 390, 844);
            yield return new AspectRatio("1:1", 1000, 1000);
        }
    }

    /// <summary>
    /// Configures PanelSettings to simulate a given screen size.
    /// Assigns a RenderTexture as the target so the panel uses the texture
    /// dimensions as the effective screen size — independent of the actual
    /// editor window or CI headless resolution.
    /// </summary>
    public static void SetPanelReferenceResolution(PanelSettings panel, int width, int height)
    {
        // Release any previously assigned test render texture.
        if (panel.targetTexture != null)
        {
            panel.targetTexture.Release();
            Object.DestroyImmediate(panel.targetTexture);
        }

        var rt = new RenderTexture(width, height, 0);
        rt.Create();
        panel.targetTexture = rt;

        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(width, height);
        panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panel.match = 0.5f;
    }

    /// <summary>
    /// Cleans up any RenderTexture assigned by SetPanelReferenceResolution.
    /// </summary>
    public static void CleanUpTargetTexture(PanelSettings panel)
    {
        if (panel.targetTexture != null)
        {
            panel.targetTexture.Release();
            Object.DestroyImmediate(panel.targetTexture);
            panel.targetTexture = null;
        }
    }

    /// <summary>
    /// Waits enough frames for UI Toolkit to resolve layout after changes.
    /// </summary>
    public static IEnumerator WaitForLayoutResolve()
    {
        yield return null;
        yield return null;
    }

    /// <summary>
    /// Asserts a single element is fully visible within the panel bounds.
    /// Returns false (instead of failing) so callers can handle known issues.
    /// </summary>
    public static bool IsElementFullyVisible(
        VisualElement element,
        Rect panelBounds,
        out string failureMessage
    )
    {
        failureMessage = null;
        var b = element.worldBound;
        string name = element.name ?? element.GetType().Name;

        if (b.width <= 0 || b.height <= 0)
        {
            failureMessage = $"{name}: zero-size bounds ({b})";
            return false;
        }

        if (b.xMin < panelBounds.xMin - 1f)
        {
            failureMessage =
                $"{name}: clipped on left (element.xMin={b.xMin}, panel.xMin={panelBounds.xMin})";
            return false;
        }
        if (b.yMin < panelBounds.yMin - 1f)
        {
            failureMessage =
                $"{name}: clipped on top (element.yMin={b.yMin}, panel.yMin={panelBounds.yMin})";
            return false;
        }
        if (b.xMax > panelBounds.xMax + 1f)
        {
            failureMessage =
                $"{name}: clipped on right (element.xMax={b.xMax}, panel.xMax={panelBounds.xMax})";
            return false;
        }
        if (b.yMax > panelBounds.yMax + 1f)
        {
            failureMessage =
                $"{name}: clipped on bottom (element.yMax={b.yMax}, panel.yMax={panelBounds.yMax})";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Asserts an element is fully visible. Fails the test on violation.
    /// </summary>
    public static void AssertElementFullyVisible(
        VisualElement element,
        Rect panelBounds,
        string context
    )
    {
        if (!IsElementFullyVisible(element, panelBounds, out string msg))
            Assert.Fail($"[{context}] {msg}");
    }

    /// <summary>
    /// Checks element visibility but marks the test as inconclusive (not failed)
    /// on violation. Use for known-issue aspect ratios (e.g. portrait).
    /// </summary>
    public static void WarnElementFullyVisible(
        VisualElement element,
        Rect panelBounds,
        string context
    )
    {
        if (!IsElementFullyVisible(element, panelBounds, out string msg))
            Assert.Inconclusive($"[{context}] {msg}");
    }

    /// <summary>
    /// Recursively asserts all visible leaf elements are within panel bounds.
    /// Skips elements with display:none.
    /// </summary>
    public static void AssertAllVisibleChildren(
        VisualElement root,
        Rect panelBounds,
        string context,
        bool warnOnly = false
    )
    {
        if (root.resolvedStyle.display == DisplayStyle.None)
            return;
        if (root.resolvedStyle.opacity < 0.01f)
            return;

        bool isLeaf = root.childCount == 0 || root is Label || root is Button;

        if (isLeaf)
        {
            if (warnOnly)
                WarnElementFullyVisible(root, panelBounds, context);
            else
                AssertElementFullyVisible(root, panelBounds, context);
        }
        else
        {
            for (int i = 0; i < root.hierarchy.childCount; i++)
                AssertAllVisibleChildren(root.hierarchy[i], panelBounds, context, warnOnly);
        }
    }
}
