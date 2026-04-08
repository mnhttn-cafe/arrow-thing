using UnityEngine;

/// <summary>
/// Delayed Auto Shift (DAS) repeater. Fires once on initial press, waits for
/// an initial delay, then repeats at a faster interval while held. Models the
/// behavior of keyboard repeat in rhythm games and puzzle UIs.
/// </summary>
public sealed class DASRepeater
{
    private readonly float _initialDelay;
    private readonly float _repeatInterval;

    private bool _held;
    private float _holdTime;
    private float _nextFireTime;

    /// <param name="initialDelay">Seconds before first repeat fires (default 0.4s).</param>
    /// <param name="repeatInterval">Seconds between repeats after initial delay (default 0.05s).</param>
    public DASRepeater(float initialDelay = 0.4f, float repeatInterval = 0.05f)
    {
        _initialDelay = initialDelay;
        _repeatInterval = repeatInterval;
    }

    /// <summary>True if the last fire was the initial press (not a DAS repeat).</summary>
    public bool WasInitialPress { get; private set; }

    /// <summary>
    /// Call every frame. Returns true on the initial press and on each DAS repeat.
    /// </summary>
    /// <param name="pressed">True if the key is currently held down.</param>
    public bool Update(bool pressed)
    {
        WasInitialPress = false;

        if (!pressed)
        {
            _held = false;
            _holdTime = 0f;
            return false;
        }

        if (!_held)
        {
            // Initial press fires immediately regardless of frame rate — standard
            // DAS behavior (like keyboard repeat: instant first character, then delay).
            _held = true;
            _holdTime = 0f;
            _nextFireTime = _initialDelay;
            WasInitialPress = true;
            return true;
        }

        _holdTime += Time.unscaledDeltaTime;
        if (_holdTime >= _nextFireTime)
        {
            _nextFireTime += _repeatInterval;
            return true;
        }

        return false;
    }

    /// <summary>Reset state (e.g. when focus changes).</summary>
    public void Reset()
    {
        _held = false;
        _holdTime = 0f;
    }

    /// <summary>
    /// Mark the key as already held without firing. The next Update(true) won't
    /// treat it as a new press. Use after a sub-component closes while the key
    /// is still physically held.
    /// </summary>
    public void Suppress()
    {
        _held = true;
        _holdTime = 0f;
        _nextFireTime = _initialDelay;
    }
}
