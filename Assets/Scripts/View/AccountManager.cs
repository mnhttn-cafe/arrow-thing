using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages the account screen UI: register/login forms and logged-in account info.
/// Screen navigation (show/hide) is handled by MainMenuController.
/// </summary>
public class AccountManager
{
    private readonly ApiClient _api;

    // Forms
    private readonly VisualElement _registerForm;
    private readonly VisualElement _loginForm;
    private readonly VisualElement _accountInfo;

    // Register fields
    private readonly TextField _registerUsername;
    private readonly TextField _registerDisplayName;
    private readonly TextField _registerPassword;
    private readonly Label _registerError;

    // Login fields
    private readonly TextField _loginUsername;
    private readonly TextField _loginPassword;
    private readonly Label _loginError;

    // Account info fields
    private readonly Label _accountGreeting;
    private readonly TextField _changeDisplayName;
    private readonly Label _accountError;

    public AccountManager(VisualElement screenRoot)
    {
        _api = new ApiClient();

        // Register form
        _registerForm = screenRoot.Q("register-form");
        _registerUsername = screenRoot.Q<TextField>("register-username");
        _registerDisplayName = screenRoot.Q<TextField>("register-display-name");
        _registerPassword = screenRoot.Q<TextField>("register-password");
        _registerPassword.isPasswordField = true;
        _registerError = screenRoot.Q<Label>("register-error");

        screenRoot.Q<Button>("register-submit-btn").clicked += OnRegister;
        screenRoot.Q<Button>("switch-to-login-btn").clicked += ShowLoginForm;

        // Login form
        _loginForm = screenRoot.Q("login-form");
        _loginUsername = screenRoot.Q<TextField>("login-username");
        _loginPassword = screenRoot.Q<TextField>("login-password");
        _loginPassword.isPasswordField = true;
        _loginError = screenRoot.Q<Label>("login-error");

        screenRoot.Q<Button>("login-submit-btn").clicked += OnLogin;
        screenRoot.Q<Button>("switch-to-register-btn").clicked += ShowRegisterForm;

        // Account info
        _accountInfo = screenRoot.Q("account-info");
        _accountGreeting = screenRoot.Q<Label>("account-greeting");
        _changeDisplayName = screenRoot.Q<TextField>("change-display-name");
        _accountError = screenRoot.Q<Label>("account-error");

        screenRoot.Q<Button>("save-display-name-btn").clicked += OnSaveDisplayName;
        screenRoot.Q<Button>("logout-btn").clicked += OnLogout;

        // Start in correct state
        if (_api.IsLoggedIn)
            ShowAccountInfo();
        else
            ShowLoginForm();
    }

    private void ShowRegisterForm()
    {
        SetVisible(_registerForm, true);
        SetVisible(_loginForm, false);
        SetVisible(_accountInfo, false);
        ClearErrors();
    }

    private void ShowLoginForm()
    {
        SetVisible(_registerForm, false);
        SetVisible(_loginForm, true);
        SetVisible(_accountInfo, false);
        ClearErrors();
    }

    private void ShowAccountInfo()
    {
        SetVisible(_registerForm, false);
        SetVisible(_loginForm, false);
        SetVisible(_accountInfo, true);
        _accountGreeting.text = _api.DisplayName;
        _changeDisplayName.value = _api.DisplayName;
        ClearErrors();
    }

    private async void OnRegister()
    {
        ClearErrors();
        var result = await _api.RegisterAsync(
            _registerUsername.value,
            _registerPassword.value,
            _registerDisplayName.value
        );

        if (result.Success)
        {
            ShowAccountInfo();
        }
        else
        {
            ShowError(_registerError, result.Error);
        }
    }

    private async void OnLogin()
    {
        ClearErrors();
        var result = await _api.LoginAsync(_loginUsername.value, _loginPassword.value);

        if (result.Success)
        {
            ShowAccountInfo();
        }
        else
        {
            ShowError(_loginError, result.Error);
        }
    }

    private async void OnSaveDisplayName()
    {
        ClearErrors();
        var result = await _api.UpdateDisplayNameAsync(_changeDisplayName.value);

        if (result.Success)
        {
            _accountGreeting.text = _api.DisplayName;
        }
        else
        {
            ShowError(_accountError, result.Error);
        }
    }

    private void OnLogout()
    {
        _api.Logout();
        ShowLoginForm();
    }

    private void ClearErrors()
    {
        SetVisible(_registerError, false);
        SetVisible(_loginError, false);
        SetVisible(_accountError, false);
    }

    private static void ShowError(Label label, string message)
    {
        label.text = message;
        label.RemoveFromClassList("screen--hidden");
    }

    private static void SetVisible(VisualElement el, bool visible)
    {
        if (visible)
            el.RemoveFromClassList("screen--hidden");
        else
            el.AddToClassList("screen--hidden");
    }
}
