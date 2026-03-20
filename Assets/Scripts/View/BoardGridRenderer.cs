using System.Collections;
using UnityEngine;

/// <summary>
/// Renders a dotted background grid for the board using a single quad
/// with a tiling texture. One GameObject instead of W×H sprites.
/// </summary>
public sealed class BoardGridRenderer : MonoBehaviour
{
    private MeshRenderer _renderer;
    private Material _material;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>
    /// Creates a single tiling quad covering the board.
    /// The dot texture is tiled once per cell via material tiling.
    /// </summary>
    public void Init(Board board, VisualSettings settings)
    {
        if (settings.boardDotSprite == null)
        {
            Debug.LogWarning(
                "BoardGridRenderer: boardDotSprite is not assigned in VisualSettings."
            );
            return;
        }

        float w = board.Width;
        float h = board.Height;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "GridQuad";
        go.transform.SetParent(transform, false);

        // Board cells span (0,0) to (W-1, H-1); center the quad
        go.transform.localPosition = new Vector3((w - 1) * 0.5f, (h - 1) * 0.5f, 0f);
        go.transform.localScale = new Vector3(w, h, 1f);

        // Bake tiling into UVs — Sprites/Default ignores _MainTex_ST
        var mesh = go.GetComponent<MeshFilter>().mesh;
        var uvs = mesh.uv;
        for (int i = 0; i < uvs.Length; i++)
            uvs[i] = new Vector2(uvs[i].x * w, uvs[i].y * h);
        mesh.uv = uvs;

        _renderer = go.GetComponent<MeshRenderer>();
        _renderer.sortingOrder = -1;

        _material = new Material(Shader.Find("Sprites/Default"))
        {
            mainTexture = settings.boardDotSprite.texture,
        };
        _material.SetColor(ColorId, settings.gridDotColor);

        _renderer.material = _material;
    }

    /// <summary>
    /// Fades the grid to transparent over the given duration.
    /// </summary>
    public void FadeOut(float duration, System.Action onComplete = null)
    {
        StartCoroutine(FadeOutCoroutine(duration, onComplete));
    }

    private IEnumerator FadeOutCoroutine(float duration, System.Action onComplete)
    {
        if (_material == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        Color startColor = _material.GetColor(ColorId);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Color c = startColor;
            c.a = startColor.a * (1f - t);
            _material.SetColor(ColorId, c);
            yield return null;
        }
        onComplete?.Invoke();
    }
}
