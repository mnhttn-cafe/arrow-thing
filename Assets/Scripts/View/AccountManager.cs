using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages the account screen UI: login, register, verification, forgot password,
/// reset password, email change, and logged-in account info forms. Screen navigation
/// (show/hide) is handled by MainMenuController.
/// </summary>
public class AccountManager
{
    private readonly ApiClient _api;

    // Forms
    private readonly VisualElement _loginForm;
    private readonly VisualElement _registerForm;
    private readonly VisualElement _verifyForm;
    private readonly VisualElement _forgotForm;
    private readonly VisualElement _resetForm;
    private readonly VisualElement _accountInfo;
    private readonly VisualElement _changeEmailForm;
    private readonly VisualElement _confirmEmailForm;
    private readonly VisualElement _changePasswordForm;

    // Login fields
    private readonly TextField _loginEmail;
    private readonly TextField _loginPassword;
    private readonly Label _loginError;

    // Register fields
    private readonly TextField _registerEmail;
    private readonly TextField _registerDisplayName;
    private readonly TextField _registerPassword;
    private readonly TextField _registerConfirmPassword;
    private readonly Label _registerError;

    // Verify fields
    private readonly Label _verifyMessage;
    private readonly TextField _verifyCode;
    private readonly Label _verifyError;
    private readonly Label _verifySuccess;

    // Forgot password fields
    private readonly TextField _forgotEmail;
    private readonly Label _forgotError;
    private readonly Label _forgotSuccess;

    // Reset password fields
    private readonly Label _resetMessage;
    private readonly TextField _resetCode;
    private readonly TextField _resetNewPassword;
    private readonly TextField _resetConfirmPassword;
    private readonly Label _resetError;
    private readonly Label _resetSuccess;

    // Account info fields
    private readonly Label _accountGreeting;
    private readonly Label _accountEmail;
    private readonly TextField _changeDisplayName;
    private readonly Label _accountError;

    // Change email fields
    private readonly TextField _newEmail;
    private readonly TextField _confirmPassword;
    private readonly Label _changeEmailError;

    // Confirm email change fields
    private readonly Label _confirmEmailMessage;
    private readonly TextField _confirmEmailCode;
    private readonly Label _confirmEmailError;

    // Change password fields
    private readonly TextField _currentPassword;
    private readonly TextField _newPassword;
    private readonly TextField _confirmNewPassword;
    private readonly Label _changePasswordError;
    private readonly Label _changePasswordSuccess;

    // Track email for resend / confirm flows
    private string _pendingVerificationEmail;
    private string _pendingNewEmail;
    private string _pendingResetEmail;

