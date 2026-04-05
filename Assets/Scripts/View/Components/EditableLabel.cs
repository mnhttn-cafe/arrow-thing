using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A text field that looks like a plain label until clicked.
/// Uses a single TextField for both states to avoid any layout shift.
/// Commits on blur, Enter, or Escape. Auto-selects text on edit start.
/// </summary>
public sealed class EditableLabel
{
    public VisualElement Root { get; }

    public string Value
    {
        get => _input.value;
        set => _input.SetValueWithoutNotify(value);
    }

    /// <summary>Fired when the user commits a changed value.</summary>
    public event Action<string> OnCommit;

    private readonly TextField _input;
    private bool _editing;
    private bool _commitInProgress;
    private string _valueBeforeEdit;

    public EditableLabel()
    {
        Root = new VisualElement();
        Root.AddToClassList("editable-label");

        _input = new TextField { maxLength = 24 };
        _input.AddToClassList("editable-label__input");
        Root.Add(_input);

        // Start in display mode — read-only, styled as plain text
        _input.isReadOnly = true;

        _input.RegisterCallback<ClickEvent>(_ =>
        {
            if (!_editing)
                BeginEdit();
        });
        _input.RegisterCallback<FocusOutEvent>(_ => Commit());
        _input.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (!_editing)
                return;
            if (
                evt.keyCode == KeyCode.Return
                || evt.keyCode == KeyCode.KeypadEnter
                || evt.keyCode == KeyCode.Escape
            )
            {
                _input.Blur();
            }
        });
    }

    /// <summary>Cancel any in-progress edit without committing.</summary>
    public void CancelEdit()
    {
        if (!_editing)
            return;
        _editing = false;
        _input.SetValueWithoutNotify(_valueBeforeEdit);
        _input.isReadOnly = true;
        Root.RemoveFromClassList("editable-label--editing");
    }

    private void BeginEdit()
    {
        _editing = true;
        _valueBeforeEdit = _input.value;
        _input.isReadOnly = false;
        Root.AddToClassList("editable-label--editing");

        _input.Focus();
        _input.schedule.Execute(() => _input.SelectAll());
    }

    private void Commit()
    {
        if (!_editing || _commitInProgress)
            return;
        _commitInProgress = true;

        _editing = false;
        _input.isReadOnly = true;
        Root.RemoveFromClassList("editable-label--editing");

        var newValue = _input.value;
        if (newValue.Length >= 1 && newValue != _valueBeforeEdit)
        {
            OnCommit?.Invoke(newValue);
        }
        else if (newValue.Length < 1)
        {
            _input.SetValueWithoutNotify(_valueBeforeEdit);
        }

        _commitInProgress = false;
    }
}
