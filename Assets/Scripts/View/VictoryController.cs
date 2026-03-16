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
        "🐐",
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
    private BoardGridRenderer _gridRenderer;
    private VisualElement _overlay;
    private Label _messageLabel;

    [SerializeField]
    private float gridFadeDuration = 0.5f;

    public void Init(UIDocument uiDocument, BoardGridRenderer gridRenderer)
    {
        _uiDocument = uiDocument;
        _gridRenderer = gridRenderer;

        var root = _uiDocument.rootVisualElement;
        _overlay = root.Q("victory-overlay");
        _messageLabel = root.Q<Label>("victory-message");

        root.Q<Button>("play-again-btn").clicked += OnPlayAgain;
        root.Q<Button>("menu-btn").clicked += OnMenu;
    }

    /// <summary>
    /// Call when the last arrow's pull-out animation finishes.
    /// Starts the grid fade, then shows the victory popup.
    /// </summary>
    public void OnBoardCleared()
    {
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

        _overlay.RemoveFromClassList("victory--hidden");
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
