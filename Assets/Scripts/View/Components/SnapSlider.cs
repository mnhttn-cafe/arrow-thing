using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reusable slider row: [track+handle] [value] [- + lock?]
///
/// The lock button toggles snap-to-grid mode. When locked the slider drag
/// snaps in <see cref="_snapStep"/> increments; +/- always use <see cref="_smallStep"/>.
/// Pass showLock=false (and/or snapStep=0) to hide the lock button.
/// </summary>
public sealed class SnapSlider
{
    public VisualElement Root { get; }

    /// <summary>The track element — use for keyboard focus targeting.</summary>
    public VisualElement Track => _track;

    /// Current value. Always clamped to [min, max].
    public float Value { get; private set; }

    /// Fired whenever the value changes (drag, step buttons).
    public event Action<float> OnValueChanged;

    private readonly VisualElement _track;
    private readonly VisualElement _handle;
    private readonly Label _valueLabel;
    private readonly Button _lockBtn;
    private VisualElement _lockIcon;

    private readonly float _min;
    private readonly float _max;
    private readonly float _smallStep;
    private readonly float _snapStep;
    private readonly string _format;

    private bool _snapped;
    private bool _dragging;

    public SnapSlider(
        float min,
        float max,
        float initialValue,
        float smallStep,
        float snapStep = 0f,
        string format = "0",
        bool showLock = true
    )
    {
        _min = min;
        _max = max;
        _smallStep = smallStep;
        _snapStep = snapStep;
        _format = format;

        bool hasLock = showLock && snapStep > 0f;
        _snapped = hasLock;

        float clamped = Mathf.Clamp(initialValue, min, max);
        Value = _snapped ? SnapToGrid(clamped) : clamped;

        Root = new VisualElement();
        Root.AddToClassList("snap-slider");

        // 1. Custom track + handle + focus ring overlay.
        _track = new VisualElement();
        _track.AddToClassList("snap-slider__track");

        _handle = new VisualElement();
        _handle.AddToClassList("snap-slider__handle");
        _track.Add(_handle);

        // Focus ring: absolute overlay inside the track, no layout impact.
        var focusRing = new VisualElement();
        focusRing.AddToClassList("snap-slider__focus-ring");
        _track.Add(focusRing);

        _track.RegisterCallback<GeometryChangedEvent>(_ => UpdateHandlePosition());
        _track.RegisterCallback<PointerDownEvent>(OnTrackPointerDown);
        _track.RegisterCallback<PointerMoveEvent>(OnTrackPointerMove);
        _track.RegisterCallback<PointerUpEvent>(OnTrackPointerUp);

        Root.Add(_track);

        // 2. Value label
        _valueLabel = new Label(FormatValue(Value));
        _valueLabel.AddToClassList("snap-slider__value");
        Root.Add(_valueLabel);

        // 3. Pill button row: ( - ][ + ][ lock ) or ( - ][ + )
        var btnRow = new VisualElement();
        btnRow.AddToClassList("snap-slider__btn-row");

        var decBtn = new Button(() => Step(-1f));
        decBtn.AddToClassList("snap-slider__step-btn");
        decBtn.AddToClassList("snap-slider__btn--first");
        decBtn.text = "-";
        btnRow.Add(decBtn);

        var incBtn = new Button(() => Step(1f));
        incBtn.AddToClassList("snap-slider__step-btn");
        incBtn.text = "+";
        btnRow.Add(incBtn);

        if (hasLock)
        {
            incBtn.AddToClassList("snap-slider__btn--middle");

            _lockBtn = new Button(ToggleLock);
            _lockBtn.AddToClassList("snap-slider__lock-btn");
            _lockBtn.AddToClassList("snap-slider__btn--last");

            var lockIcon = new VisualElement();
            lockIcon.AddToClassList("snap-slider__lock-icon");
            _lockBtn.Add(lockIcon);
            _lockIcon = lockIcon;

            RefreshLockBtn();
            btnRow.Add(_lockBtn);
        }
        else
        {
            incBtn.AddToClassList("snap-slider__btn--last");
        }

        Root.Add(btnRow);
    }

