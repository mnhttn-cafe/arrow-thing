using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reusable slider row: [slider] [value] [+/-] [lock (optional)]
///
/// The lock button toggles snap-to-grid mode. When locked the slider and step buttons
/// both move in <see cref="snapStep"/> increments; when free they use <see cref="smallStep"/>.
/// Pass showLock=false (and/or snapStep=0) to hide the lock button — used for settings
/// sliders that have no coarse grid.
/// </summary>
public sealed class SnapSlider
{
    public VisualElement Root { get; }

    /// Current value. Always clamped to [min, max].
    public float Value { get; private set; }

    /// Fired whenever the value changes (drag, step buttons, lock snap).
    public event Action<float> OnValueChanged;

    private readonly Slider _slider;
    private readonly Label _valueLabel;
    private readonly Button _lockBtn;

    private readonly float _min;
    private readonly float _max;
    private readonly float _smallStep;
    private readonly float _snapStep;
    private readonly string _format;

    private bool _snapped;

    /// <param name="min">Minimum value.</param>
    /// <param name="max">Maximum value.</param>
    /// <param name="initialValue">Starting value (snapped if lock starts active).</param>
    /// <param name="smallStep">Step for +/- in free mode.</param>
    /// <param name="snapStep">Coarse snap increment. 0 = no lock button.</param>
    /// <param name="format">"0" = integer display; "F1"/"F2" = decimal.</param>
    /// <param name="showLock">Show the lock/unlock button (requires snapStep > 0).</param>
    public SnapSlider(
        float min, float max, float initialValue, float smallStep,
        float snapStep = 0f, string format = "0", bool showLock = true)
    {
        _min = min;
        _max = max;
        _smallStep = smallStep;
        _snapStep = snapStep;
        _format = format;

        bool hasLock = showLock && snapStep > 0f;
        _snapped = hasLock; // start locked when a snap grid is defined

        float clamped = Mathf.Clamp(initialValue, min, max);
        Value = _snapped ? SnapToGrid(clamped) : clamped;

        Root = new VisualElement();
        Root.AddToClassList("snap-slider");

        // 1. Slider
        _slider = new Slider(min, max);
        _slider.AddToClassList("snap-slider__slider");
        _slider.SetValueWithoutNotify(Value);
        _slider.RegisterValueChangedCallback(OnSliderChanged);
        Root.Add(_slider);

        // 2. Value label
        _valueLabel = new Label(FormatValue(Value));
        _valueLabel.AddToClassList("snap-slider__value");
        Root.Add(_valueLabel);

        // 3. Step buttons — vertical column, + on top
        var btnCol = new VisualElement();
        btnCol.AddToClassList("snap-slider__btn-column");

        var incBtn = new Button(() => Step(1f));
        incBtn.AddToClassList("snap-slider__step-btn");
        incBtn.text = "+";
        btnCol.Add(incBtn);

        var decBtn = new Button(() => Step(-1f));
        decBtn.AddToClassList("snap-slider__step-btn");
        decBtn.text = "\u2212"; // − (proper minus, not hyphen)
        btnCol.Add(decBtn);

        Root.Add(btnCol);

        // 4. Lock/unlock button (optional)
        if (hasLock)
        {
            _lockBtn = new Button(ToggleLock);
            _lockBtn.AddToClassList("snap-slider__lock-btn");
            RefreshLockBtn();
            Root.Add(_lockBtn);
        }
    }

    /// Set value without firing <see cref="OnValueChanged"/>.
    /// Does not snap even when in locked mode — used to restore saved state.
    public void SetValueWithoutNotify(float val)
    {
        Value = Mathf.Clamp(val, _min, _max);
        _slider.SetValueWithoutNotify(Value);
        _valueLabel.text = FormatValue(Value);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void OnSliderChanged(ChangeEvent<float> evt)
    {
        float val = _snapped ? SnapToGrid(evt.newValue) : evt.newValue;
        Value = val;
        // Correct slider thumb position when snap moved the value.
        if (Math.Abs(val - evt.newValue) > 0.0001f)
            _slider.SetValueWithoutNotify(val);
        _valueLabel.text = FormatValue(val);
        OnValueChanged?.Invoke(val);
    }

    private void Step(float dir)
    {
        float step = _snapped ? _snapStep : _smallStep;
        float newVal = Mathf.Clamp(Value + dir * step, _min, _max);
        if (_snapped) newVal = SnapToGrid(newVal);
        CommitValue(newVal);
    }

    private void ToggleLock()
    {
        _snapped = !_snapped;
        RefreshLockBtn();
        if (_snapped)
            CommitValue(SnapToGrid(Value));
    }

    private void CommitValue(float val)
    {
        Value = val;
        _slider.SetValueWithoutNotify(val);
        _valueLabel.text = FormatValue(val);
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
        if (_lockBtn == null) return;
        // ■ = locked/snapped, □ = free  (U+25A0 / U+25A1, widely available in fonts)
        _lockBtn.text = _snapped ? "\u25a0" : "\u25a1";
        if (_snapped)
            _lockBtn.AddToClassList("snap-slider__lock-btn--active");
        else
            _lockBtn.RemoveFromClassList("snap-slider__lock-btn--active");
    }
}
