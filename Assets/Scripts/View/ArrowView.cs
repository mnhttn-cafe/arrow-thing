using System.Collections;
using UnityEngine;

/// <summary>
/// Visual representation of a single arrow. Owns the procedural body mesh
/// and a separate arrowhead child object. Handles reject flash animation.
/// </summary>
public sealed class ArrowView : MonoBehaviour
{
    private MeshFilter _meshFilter = null!;
    private MeshRenderer _meshRenderer = null!;
    private Material _materialInstance = null!;
    private Material _headMaterialInstance = null!;
    private VisualSettings _settings = null!;
    private GameObject _arrowHead = null!;
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
        Mesh bodyMesh = ArrowMeshBuilder.Build(path, settings.arrowBodyWidth);
        _meshFilter.mesh = bodyMesh;
        _meshRenderer.sortingOrder = 1;

        // Arrowhead as separate child object
        _arrowHead = CreateArrowHead(path, settings);
        _arrowHead.transform.SetParent(transform, true);

        _headMaterialInstance = _arrowHead.GetComponent<MeshRenderer>().material;
        _headMaterialInstance.SetColor(ColorId, settings.arrowHeadColor);
        _headMaterialInstance.SetColor(FlashColorId, settings.rejectFlashColor);
        _headMaterialInstance.SetFloat(FlashTId, 0f);
    }

    private static GameObject CreateArrowHead(Vector3[] path, VisualSettings settings)
    {
        Vector3 headPos = path[0];
        Vector3 headDir = (path[0] - path[1]).normalized;
        Vector3 headPerp = new Vector3(-headDir.y, headDir.x, 0f);

        float headHalfBase = settings.arrowBodyWidth * settings.arrowHeadWidthMultiplier;
        Vector3 tip = headPos + headDir * settings.arrowHeadLength;
        Vector3 baseLeft = headPos - headPerp * headHalfBase;
        Vector3 baseRight = headPos + headPerp * headHalfBase;

        var mesh = new Mesh { name = "ArrowHead" };
        mesh.vertices = new[] { baseLeft, baseRight, tip };
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("ArrowHead");
        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.material = settings.arrowHeadMaterial != null
            ? settings.arrowHeadMaterial
            : settings.arrowBodyMaterial;
        mr.sortingOrder = 2;

        return go;
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
            _headMaterialInstance.SetFloat(FlashTId, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _materialInstance.SetFloat(FlashTId, 0f);
        _headMaterialInstance.SetFloat(FlashTId, 0f);
    }

}
