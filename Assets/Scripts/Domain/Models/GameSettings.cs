/// <summary>
/// Static container for board parameters chosen in the main menu.
/// When IsSet is true, GameController uses these instead of its inspector fields.
/// When IsResuming is true, ResumeData holds the save to restore.
/// </summary>
public static class GameSettings
{
    public static bool IsSet { get; private set; }
    public static int Width { get; private set; }
    public static int Height { get; private set; }
    public static int MaxArrowLength { get; private set; }

    /// <summary>True when the player is continuing a previously saved game.</summary>
    public static bool IsResuming { get; private set; }

    /// <summary>The loaded save data when IsResuming is true; otherwise null.</summary>
    public static ReplayData ResumeData { get; private set; }

    // PlayerPrefs keys and defaults for settings persisted across sessions.
    public const string DragThresholdPrefKey = "DragThreshold";
    public const float DefaultDragThreshold = 15f;
    public const float MinDragThreshold = 5f;
    public const float MaxDragThreshold = 60f;

    public const string ArrowColoringPrefKey = "ArrowColoring";

    public const string ZoomSpeedPrefKey = "ZoomSpeed";
    public const float DefaultZoomSpeed = 1f;
    public const float MinZoomSpeed = 0.2f;
    public const float MaxZoomSpeed = 5f;

    public static void Apply(int width, int height)
    {
        Width = width;
        Height = height;
        MaxArrowLength = 2 * (width > height ? width : height);
        IsSet = true;
        IsResuming = false;
        ResumeData = null;
    }

    /// <summary>
    /// Signal that the next game scene load should resume from the on-disk save.
    /// The save file is NOT loaded here — GameController loads it after the
    /// loading overlay is visible to avoid a hitch on the menu.
    /// </summary>
    public static void ResumeFromSave()
    {
        IsSet = true;
        IsResuming = true;
        ResumeData = null;
    }

    /// <summary>
    /// Populate resume data after loading. Called by GameController once the
    /// save file has been read.
    /// </summary>
    public static void SetResumeData(ReplayData data)
    {
        Width = data.boardWidth;
        Height = data.boardHeight;
        MaxArrowLength = data.maxArrowLength;
        ResumeData = data;
    }

    public static void Reset()
    {
        IsSet = false;
        IsResuming = false;
        ResumeData = null;
        Width = 0;
        Height = 0;
        MaxArrowLength = 0;
    }
}
