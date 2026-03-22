using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Expanding ring that fades out over a short duration. Used to visualize
/// tap positions during replay playback. Managed by <see cref="TapIndicatorPool"/>.
/// </summary>
public sealed class TapIndicator : MonoBehaviour
{
    private SpriteRenderer _sr;
    private Action<TapIndicator> _onComplete;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    public void Play(
        Vector3 position,
        Color color,
        float duration,
        float maxScale,
        Action<TapIndicator> onComplete
    )
    {
        _onComplete = onComplete;
        transform.position = position;
        transform.localScale = Vector3.one * 0.1f;
        if (_sr == null)
            _sr = GetComponent<SpriteRenderer>();
        _sr.color = color;
        gameObject.SetActive(true);
        StartCoroutine(AnimateCoroutine(color, duration, maxScale));
    }

    private IEnumerator AnimateCoroutine(Color color, float duration, float maxScale)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float scale = Mathf.Lerp(0.1f, maxScale, t);
            transform.localScale = Vector3.one * scale;

            Color c = color;
            c.a = Mathf.Lerp(color.a, 0f, t * t); // quadratic fade
            _sr.color = c;

            elapsed += Time.deltaTime;
            yield return null;
        }

        gameObject.SetActive(false);
        _onComplete?.Invoke(this);
    }
}
