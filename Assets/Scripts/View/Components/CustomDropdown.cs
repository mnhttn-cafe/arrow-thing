using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Styled dropdown component that avoids Unity's default DropdownField popup styling.
/// Build-your-own pattern: creates a trigger button and an absolutely-positioned
/// popup injected into the panel root so it layers above all other UI.
/// </summary>
public sealed class CustomDropdown
{
    /// <summary>Add this to your layout.</summary>
    public VisualElement Root { get; }

    /// <summary>Fired when the user selects an option.</summary>
    public event Action<string> ValueChanged;

    public string Value { get; private set; }

    private readonly Label _valueLabel;
    private readonly List<string> _choices;
    private VisualElement _popup;
    private ScrollView _watchedScroll;
    private Action<float> _scrollListener;
    private EventCallback<PointerDownEvent> _outsideClickListener;

    public CustomDropdown(IReadOnlyList<string> choices, string initialValue)
    {
        _choices = new List<string>(choices);
        Value = initialValue;

        Root = new VisualElement();
        Root.AddToClassList("custom-dropdown");

        _valueLabel = new Label(initialValue);
        _valueLabel.AddToClassList("custom-dropdown__value");
        Root.Add(_valueLabel);

        var arrow = new VisualElement();
        arrow.AddToClassList("custom-dropdown__arrow");
        Root.Add(arrow);

        Root.RegisterCallback<ClickEvent>(_ => Toggle());
    }

    public void SetChoices(IReadOnlyList<string> choices)
    {
        _choices.Clear();
        _choices.AddRange(choices);
    }

    public void SetValueWithoutNotify(string value)
    {
        Value = value;
        _valueLabel.text = value;
    }

    // -- Private ---------------------------------------------------------------

    private void Toggle()
    {
        if (_popup != null)
            Close();
        else
            Open();
    }

    private void Open()
    {
        var panelRoot = Root.panel?.visualTree;
        if (panelRoot == null)
            return;

        // Copy stylesheets from the UIDocument root (direct child of panelRoot)
        // so the popup inherits component styles as well as theme variables.
        var docRoot = Root;
        while (docRoot.parent != null && docRoot.parent != panelRoot)
            docRoot = docRoot.parent;
        for (int i = 0; i < docRoot.styleSheets.count; i++)
        {
            var sheet = docRoot.styleSheets[i];
            if (!panelRoot.styleSheets.Contains(sheet))
                panelRoot.styleSheets.Add(sheet);
        }

        // Popup list — injected directly into panelRoot, no backdrop overlay
        _popup = new VisualElement();
        _popup.AddToClassList("custom-dropdown__popup");

        foreach (var choice in _choices)
        {
            var item = new Button();
            item.AddToClassList("custom-dropdown__item");
            if (choice == Value)
                item.AddToClassList("custom-dropdown__item--selected");
            item.text = choice;
            var captured = choice;
            item.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                Select(captured);
            });
            _popup.Add(item);
        }

        panelRoot.Add(_popup);

        // Position popup below trigger button after layout
        Root.schedule.Execute(() => PositionPopup(_popup));

        // Observe pointer-down on the panel in the capture phase — fires before
        // any element handles it, doesn't consume the event, so scrolling and
        // clicks pass through normally.
        _outsideClickListener = evt =>
        {
            if (
                _popup != null
                && !_popup.worldBound.Contains(evt.position)
                && !Root.worldBound.Contains(evt.position)
            )
                Close();
        };
        panelRoot.RegisterCallback(_outsideClickListener, TrickleDown.TrickleDown);

        // Close only when the trigger scrolls outside the ScrollView's viewport.
        _watchedScroll = FindAncestorScrollView(Root);
        if (_watchedScroll != null)
        {
            _scrollListener = _ =>
            {
                if (!_watchedScroll.worldBound.Overlaps(Root.worldBound))
                    Close();
                else
                    Root.schedule.Execute(() => PositionPopup(_popup));
            };
            _watchedScroll.verticalScroller.valueChanged += _scrollListener;
        }

        Root.AddToClassList("custom-dropdown--open");
    }

    private void PositionPopup(VisualElement popup)
    {
        var panelRoot = Root.panel?.visualTree;
        if (panelRoot == null)
            return;

        var triggerBounds = Root.worldBound;
        var panelBounds = panelRoot.worldBound;

        popup.style.position = Position.Absolute;
        popup.style.left = triggerBounds.xMin - panelBounds.xMin;
        popup.style.top = triggerBounds.yMax - panelBounds.yMin;
        popup.style.minWidth = triggerBounds.width;
    }

    private void Select(string choice)
    {
        Value = choice;
        _valueLabel.text = choice;
        Close();
        ValueChanged?.Invoke(choice);
    }

    public void Close()
    {
        if (_watchedScroll != null && _scrollListener != null)
        {
            _watchedScroll.verticalScroller.valueChanged -= _scrollListener;
            _watchedScroll = null;
            _scrollListener = null;
        }
        if (_outsideClickListener != null)
        {
            Root.panel?.visualTree.UnregisterCallback(
                _outsideClickListener,
                TrickleDown.TrickleDown
            );
            _outsideClickListener = null;
        }
        _popup?.RemoveFromHierarchy();
        _popup = null;
        Root.RemoveFromClassList("custom-dropdown--open");
    }

    private static ScrollView FindAncestorScrollView(VisualElement el)
    {
        var parent = el.parent;
        while (parent != null)
        {
            if (parent is ScrollView sv)
                return sv;
            parent = parent.parent;
        }
        return null;
    }
}
