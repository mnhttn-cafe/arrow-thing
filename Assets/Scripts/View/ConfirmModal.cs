using System;
using UnityEngine.UIElements;

/// <summary>
/// Wraps a ConfirmModal.uxml template instance and configures its content.
/// The template provides the structure; this class sets text, styling, and callbacks.
/// </summary>
public class ConfirmModal
{
    private readonly VisualElement _root;
    private readonly VisualElement _overlay;
    private readonly Button _confirmBtn;
    private readonly Button _cancelBtn;

    public event Action Confirmed;
    public event Action Cancelled;

    public ConfirmModal(
        VisualElement root,
        string title,
        string confirmText,
        string cancelText,
        string subtitle = null,
        bool isDanger = false
    )
    {
        _root = root;
        _overlay = root.Q(className: "modal-overlay");

        root.Q<Label>("modal-title").text = title;

        if (subtitle != null)
        {
            var sub = root.Q<Label>("modal-subtitle");
            sub.text = subtitle;
            sub.RemoveFromClassList("screen--hidden");
        }

        _confirmBtn = root.Q<Button>("modal-confirm-btn");
        _confirmBtn.text = confirmText;
        if (isDanger)
            _confirmBtn.AddToClassList("menu-btn--danger");

        _cancelBtn = root.Q<Button>("modal-cancel-btn");
        _cancelBtn.text = cancelText;

        _confirmBtn.clicked += () => Confirmed?.Invoke();
        _cancelBtn.clicked += () => Cancelled?.Invoke();
    }

    public void Show()
    {
        _root.style.display = DisplayStyle.Flex;
        _overlay.RemoveFromClassList("screen--hidden");
    }

    public void Hide()
    {
        _overlay.AddToClassList("screen--hidden");
        _root.style.display = DisplayStyle.None;
    }
}
