using UnityEngine;

/// <summary>
/// Orthographic camera controller. Exposes Pan/Zoom methods — input polling is
/// handled by InputHandler, which calls these.
/// </summary>
public sealed class CameraController : MonoBehaviour
{
    [Header("Zoom")]
    [SerializeField] private float minOrthoSize = 2f;
    [SerializeField] private float maxOrthoSize = 20f;
    [SerializeField] private float zoomSpeed = 1f;

    [Header("Pan")]
    [SerializeField] private float panBuffer = 2f;

    private Camera _cam = null!;
    private Rect _panBounds;

    public Camera Cam => _cam;

    public void Init(Board board, float bufferFraction = 0.1f)
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;

        // Fit camera to board with buffer margin
        float boardW = board.Width;
        float boardH = board.Height;
        float buffer = Mathf.Max(boardW, boardH) * bufferFraction;
        float sizeForHeight = (boardH + buffer) * 0.5f;
        float sizeForWidth = (boardW + buffer) * 0.5f / _cam.aspect;
        _cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);

        // Pan bounds: board extents + panBuffer
        float halfW = boardW * 0.5f + panBuffer;
        float halfH = boardH * 0.5f + panBuffer;
        _panBounds = new Rect(-halfW, -halfH, halfW * 2f, halfH * 2f);

        transform.position = new Vector3(0f, 0f, -10f);
    }

    /// <summary>
    /// Pans the camera by a world-space delta, clamped to board bounds.
    /// </summary>
    public void Pan(Vector3 worldDelta)
    {
        Vector3 newPos = transform.position + worldDelta;
        newPos.x = Mathf.Clamp(newPos.x, _panBounds.xMin, _panBounds.xMax);
        newPos.y = Mathf.Clamp(newPos.y, _panBounds.yMin, _panBounds.yMax);
        newPos.z = transform.position.z;
        transform.position = newPos;
    }

    /// <summary>
    /// Zooms the camera by a scroll delta value (positive = zoom in).
    /// </summary>
    public void Zoom(float scrollDelta)
    {
        _cam.orthographicSize = Mathf.Clamp(
            _cam.orthographicSize - scrollDelta * zoomSpeed,
            minOrthoSize, maxOrthoSize);
    }

    /// <summary>
    /// Zooms the camera by a pinch ratio (> 1 = zoom in, &lt; 1 = zoom out).
    /// </summary>
    public void PinchZoom(float ratio)
    {
        _cam.orthographicSize = Mathf.Clamp(
            _cam.orthographicSize / ratio,
            minOrthoSize, maxOrthoSize);
    }
}
