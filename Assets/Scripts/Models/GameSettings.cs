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

    public static void Apply(int width, int height)
    {
        Width = width;
        Height = height;
        MaxArrowLength = 2 * (width > height ? width : height);
        IsSet = true;
    }
}
