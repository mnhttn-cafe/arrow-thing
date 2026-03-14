using System.Collections;
using UnityEngine;

/// <summary>
/// Visual representation of a single arrow. Owns the procedural body mesh
/// and arrowhead sprite. Handles reject flash animation.
/// </summary>
public sealed class ArrowView : MonoBehaviour
{
    private MeshFilter _meshFilter = null!;
    private MeshRenderer _meshRenderer = null!;
    private Material _materialInstance = null!;
    private VisualSettings _settings = null!;
    private static readonly int FlashTId = Shader.PropertyToID("_FlashT");
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public Arrow Arrow { get; private set; } = null!;

    /// <summary>
    /// Initializes the arrow view with its domain arrow and visual settings.
    /// </summary>
    public void Init(Arrow arrow, int boardWidth, int boardHeight, VisualSettings settings)
    {
        Arrow = arrow;
        _settings = settings;

        // Body mesh
        _meshFilter = gameObject.AddComponent<MeshFilter>();
        _meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // Create a material instance so per-arrow property changes don't affect others.
        // Setting .material (not .sharedMaterial) auto-instantiates.
        _meshRenderer.material = settings.arrowBodyMaterial;
        _materialInstance = _meshRenderer.material;

        _materialInstance.SetColor(ColorId, settings.arrowBodyColor);
        _materialInstance.SetColor(FlashColorId, settings.rejectFlashColor);
        _materialInstance.SetFloat(FlashTId, 0f);

        Vector3[] path = BoardCoords.ArrowPathToWorld(arrow, boardWidth, boardHeight);
        Mesh mesh = ArrowMeshBuilder.Build(path, settings.arrowBodyWidth, headLength: settings.arrowHeadLength, headWidthMultiplier: settings.arrowHeadWidthMultiplier);
        _meshFilter.mesh = mesh;

        _meshRenderer.sortingOrder = 1;
    }

    /// <summary>
    /// Plays the reject flash animation by driving _FlashT on the material instance.
    /// </summary>
    public void PlayRejectFlash()
    {
        StopAllCoroutines();
        StartCoroutine(RejectFlashCoroutine());
    }

    private IEnumerator RejectFlashCoroutine()
    {
        float elapsed = 0f;
        float duration = _settings.rejectFlashDuration;
        AnimationCurve curve = _settings.rejectFlashCurve;

        while (elapsed < duration)
        {
            float t = curve.Evaluate(elapsed / duration);
            _materialInstance.SetFloat(FlashTId, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _materialInstance.SetFloat(FlashTId, 0f);
    }

}
