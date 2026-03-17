using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Handles the board-cleared sequence: grid fade-out, then victory popup
/// with a randomized message and Play Again / Menu buttons.
/// </summary>
public sealed class VictoryController : MonoBehaviour
{
    private static readonly string[] Messages =
    {
        "Nice!",
        "Nailed it!",
        "That was smooth.",
        "Arrows fear you.",
        "Flawless.",
        "Too easy.",
        "Clean sweep!",
        "Not a scratch.",
        "WAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        "buff CE",
        "GOAT",
        "Victory Message.",
        "woa",
        "Where did my arrows go????? :(",
        "mmm im hungy. macdonel..",
        "WHAT???",
        "Congrat.",
        "[object Object]",
        "0110100001101001",
        "DONT PRESS THAT BUTTON DOWN THERE ITS A TRAP IT WILL MAKE YOU PLAY A DIFFERENT LEVEL ENTIRELY ITS DANGEROUS DONT DO IT NO!!!!!!!!!",
        "Wonderful.",
        "Wodnfeurl.",
        "pees on you", // CELERY CAME UP WITH THIS IT WASNT ME I SWEAR https://discord.com/channels/1085366539437494272/1085366539974361151/1482950081031442605
    };

    private UIDocument _uiDocument;
    private UIDocument _hudDocument;
    private BoardGridRenderer _gridRenderer;
    private CameraController _camCtrl;
    private GameTimer _timer;
    private VisualElement _overlay;
    private Label _messageLabel;
    private Label _timeLabel;

    [SerializeField]
    private float zoomOutDuration = 0.6f;

    [SerializeField]
    private float gridFadeDuration = 0.5f;

    public void Init(
        UIDocument uiDocument,
        BoardGridRenderer gridRenderer,
        CameraController camCtrl,
        GameTimer timer = null,
        UIDocument hudDocument = null
    )
    {
        _uiDocument = uiDocument;
        _hudDocument = hudDocument;
        _gridRenderer = gridRenderer;
        _camCtrl = camCtrl;
        _timer = timer;

        var root = _uiDocument.rootVisualElement;
        _overlay = root.Q("victory-overlay");
        _messageLabel = root.Q<Label>("victory-message");
        _timeLabel = root.Q<Label>("victory-time");

        root.Q<Button>("play-again-btn").clicked += OnPlayAgain;
        root.Q<Button>("menu-btn").clicked += OnMenu;
    }

    /// <summary>
    /// Call when the last arrow's pull-out animation finishes.
    /// Starts the grid fade, then shows the victory popup.
    /// </summary>
    public void OnBoardCleared()
    {
        if (_camCtrl != null)
            _camCtrl.ZoomToFit(
                zoomOutDuration,
                () => _gridRenderer.FadeOut(gridFadeDuration, ShowPopup)
            );
        else
            _gridRenderer.FadeOut(gridFadeDuration, ShowPopup);
    }

    private void ShowPopup()
    {
        string msg = Messages[Random.Range(0, Messages.Length)];
        _messageLabel.text = msg;

        // Scale font down for longer messages so they fit the box
        int len = msg.Length;
        if (len > 40)
            _messageLabel.style.fontSize = 20;
        else if (len > 20)
            _messageLabel.style.fontSize = 28;
        else
            _messageLabel.style.fontSize = 40;

        if (_timeLabel != null && _timer != null)
            _timeLabel.text = FormatTime(_timer.SolveElapsed);

        if (_hudDocument != null)
            _hudDocument.rootVisualElement.style.display = DisplayStyle.None;

        _overlay.RemoveFromClassList("victory--hidden");
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 0)
            seconds = 0;
        int totalMillis = (int)(seconds * 1000);
        int hours = totalMillis / 3600000;
        int mins = (totalMillis % 3600000) / 60000;
        int secs = (totalMillis % 60000) / 1000;
        int millis = totalMillis % 1000;

        if (hours > 0)
            return $"{hours}:{mins:D2}:{secs:D2}.{millis:D3}";
        if (mins > 0)
            return $"{mins}:{secs:D2}.{millis:D3}";
        return $"{secs}.{millis:D3}";
    }

    private void OnPlayAgain()
    {
        SceneManager.LoadScene("Game");
    }

    private void OnMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