    public AccountManager(VisualElement screenRoot)
    {
        _api = new ApiClient();

        // Login form
        _loginForm = screenRoot.Q("login-form");
        _loginEmail = screenRoot.Q<TextField>("login-email");
        _loginPassword = screenRoot.Q<TextField>("login-password");
        _loginPassword.isPasswordField = true;
        _loginError = screenRoot.Q<Label>("login-error");

        screenRoot.Q<Button>("login-submit-btn").clicked += OnLogin;
        screenRoot.Q<Button>("forgot-password-btn").clicked += ShowForgotForm;
        screenRoot.Q<Button>("switch-to-register-btn").clicked += ShowRegisterForm;

        // Register form
        _registerForm = screenRoot.Q("register-form");
        _registerEmail = screenRoot.Q<TextField>("register-email");
        _registerDisplayName = screenRoot.Q<TextField>("register-display-name");
        _registerPassword = screenRoot.Q<TextField>("register-password");
        _registerPassword.isPasswordField = true;
        _registerConfirmPassword = screenRoot.Q<TextField>("register-confirm-password");
        _registerConfirmPassword.isPasswordField = true;
        _registerError = screenRoot.Q<Label>("register-error");

        screenRoot.Q<Button>("register-submit-btn").clicked += OnRegister;
        screenRoot.Q<Button>("switch-to-login-btn").clicked += ShowLoginForm;

        // Verify form
        _verifyForm = screenRoot.Q("verify-form");
        _verifyMessage = screenRoot.Q<Label>("verify-message");
        _verifyCode = screenRoot.Q<TextField>("verify-code");
        _verifyError = screenRoot.Q<Label>("verify-error");
        _verifySuccess = screenRoot.Q<Label>("verify-success");

        screenRoot.Q<Button>("verify-submit-btn").clicked += OnVerifyCode;
        screenRoot.Q<Button>("resend-verify-btn").clicked += OnResendVerification;
        screenRoot.Q<Button>("verify-back-btn").clicked += ShowLoginForm;

        // Forgot password form
        _forgotForm = screenRoot.Q("forgot-form");
        _forgotEmail = screenRoot.Q<TextField>("forgot-email");
        _forgotError = screenRoot.Q<Label>("forgot-error");
        _forgotSuccess = screenRoot.Q<Label>("forgot-success");

        screenRoot.Q<Button>("forgot-submit-btn").clicked += OnForgotPassword;
        screenRoot.Q<Button>("forgot-back-btn").clicked += ShowLoginForm;

        // Reset password form
        _resetForm = screenRoot.Q("reset-form");
        _resetMessage = screenRoot.Q<Label>("reset-message");
        _resetCode = screenRoot.Q<TextField>("reset-code");
        _resetNewPassword = screenRoot.Q<TextField>("reset-new-password");
        _resetNewPassword.isPasswordField = true;
        _resetConfirmPassword = screenRoot.Q<TextField>("reset-confirm-password");
        _resetConfirmPassword.isPasswordField = true;
        _resetError = screenRoot.Q<Label>("reset-error");
        _resetSuccess = screenRoot.Q<Label>("reset-success");

        screenRoot.Q<Button>("reset-submit-btn").clicked += OnResetPassword;
        screenRoot.Q<Button>("reset-back-btn").clicked += ShowLoginForm;

        // Account info
        _accountInfo = screenRoot.Q("account-info");
        _accountGreeting = screenRoot.Q<Label>("account-greeting");
        _accountEmail = screenRoot.Q<Label>("account-email");
        _changeDisplayName = screenRoot.Q<TextField>("change-display-name");
        _accountError = screenRoot.Q<Label>("account-error");

        screenRoot.Q<Button>("save-display-name-btn").clicked += OnSaveDisplayName;
        screenRoot.Q<Button>("change-email-btn").clicked += ShowChangeEmailForm;
        screenRoot.Q<Button>("change-password-btn").clicked += ShowChangePasswordForm;
        screenRoot.Q<Button>("logout-btn").clicked += OnLogout;

        // Change email form
        _changeEmailForm = screenRoot.Q("change-email-form");
        _newEmail = screenRoot.Q<TextField>("new-email");
        _confirmPassword = screenRoot.Q<TextField>("confirm-password");
        _confirmPassword.isPasswordField = true;
        _changeEmailError = screenRoot.Q<Label>("change-email-error");

        screenRoot.Q<Button>("change-email-submit-btn").clicked += OnChangeEmail;
        screenRoot.Q<Button>("change-email-back-btn").clicked += ShowAccountInfo;

        // Confirm email change form
        _confirmEmailForm = screenRoot.Q("confirm-email-form");
        _confirmEmailMessage = screenRoot.Q<Label>("confirm-email-message");
        _confirmEmailCode = screenRoot.Q<TextField>("confirm-email-code");
        _confirmEmailError = screenRoot.Q<Label>("confirm-email-error");

        screenRoot.Q<Button>("confirm-email-submit-btn").clicked += OnConfirmEmailChange;
        screenRoot.Q<Button>("confirm-email-back-btn").clicked += ShowAccountInfo;

        // Change password form
        _changePasswordForm = screenRoot.Q("change-password-form");
        _currentPassword = screenRoot.Q<TextField>("current-password");
        _currentPassword.isPasswordField = true;
        _newPassword = screenRoot.Q<TextField>("new-password");
        _newPassword.isPasswordField = true;
        _confirmNewPassword = screenRoot.Q<TextField>("confirm-new-password");
        _confirmNewPassword.isPasswordField = true;
        _changePasswordError = screenRoot.Q<Label>("change-password-error");
        _changePasswordSuccess = screenRoot.Q<Label>("change-password-success");

        screenRoot.Q<Button>("change-password-submit-btn").clicked += OnChangePassword;
        screenRoot.Q<Button>("change-password-back-btn").clicked += ShowAccountInfo;

        // Start in correct state
        if (_api.IsLoggedIn)
            ShowAccountInfo();
        else
            ShowLoginForm();
    }

