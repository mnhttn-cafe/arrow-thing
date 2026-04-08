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
///
/// The TextField is never set to <c>isReadOnly</c>.  On WebGL mobile, a
/// readonly HTML input suppresses the virtual keyboard even after the
/// flag is cleared, because the DOM attribute removal hasn't flushed by
/// the time <c>.focus()</c> fires.  Instead, edits while not in edit
/// mode are silently reverted via a <c>ChangeEvent</c> guard.
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

        _input.RegisterCallback<ClickEvent>(_ =>
        {
            if (!_editing)
                ActivateFromKeyboard();
        });

        // Guard: revert any change that arrives while not in edit mode.
        // This replaces isReadOnly — the field stays editable at the DOM
        // level so the mobile virtual keyboard can appear on focus.
        _input.RegisterValueChangedCallback(evt =>
        {
            if (!_editing)
                _input.SetValueWithoutNotify(evt.previousValue);
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
        Root.AddToClassList("editable-label--editing");

        if (KeybindManager.Instance != null)
            KeybindManager.Instance.TextFieldFocused = true;

        // Focus immediately so the mobile virtual keyboard appears (the
        // browser requires .focus() within the user-gesture handler).
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
