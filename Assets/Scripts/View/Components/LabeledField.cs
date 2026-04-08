using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reusable text input with a label above it, built from plain VisualElements.
/// Replaces Unity's built-in TextField to avoid fighting its default layout.
///
/// Keyboard flow: navigator highlights Root, Enter focuses the TextField
/// (Unity handles all text editing natively), Enter/Escape returns to navigator.
/// </summary>
public sealed class LabeledField
{
    public VisualElement Root { get; }

    /// <summary>The inner TextField.</summary>
    public TextField Input => _input;

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

    /// <summary>
    /// Called when Enter is pressed while this field is focused.
    /// Wire to the form's submit action (e.g. OnLogin).
    /// </summary>
    public Action OnSubmit { get; set; }

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

        _input.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (
                evt.keyCode == UnityEngine.KeyCode.Return
                || evt.keyCode == UnityEngine.KeyCode.KeypadEnter
            )
            {
                evt.StopPropagation();
                OnSubmit?.Invoke();
            }
        });

        // When the TextField gains focus (click or keyboard), suppress shortcuts.
        _input.RegisterCallback<FocusInEvent>(_ =>
        {
            if (KeybindManager.Instance != null)
                KeybindManager.Instance.TextFieldFocused = true;
        });

        // When the TextField loses focus, return to navigator mode.
        _input.RegisterCallback<FocusOutEvent>(_ =>
        {
            if (KeybindManager.Instance != null)
                KeybindManager.Instance.TextFieldFocused = false;
        });
    }

    /// <summary>Adds an element (e.g. a small link button) to the right side of the label row.</summary>
    public void AddToLabelRow(VisualElement el) => _labelRow.Add(el);

    public void Focus() => _input.Focus();

    /// <summary>
    /// Activate from keyboard navigation. Focuses the TextField so Unity
    /// handles all text editing natively.
    /// </summary>
    public void ActivateFromKeyboard()
    {
        if (KeybindManager.Instance != null)
            KeybindManager.Instance.TextFieldFocused = true;

        _input.schedule.Execute(() => _input.Focus());
    }

    public void RegisterValueChangedCallback(EventCallback<ChangeEvent<string>> callback) =>
        _input.RegisterValueChangedCallback(callback);
}