    private void ShowLoginForm()
    {
        HideAll();
        _loginEmail.value = "";
        _loginPassword.value = "";
        SetVisible(_loginForm, true);
    }

    private void ShowRegisterForm()
    {
        HideAll();
        _registerEmail.value = "";
        _registerDisplayName.value = "";
        _registerPassword.value = "";
        _registerConfirmPassword.value = "";
        SetVisible(_registerForm, true);
    }

    private void ShowVerifyForm()
    {
        HideAll();
        _verifyCode.value = "";
        SetVisible(_verifyForm, true);
    }

    private void ShowForgotForm()
    {
        HideAll();
        _forgotEmail.value = "";
        SetVisible(_forgotForm, true);
    }

    private void ShowResetForm()
    {
        HideAll();
        _resetCode.value = "";
        _resetNewPassword.value = "";
        _resetConfirmPassword.value = "";
        _resetMessage.text = $"We sent a 6-digit code to {_pendingResetEmail}.";
        SetVisible(_resetForm, true);
    }

    private async void ShowAccountInfo()
    {
        HideAll();
        SetVisible(_accountInfo, true);

        // Refresh account state from server (picks up email changes, verification, etc.)
        var result = await _api.GetMeAsync();

        _accountGreeting.text = _api.DisplayName;
        _changeDisplayName.value = _api.DisplayName;
        _accountEmail.text = MaskEmail(_api.Email);
    }

    private void ShowChangeEmailForm()
    {
        HideAll();
        _newEmail.value = "";
        _confirmPassword.value = "";
        SetVisible(_changeEmailForm, true);
    }

    private void ShowConfirmEmailForm()
    {
        HideAll();
        _confirmEmailCode.value = "";
        _confirmEmailMessage.text = $"We sent a 6-digit code to {_pendingNewEmail}.";
        SetVisible(_confirmEmailForm, true);
    }

    private void ShowChangePasswordForm()
    {
        HideAll();
        _currentPassword.value = "";
        _newPassword.value = "";
        _confirmNewPassword.value = "";
        SetVisible(_changePasswordForm, true);
    }

    private void HideAll()
    {
        SetVisible(_loginForm, false);
        SetVisible(_registerForm, false);
        SetVisible(_verifyForm, false);
        SetVisible(_forgotForm, false);
        SetVisible(_resetForm, false);
        SetVisible(_accountInfo, false);
        SetVisible(_changeEmailForm, false);
        SetVisible(_confirmEmailForm, false);
        SetVisible(_changePasswordForm, false);
        ClearErrors();
    }

    private async void OnLogin()
    {
        ClearErrors();
        var result = await _api.LoginAsync(_loginEmail.value, _loginPassword.value);

        if (result.Success)
        {
            ShowAccountInfo();
        }
        else
        {
            ShowError(_loginError, result.Error);
        }
    }

    private async void OnRegister()
    {
        ClearErrors();

        if (_registerPassword.value != _registerConfirmPassword.value)
        {
            ShowError(_registerError, "Passwords do not match.");
            return;
        }

        var result = await _api.RegisterAsync(
            _registerEmail.value,
            _registerPassword.value,
            _registerDisplayName.value
        );

        if (result.Success)
        {
            _pendingVerificationEmail = _registerEmail.value;
            _verifyMessage.text = $"We sent a 6-digit code to {_pendingVerificationEmail}.";
            ShowVerifyForm();
        }
        else
        {
            ShowError(_registerError, result.Error);
        }
    }

    private async void OnVerifyCode()
    {
        ClearErrors();
        SetVisible(_verifySuccess, false);

        if (string.IsNullOrEmpty(_pendingVerificationEmail))
            return;

        var result = await _api.VerifyCodeAsync(_pendingVerificationEmail, _verifyCode.value);

        if (result.Success)
        {
            ShowAccountInfo();
        }
        else
        {
            ShowError(_verifyError, result.Error);
        }
    }

