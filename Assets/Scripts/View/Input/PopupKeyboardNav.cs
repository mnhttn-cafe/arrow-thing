using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reusable keyboard navigation for popup menus (dropdowns, context menus).
/// Drives Up/Down highlighting, Enter selection, and Escape/Left dismissal
/// via KeybindManager actions. Call <see cref="Update"/> from the owning
/// controller's Update() while the popup is open.
/// </summary>
public sealed class PopupKeyboardNav
{
    private const string HighlightClass = "custom-dropdown__item--kb-focused";

    private readonly List<VisualElement> _items = new List<VisualElement>();
    private readonly List<Action> _callbacks = new List<Action>();
    private int _highlightedIndex;
    private Action _onDismiss;
    private readonly DASRepeater _dasUp = new DASRepeater();
    private readonly DASRepeater _dasDown = new DASRepeater();

    /// <summary>True when the popup has items to navigate.</summary>
    public bool IsActive => _items.Count > 0;

    /// <summary>
    /// Set up items for keyboard navigation. Call when the popup opens.
    /// </summary>
    /// <param name="items">Visible popup items (buttons/labels).</param>
    /// <param name="callbacks">Action for each item when Enter is pressed.</param>
    /// <param name="onDismiss">Called when Escape or Left is pressed.</param>
    /// <param name="initialIndex">Initial highlighted index (default 0).</param>
    public void Open(
        List<VisualElement> items,
        List<Action> callbacks,
        Action onDismiss,
        int initialIndex = 0
    )
    {
        _items.Clear();
        _callbacks.Clear();
        _items.AddRange(items);
        _callbacks.AddRange(callbacks);
        _onDismiss = onDismiss;
        _highlightedIndex = Mathf.Clamp(initialIndex, 0, _items.Count - 1);
        _dasUp.Reset();
        _dasDown.Reset();
        ApplyHighlight();
    }

    /// <summary>Clear state when the popup closes.</summary>
    public void Close()
    {
        ClearHighlight();
        _items.Clear();
        _callbacks.Clear();
        _onDismiss = null;
    }

    /// <summary>Call every frame while the popup is open.</summary>
    public void Update()
    {
        if (_items.Count == 0)
            return;
        var km = KeybindManager.Instance;
        if (km == null)
            return;

        Vector2 nav = km.Navigate.ReadValue<Vector2>();

        if (_dasUp.Update(nav.y > 0.5f))
        {
            _highlightedIndex = Mathf.Max(0, _highlightedIndex - 1);
            ApplyHighlight();
        }
        if (_dasDown.Update(nav.y < -0.5f))
        {
            _highlightedIndex = Mathf.Min(_items.Count - 1, _highlightedIndex + 1);
            ApplyHighlight();
        }

        if (
            km.Submit.WasPerformedThisFrame()
            && _highlightedIndex >= 0
            && _highlightedIndex < _callbacks.Count
        )
            _callbacks[_highlightedIndex]?.Invoke();

        if (km.Cancel.WasPerformedThisFrame() || nav.x < -0.5f)
            _onDismiss?.Invoke();
    }

    private void ApplyHighlight()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (i == _highlightedIndex)
                _items[i].AddToClassList(HighlightClass);
            else
                _items[i].RemoveFromClassList(HighlightClass);
        }
    }

    private void ClearHighlight()
    {
        foreach (var item in _items)
            item.RemoveFromClassList(HighlightClass);
    }
}
