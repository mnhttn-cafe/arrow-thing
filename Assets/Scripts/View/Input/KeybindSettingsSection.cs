using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Builds the keybind rebinding UI dynamically into a container VisualElement.
/// Each rebindable action is shown as: [Label] [Current Key Button] [Reset Button].
/// Uses InputSystem's PerformInteractiveRebinding for key capture.
/// </summary>
public sealed class KeybindSettingsSection
{
    private readonly VisualElement _container;
    private readonly List<RebindRow> _rows = new List<RebindRow>();

    private struct RebindRow
    {
        public InputAction Action;
        public Button KeyButton;
        public Button ResetButton;
        public Label WarningLabel;
    }

    public KeybindSettingsSection(VisualElement container)
    {
        _container = container;
        Build();
    }

    private void Build()
    {
        _container.Clear();

        var km = KeybindManager.Instance;
        if (km == null)
            return;

        var groups = km.GetRebindableActions();
        foreach (var (groupName, actions) in groups)
        {
            // Group header.
            var header = new Label(groupName);
            header.AddToClassList("settings-keybind-group");
            _container.Add(header);

            foreach (var action in actions)
            {
                var row = CreateRow(action);
                _container.Add(row.Element);
            }
        }
    }

    private (VisualElement Element, RebindRow Row) CreateRow(InputAction action)
    {
        var root = new VisualElement();
        root.AddToClassList("settings-keybind-row");

        // Label with human-readable action name.
        var label = new Label(FormatActionName(action.name));
        label.AddToClassList("settings-keybind-label");
        root.Add(label);

        var btnRow = new VisualElement();
        btnRow.AddToClassList("settings-keybind-btn-row");

        // Current key button — click to rebind.
        var keyBtn = new Button();
        keyBtn.AddToClassList("settings-keybind-key-btn");
        keyBtn.text = KeybindManager.GetBindingDisplayString(action);
        btnRow.Add(keyBtn);

        // Reset button.
        var resetBtn = new Button();
        resetBtn.AddToClassList("icon-btn");
        resetBtn.AddToClassList("settings-keybind-reset-btn");
        var resetIcon = new VisualElement();
        resetIcon.AddToClassList("icon-btn__icon");
        resetIcon.AddToClassList("icon--refresh");
        resetBtn.Add(resetIcon);
        btnRow.Add(resetBtn);

        root.Add(btnRow);

        // Warning label (hidden by default).
        var warning = new Label();
        warning.AddToClassList("settings-keybind-warning");
        warning.AddToClassList("screen--hidden");
        root.Add(warning);

        var rebindRow = new RebindRow
        {
            Action = action,
            KeyButton = keyBtn,
            ResetButton = resetBtn,
            WarningLabel = warning,
        };
        _rows.Add(rebindRow);

        keyBtn.clicked += () => StartRebind(rebindRow);
        resetBtn.clicked += () => ResetBinding(rebindRow);

        return (root, rebindRow);
    }

    /// <summary>
    /// Returns focus items for all keybind key buttons with direct callbacks.
    /// </summary>
    /// <summary>
    /// Returns focus items for keybind rows. Each row has key button + reset button.
    /// Call <see cref="LinkNavigation"/> after SetItems to wire Right/Left between pairs.
    /// </summary>
    public List<FocusNavigator.FocusItem> GetFocusItems()
    {
        var items = new List<FocusNavigator.FocusItem>();
        foreach (var row in _rows)
        {
            var captured = row;
            items.Add(
                new FocusNavigator.FocusItem
                {
                    Element = captured.KeyButton,
                    OnActivate = () =>
                    {
                        StartRebind(captured);
                        return true;
                    },
                }
            );
            items.Add(
                new FocusNavigator.FocusItem
                {
                    Element = captured.ResetButton,
                    OnActivate = () =>
                    {
                        ResetBinding(captured);
                        return true;
                    },
                }
            );
        }
        return items;
    }