    private async void OnResendVerification()
    {
        ClearErrors();
        if (string.IsNullOrEmpty(_pendingVerificationEmail))
            return;

        var result = await _api.ResendVerificationAsync(_pendingVerificationEmail);

        if (result.Success)
        {
            _verifyMessage.text = "Verification email sent. Check your inbox.";
        }
        else
        {
            ShowError(_verifyError, result.Error);
        }
    }

    private async void OnForgotPassword()
    {
        ClearErrors();
        SetVisible(_forgotSuccess, false);

        var result = await _api.ForgotPasswordAsync(_forgotEmail.value);

        if (result.Success)
        {
            _pendingResetEmail = _forgotEmail.value;
            ShowResetForm();
        }
        else
        {
            ShowError(_forgotError, result.Error);
        }
    }

    private async void OnResetPassword()
    {
        ClearErrors();
        SetVisible(_resetSuccess, false);

        if (string.IsNullOrEmpty(_pendingResetEmail))
            return;

        if (_resetNewPassword.value != _resetConfirmPassword.value)
        {
            ShowError(_resetError, "Passwords do not match.");
            return;
        }

        var result = await _api.ResetPasswordAsync(
            _pendingResetEmail,
            _resetCode.value,
            _resetNewPassword.value
        );

        if (result.Success)
        {
            _resetSuccess.text = result.Data.message;
            SetVisible(_resetSuccess, true);
            _resetCode.value = "";
            _resetNewPassword.value = "";
            _resetConfirmPassword.value = "";
        }
        else
        {
            ShowError(_resetError, result.Error);
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

    private async void OnChangeEmail()
    {
        ClearErrors();

        var result = await _api.ChangeEmailAsync(_newEmail.value, _confirmPassword.value);

        if (result.Success)
        {
            _pendingNewEmail = _newEmail.value;
            ShowConfirmEmailForm();
        }
        else
        {
            ShowError(_changeEmailError, result.Error);
        }
    }

    private async void OnConfirmEmailChange()
    {
        ClearErrors();

        if (string.IsNullOrEmpty(_pendingNewEmail))
            return;

        var result = await _api.ConfirmEmailChangeAsync(_pendingNewEmail, _confirmEmailCode.value);

        if (result.Success)
        {
            ShowAccountInfo();
        }
        else
        {
            ShowError(_confirmEmailError, result.Error);
        }
    }

    private async void OnChangePassword()
    {
        ClearErrors();
        SetVisible(_changePasswordSuccess, false);

        if (_newPassword.value != _confirmNewPassword.value)
        {
            ShowError(_changePasswordError, "Passwords do not match.");
            return;
        }

        var result = await _api.ChangePasswordAsync(_currentPassword.value, _newPassword.value);

        if (result.Success)
        {
            _changePasswordSuccess.text = result.Data.message;
            SetVisible(_changePasswordSuccess, true);
            _currentPassword.value = "";
            _newPassword.value = "";
            _confirmNewPassword.value = "";
        }
        else
        {
            ShowError(_changePasswordError, result.Error);
        }
    }

    private void OnLogout()
    {
        _api.Logout();
        ShowLoginForm();
    }

    private void ClearErrors()
    {
        SetVisible(_loginError, false);
        SetVisible(_registerError, false);
        SetVisible(_verifyError, false);
        SetVisible(_verifySuccess, false);
        SetVisible(_forgotError, false);
        SetVisible(_forgotSuccess, false);
        SetVisible(_resetError, false);
        SetVisible(_resetSuccess, false);
        SetVisible(_accountError, false);
        SetVisible(_changeEmailError, false);
        SetVisible(_confirmEmailError, false);
        SetVisible(_changePasswordError, false);
        SetVisible(_changePasswordSuccess, false);
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return "";

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return email;

        var local = email.Substring(0, atIndex);
        var domain = email.Substring(atIndex);

        if (local.Length <= 2)
            return local[0] + new string('*', local.Length - 1) + domain;

        return local[0] + new string('*', local.Length - 2) + local[local.Length - 1] + domain;
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
