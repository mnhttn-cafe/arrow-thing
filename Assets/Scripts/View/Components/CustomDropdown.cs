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
    private VisualElement _backdrop;

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
        if (_backdrop != null)
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

        // Backdrop: fills the whole panel, captures outside clicks and scrolls
        _backdrop = new VisualElement();
        _backdrop.AddToClassList("custom-dropdown__backdrop");
        _backdrop.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.target == _backdrop)
            {
                evt.StopPropagation();
                Close();
            }
        });
        _backdrop.RegisterCallback<WheelEvent>(_ => Close());

        // Popup list
        var popup = new VisualElement();
        popup.AddToClassList("custom-dropdown__popup");

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
            popup.Add(item);
        }

        _backdrop.Add(popup);
        panelRoot.Add(_backdrop);

        // Position popup below trigger button after layout
        Root.schedule.Execute(() => PositionPopup(popup));

        Root.AddToClassList("custom-dropdown--open");
    }

    private void PositionPopup(VisualElement popup)
    {
        var panelRoot = Root.panel?.visualTree;
        if (panelRoot == null)
            return;

        // Convert trigger button rect to panel root local space
        var triggerBounds = Root.worldBound;
        float scale =
            panelRoot.resolvedStyle.width > 0 ? panelRoot.resolvedStyle.width / Screen.width : 1f;
        // worldBound is in screen pixels; panel uses logical pixels (may differ by DPI scale)
        // Use the panel's scale to convert
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
        _backdrop?.RemoveFromHierarchy();
        _backdrop = null;
        Root.RemoveFromClassList("custom-dropdown--open");
    }
}
