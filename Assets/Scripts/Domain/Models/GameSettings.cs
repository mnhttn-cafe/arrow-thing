/// <summary>
/// Static container for board parameters chosen in the main menu.
/// When IsSet is true, GameController uses these instead of its inspector fields.
/// </summary>
public static class GameSettings
{
    public static bool IsSet { get; private set; }
    public static int Width { get; private set; }
    public static int Height { get; private set; }
    public static int MaxArrowLength { get; private set; }

    /// <summary>
    /// Screen-space drag threshold in pixels. Persisted via PlayerPrefs.
    /// </summary>
    public static float DragThreshold { get; set; } = DefaultDragThreshold;

    /// <summary>
    /// Whether to tint arrows with map-coloring. Persisted via PlayerPrefs.
    /// </summary>
    public static bool ArrowColoring { get; set; } = false;

    public const float DefaultDragThreshold = 15f;
    public const float MinDragThreshold = 5f;
    public const float MaxDragThreshold = 60f;

    /// <summary>
    /// Zoom speed multiplier. Persisted via PlayerPrefs.
    /// </summary>
    public static float ZoomSpeed { get; set; } = DefaultZoomSpeed;

    public const float DefaultZoomSpeed = 1f;
    public const float MinZoomSpeed = 0.2f;
    public const float MaxZoomSpeed = 5f;

    public static void Apply(int width, int height)
    {
        Width = width;
        Height = height;
        MaxArrowLength = 2 * (width > height ? width : height);
        IsSet = true;
    }

    public static void Reset()
    {
        IsSet = false;
        Width = 0;
        Height = 0;
        MaxArrowLength = 0;
    }
}
