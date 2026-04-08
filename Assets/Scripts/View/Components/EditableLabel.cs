using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A text field that looks like a plain label until activated.
/// Uses a single TextField for both states to avoid any layout shift.
///
/// Keyboard flow:
///   1. Navigator highlights the container (kb-focused on Root).
///   2. Enter → select all text, switch to editable mode.
///   3. Unity's TextField handles all text editing natively.
///   4. Enter → commit, switch back to display mode, return to navigator.
///   5. Escape → revert to original value, switch back, return to navigator.
/// </summary>
public sealed class EditableLabel
{
    public VisualElement Root { get; }

    /// <summary>The inner TextField.</summary>
    public TextField Input => _input;

    public string Value
    {
        get => _input.value;
        set => _input.SetValueWithoutNotify(value);
    }

    /// <summary>Fired when the user commits a changed value.</summary>
    public event Action<string> OnCommit;

    private readonly TextField _input;
    private bool _editing;
    private string _valueBeforeEdit;

    public EditableLabel()
    {
        Root = new VisualElement();
        Root.AddToClassList("editable-label");

        _input = new TextField { maxLength = 24 };
        _input.AddToClassList("editable-label__input");
        Root.Add(_input);

        // Start in display mode — read-only, styled as plain text.
        _input.isReadOnly = true;

        _input.RegisterCallback<ClickEvent>(_ =>
        {
            if (!_editing)
                ActivateFromKeyboard();
        });

        _input.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (!_editing)
                return;

            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                evt.StopPropagation();
                Commit();
                EndEdit();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopPropagation();
                Revert();
                EndEdit();
            }
        });

        // If focus leaves the field for any other reason (click elsewhere),
        // commit the current value. Generation check prevents stale FocusOut
        // events from a previous Blur() from killing a new edit session.
        _input.RegisterCallback<FocusOutEvent>(_ =>
        {
            // Defer so we don't race with ActivateFromKeyboard's deferred Focus.
            int gen = _editGeneration;
            _input.schedule.Execute(() =>
            {
                if (gen != _editGeneration)
                    return;
                if (_editing)
                {
                    Commit();
                    EndEdit();
                }
            });
        });
    }

    /// <summary>
    /// Activate from keyboard navigation (Enter on focused element).
    /// Switches to editable mode, selects all, gives TextField real focus.
    /// </summary>
    private int _editGeneration;

    public void ActivateFromKeyboard()
    {
        _editing = true;
        _editGeneration++;
        int gen = _editGeneration;
        _valueBeforeEdit = _input.value;
        _input.isReadOnly = false;
        Root.AddToClassList("editable-label--editing");

        if (KeybindManager.Instance != null)
            KeybindManager.Instance.TextFieldFocused = true;

        // Deferred focus — the readonly toggle needs a frame to take effect.
        // Generation check ensures a stale deferred call from a previous
        // edit cycle doesn't interfere.
        _input.schedule.Execute(() =>
        {
            if (gen != _editGeneration)
                return;
            var inner = _input.Q(className: "unity-text-field__input");
            if (inner != null)
                inner.Focus();
            else
                _input.Focus();
            _input.schedule.Execute(() =>
            {
                if (gen != _editGeneration)
                    return;
                _input.SelectAll();
            });
        });
    }

    /// <summary>Cancel any in-progress edit without committing (e.g. settings panel closing).</summary>
    public void CancelEdit()
    {
        if (!_editing)
            return;
        Revert();
        EndEdit();
    }

    private void Commit()
    {
        if (!_editing)
            return;
        var newValue = _input.value;
        if (newValue.Length >= 1 && newValue != _valueBeforeEdit)
            OnCommit?.Invoke(newValue);
        else if (newValue.Length < 1)
            _input.SetValueWithoutNotify(_valueBeforeEdit);
    }

    private void Revert()
    {
        if (!_editing)
            return;
        _input.SetValueWithoutNotify(_valueBeforeEdit);
    }

    private void EndEdit()
    {
        if (!_editing)
            return;
        _editing = false;
        _input.isReadOnly = true;
        Root.RemoveFromClassList("editable-label--editing");

        // Set TextFieldFocused false BEFORE blur so the FocusOutEvent
        // handler (which checks _editing) doesn't interfere.
        if (KeybindManager.Instance != null)
            KeybindManager.Instance.TextFieldFocused = false;

        // Only blur the outer TextField. Blurring the inner element causes
        // a deferred FocusOutEvent that can fire after the next activation,
        // immediately ending the new edit session.
        _input.Blur();
    }
}
