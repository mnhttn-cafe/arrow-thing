using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reusable text input with a label above it, built from plain VisualElements.
/// Replaces Unity's built-in TextField to avoid fighting its default layout.
/// </summary>
public sealed class LabeledField
{
    public VisualElement Root { get; }
    public string Value
    {
        get => _input.value;
        set => _input.value = value;
    }

    public bool IsPassword
    {
        set => _input.isPasswordField = value;
    }

    private readonly VisualElement _labelRow;
    private readonly TextField _input;

    public LabeledField(string label, string name = null)
    {
        Root = new VisualElement();
        Root.AddToClassList("labeled-field");
        if (name != null)
            Root.name = name;

        _labelRow = new VisualElement();
        _labelRow.AddToClassList("labeled-field__label-row");

        var lbl = new Label(label);
        lbl.AddToClassList("labeled-field__label");
        _labelRow.Add(lbl);

        Root.Add(_labelRow);

        _input = new TextField();
        _input.AddToClassList("labeled-field__input");
        Root.Add(_input);
    }

    /// <summary>Adds an element (e.g. a small link button) to the right side of the label row.</summary>
    public void AddToLabelRow(VisualElement el) => _labelRow.Add(el);

    public void Focus() => _input.Focus();

    public void RegisterValueChangedCallback(EventCallback<ChangeEvent<string>> callback) =>
        _input.RegisterValueChangedCallback(callback);
}
