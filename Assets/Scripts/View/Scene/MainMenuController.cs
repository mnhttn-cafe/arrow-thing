using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Drives the main menu UI: Play, Continue, Settings, Leaderboard, and Quit.
/// Board size selection is in a separate scene (Solo Size Select).
/// </summary>
public sealed class MainMenuController : NavigableScene
{
    private const string GitHubUrl = "https://github.com/vicplusplus/arrow-thing";
    private const string DiscordUrl = "https://discord.gg/FBwTyaWzpE";

    private ConfirmModal _quitModal;

    protected override KeybindManager.Context NavContext => KeybindManager.Context.MainMenu;

    protected override void BuildUI(VisualElement root)
    {
        var continueBtn = root.Q<Button>("continue-btn");
        if (SaveManager.HasSave())
            SetVisible(continueBtn, true);

        root.Q<Button>("play-btn").clicked += OnPlay;
        continueBtn.clicked += OnContinue;
        root.Q<Button>("settings-btn").clicked += () => SettingsController.Instance.Open();
        root.Q<Button>("leaderboard-btn").clicked += OnLeaderboard;
        root.Q<Button>("link-github-btn").clicked += () => ExternalLinks.Open(GitHubUrl);
        root.Q<Button>("link-discord-btn").clicked += () => ExternalLinks.Open(DiscordUrl);

        var quitBtn = root.Q<Button>("quit-btn");
        if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
            quitBtn.style.display = DisplayStyle.None;
        else
            quitBtn.clicked += OnQuitPressed;

        _quitModal = new ConfirmModal(root.Q("quit-modal"), "Quit game?", "Yes", "No");
        _quitModal.Confirmed += OnQuitConfirm;
        _quitModal.Cancelled += () => _quitModal.Hide();
    }

    protected override void BuildNavGraph(FocusNavigator nav)
    {
        var items = new List<FocusNavigator.FocusItem>();

        var quitBtn = Root.Q<Button>("quit-btn");
        bool hasQuit =
            quitBtn != null
            && !Application.isMobilePlatform
            && Application.platform != RuntimePlatform.WebGLPlayer;
        int quitIdx = -1;
        if (hasQuit)
        {
            quitIdx = items.Count;
            items.Add(
                new FocusNavigator.FocusItem
                {
                    Element = quitBtn,
                    OnActivate = () =>
                    {
                        OnQuitPressed();
                        return true;
                    },
                }
            );
        }

        int leaderboardIdx = items.Count;
        items.Add(
            new FocusNavigator.FocusItem
            {
                Element = Root.Q<Button>("leaderboard-btn"),
                OnActivate = () =>
                {
                    OnLeaderboard();
                    return true;
                },
            }
        );

        int playIdx = items.Count;
        items.Add(
            new FocusNavigator.FocusItem
            {
                Element = Root.Q<Button>("play-btn"),
                OnActivate = () =>
                {
                    OnPlay();
                    return true;
                },
            }
        );

        var continueBtn = Root.Q<Button>("continue-btn");
        bool hasContinue = !continueBtn.ClassListContains("screen--hidden");
        int continueIdx = -1;
        if (hasContinue)
        {
            continueIdx = items.Count;
            items.Add(
                new FocusNavigator.FocusItem
                {
                    Element = continueBtn,
                    OnActivate = () =>
                    {
                        OnContinue();
                        return true;
                    },
                }
            );
        }

        int settingsIdx = items.Count;
        items.Add(
            new FocusNavigator.FocusItem
            {
                Element = Root.Q<Button>("settings-btn"),
                OnActivate = () =>
                {
                    SettingsController.Instance.Open();
                    return true;
                },
            }
        );

        int githubIdx = items.Count;
        items.Add(
            new FocusNavigator.FocusItem
            {
                Element = Root.Q<Button>("link-github-btn"),
                OnActivate = () =>
                {
                    ExternalLinks.Open(GitHubUrl);
                    return true;
                },
            }
        );

        int discordIdx = items.Count;
        items.Add(
            new FocusNavigator.FocusItem
            {
                Element = Root.Q<Button>("link-discord-btn"),
                OnActivate = () =>
                {
                    ExternalLinks.Open(DiscordUrl);
                    return true;
                },
            }
        );

        nav.SetItems(items, playIdx);

        nav.Link(playIdx, FocusNavigator.NavDir.Up, leaderboardIdx);
        nav.Link(leaderboardIdx, FocusNavigator.NavDir.Down, playIdx);

        if (hasQuit)
        {
            nav.LinkBidi(quitIdx, FocusNavigator.NavDir.Right, leaderboardIdx);
            nav.Link(quitIdx, FocusNavigator.NavDir.Down, playIdx);
            nav.Link(leaderboardIdx, FocusNavigator.NavDir.Left, quitIdx);
            nav.Link(playIdx, FocusNavigator.NavDir.Left, quitIdx);
        }

        int belowPlay = hasContinue ? continueIdx : settingsIdx;
        nav.LinkBidi(playIdx, FocusNavigator.NavDir.Down, belowPlay);
        if (hasContinue)
            nav.LinkBidi(continueIdx, FocusNavigator.NavDir.Down, settingsIdx);

        nav.LinkBidi(settingsIdx, FocusNavigator.NavDir.Down, discordIdx);
        nav.Link(settingsIdx, FocusNavigator.NavDir.Left, discordIdx);

        nav.LinkBidi(githubIdx, FocusNavigator.NavDir.Right, discordIdx);
        nav.Link(githubIdx, FocusNavigator.NavDir.Up, settingsIdx);
    }

    protected override void OnUpdate(KeybindManager km)
    {
        if (km.OpenLeaderboard != null && km.OpenLeaderboard.WasPerformedThisFrame())
            OnLeaderboard();
    }

    protected override void OnCancel()
    {
        if (!Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer)
            OnQuitPressed();
    }

    // -- Actions -----------------------------------------------------------------

    private void OnPlay() => SceneNav.Push("Solo Size Select");

    private void OnContinue()
    {
        GameSettings.ResumeFromSave();
        SceneNav.Push("Game");
    }

    private void OnLeaderboard() => SceneNav.Push("Leaderboard");

    private void OnQuitPressed() => _quitModal.Show();

    private void OnQuitConfirm()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private static void SetVisible(VisualElement el, bool visible)
    {
        if (visible)
            el.RemoveFromClassList("screen--hidden");
        else
            el.AddToClassList("screen--hidden");
    }
}