    /// <summary>
    /// Link keybind rows as a 2×N grid: Right/Left between key↔reset,
    /// Up/Down between corresponding columns across rows.
    /// </summary>
    /// <summary>
    /// Link keybind rows as a 2×N grid. <paramref name="belowIndex"/> is the
    /// item index that Down from the last row should navigate to (-1 for none).
    /// </summary>
    /// <param name="aboveIndex">Item index that Up from the first row goes to (-1 for none).</param>
    /// <param name="belowIndex">Item index that Down from the last row goes to (-1 for none).</param>
    public void LinkNavigation(
        FocusNavigator nav,
        int startIndex,
        int aboveIndex = -1,
        int belowIndex = -1
    )
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            int keyIdx = startIndex + i * 2;
            int resetIdx = keyIdx + 1;

            // Horizontal: key ↔ reset.
            nav.LinkBidi(keyIdx, FocusNavigator.NavDir.Right, resetIdx);

            // Vertical: link to same column in adjacent rows.
            if (i > 0)
            {
                int prevKeyIdx = startIndex + (i - 1) * 2;
                int prevResetIdx = prevKeyIdx + 1;
                nav.LinkBidi(prevKeyIdx, FocusNavigator.NavDir.Down, keyIdx);
                nav.LinkBidi(prevResetIdx, FocusNavigator.NavDir.Down, resetIdx);
            }
        }

        if (_rows.Count == 0)
            return;

        // Both columns of the first row share the same Up target.
        if (aboveIndex >= 0)
        {
            int firstKeyIdx = startIndex;
            int firstResetIdx = startIndex + 1;
            nav.Link(firstKeyIdx, FocusNavigator.NavDir.Up, aboveIndex);
            nav.Link(firstResetIdx, FocusNavigator.NavDir.Up, aboveIndex);
            nav.Link(aboveIndex, FocusNavigator.NavDir.Down, firstKeyIdx);
        }

        // Both columns of the last row share the same Down target.
        if (belowIndex >= 0)
        {
            int lastKeyIdx = startIndex + (_rows.Count - 1) * 2;
            int lastResetIdx = lastKeyIdx + 1;
            nav.Link(lastKeyIdx, FocusNavigator.NavDir.Down, belowIndex);
            nav.Link(lastResetIdx, FocusNavigator.NavDir.Down, belowIndex);
            nav.Link(belowIndex, FocusNavigator.NavDir.Up, lastKeyIdx);
        }
    }

    private void StartRebind(RebindRow row)
    {
        var km = KeybindManager.Instance;
        if (km == null)
            return;

        km.IsRebinding = true;
        row.KeyButton.text = "...";
        row.WarningLabel.AddToClassList("screen--hidden");

        int bindingIndex = KeybindManager.GetRebindableBindingIndex(row.Action);

        row.Action.Disable();
        var operation = row
            .Action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op =>
            {
                op.Dispose();
                row.Action.Enable();
                km.IsRebinding = false;

                string newPath = row.Action.bindings[bindingIndex].effectivePath;
                row.KeyButton.text = row.Action.GetBindingDisplayString(bindingIndex);

                // Check for conflicts.
                var conflicts = km.FindConflicts(row.Action, newPath);
                if (conflicts.Count > 0)
                {
                    row.WarningLabel.text = $"Also bound to: {conflicts[0].actionName}";
                    row.WarningLabel.RemoveFromClassList("screen--hidden");
                }

                km.SaveBindingOverrides();
                RefreshAllDisplayStrings();
            })
            .OnCancel(op =>
            {
                op.Dispose();
                row.Action.Enable();
                km.IsRebinding = false;
                row.KeyButton.text = KeybindManager.GetBindingDisplayString(row.Action);
            });

        operation.Start();
    }

    private void ResetBinding(RebindRow row)
    {
        var km = KeybindManager.Instance;
        if (km == null)
            return;

        km.ResetBindingsForAction(row.Action);
        row.KeyButton.text = KeybindManager.GetBindingDisplayString(row.Action);
        row.WarningLabel.AddToClassList("screen--hidden");
        RefreshAllDisplayStrings();
    }

    private void RefreshAllDisplayStrings()
    {
        foreach (var row in _rows)
            row.KeyButton.text = KeybindManager.GetBindingDisplayString(row.Action);
    }

    private static string FormatActionName(string name)
    {
        // Insert spaces before capitals: "QuickReset" → "Quick Reset"
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                sb.Append(' ');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }
}