    /// Set value without firing <see cref="OnValueChanged"/>.
    public void SetValueWithoutNotify(float val)
    {
        Value = Mathf.Clamp(val, _min, _max);
        _valueLabel.text = FormatValue(Value);
        UpdateHandlePosition();
    }

    // ── Drag handling ────────────────────────────────────────────────────────

    private void OnTrackPointerDown(PointerDownEvent evt)
    {
        _dragging = true;
        _track.CapturePointer(evt.pointerId);
        ApplyDrag(evt.localPosition.x);
    }

    private void OnTrackPointerMove(PointerMoveEvent evt)
    {
        if (!_dragging)
            return;
        ApplyDrag(evt.localPosition.x);
    }

    private void OnTrackPointerUp(PointerUpEvent evt)
    {
        if (!_dragging)
            return;
        _dragging = false;
        _track.ReleasePointer(evt.pointerId);
    }

    private void ApplyDrag(float localX)
    {
        float trackWidth = _track.resolvedStyle.width;
        float handleWidth = _handle.resolvedStyle.width;
        if (trackWidth <= handleWidth)
            return;

        float usable = trackWidth - handleWidth;
        float t = Mathf.Clamp01((localX - handleWidth * 0.5f) / usable);
        float raw = Mathf.Lerp(_min, _max, t);
        float val = _snapped ? SnapToGrid(raw) : raw;
        CommitValue(val);
    }

    private void UpdateHandlePosition()
    {
        if (_max <= _min) return; // Avoid NaN from InverseLerp.
        float trackWidth = _track.resolvedStyle.width;
        float handleWidth = _handle.resolvedStyle.width;
        if (float.IsNaN(trackWidth) || float.IsNaN(handleWidth) || trackWidth <= handleWidth)
            return;

        float usable = trackWidth - handleWidth;
        float t = Mathf.InverseLerp(_min, _max, Value);
        _handle.style.left = t * usable;
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void Step(float dir)
    {
        float newVal = Mathf.Clamp(Value + dir * _smallStep, _min, _max);
        CommitValue(newVal);
    }

    /// <summary>
    /// Step the slider via keyboard arrow keys. Default uses big steps
    /// (snapStep if available, otherwise smallStep * 5). When
    /// <paramref name="useSmallStep"/> is true (Shift held), uses smallStep.
    /// </summary>
    /// <param name="direction">-1 for left/decrease, +1 for right/increase.</param>
    /// <param name="useSmallStep">True when Shift is held for fine adjustment.</param>
    public void KeyboardStep(int direction, bool useSmallStep)
    {
        float step;
        if (useSmallStep)
            step = _smallStep;
        else if (_snapStep > 0f)
            step = _snapStep;
        else
            step = _smallStep * 5f;

        float newVal = Mathf.Clamp(Value + direction * step, _min, _max);
        CommitValue(newVal);
    }

    private void ToggleLock()
    {
        _snapped = !_snapped;
        RefreshLockBtn();
    }

    private void CommitValue(float val)
    {
        Value = val;
        _valueLabel.text = FormatValue(val);
        UpdateHandlePosition();
        OnValueChanged?.Invoke(val);
    }

    private float SnapToGrid(float val)
    {
        float snapped = Mathf.Round(val / _snapStep) * _snapStep;
        return Mathf.Clamp(snapped, _min, _max);
    }

    private string FormatValue(float val)
    {
        if (_format == "0")
            return Mathf.RoundToInt(val).ToString();
        return val.ToString(_format);
    }

    private void RefreshLockBtn()
    {
        if (_lockIcon == null)
            return;
        if (_snapped)
        {
            _lockIcon.AddToClassList("snap-slider__lock-icon--locked");
            _lockIcon.RemoveFromClassList("snap-slider__lock-icon--unlocked");
        }
        else
        {
            _lockIcon.RemoveFromClassList("snap-slider__lock-icon--locked");
            _lockIcon.AddToClassList("snap-slider__lock-icon--unlocked");
        }
    }
}
