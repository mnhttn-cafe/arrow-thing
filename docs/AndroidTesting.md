# Android Testing

> No iOS device available — this doc covers Android only. iOS setup and troubleshooting TBD.

## Quick Iteration: Unity Remote 5

Streams the editor Game view to your phone over USB and sends touch input back. Not a real build — `Application.isMobilePlatform` returns false, so platform-conditional logic (e.g. quit button visibility) won't reflect mobile behavior.

Official docs: https://docs.unity3d.com/Manual/UnityRemote5.html

### Setup

1. Install **Unity Remote 5** on your Android phone (free on Play Store).
2. Install **Android Build Support** module via Unity Hub (Hub > Installs > your version > Add Modules). This includes the Android SDK and ADB.
3. Add ADB to your PATH (it lives in `<Unity install>/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/`).
4. Enable **USB Debugging** on the phone (Settings > Developer Options).
5. Connect via USB. Run `adb devices` — the phone should show as "device" (not "unauthorized").
6. In Unity: **Edit > Project Settings > Editor > Device** → set to **Any Android Device**.

### Troubleshooting

- **Active build profile must be Android.** Unity Remote will not stream if the active profile is set to another platform (e.g. Windows). Switch via **File > Build Settings**.
- **"unauthorized" in `adb devices`**: check the phone screen for an "Allow USB debugging?" prompt. If none appears, revoke USB debugging authorizations (Developer Options), unplug/replug, and run `adb kill-server; adb start-server`.
- **Nothing happens on Play**: restart Unity after changing the Device setting — it doesn't always take effect until restart.
- **ADB not found**: the Unity-bundled SDK doesn't auto-add to PATH. Find `adb.exe` manually (see step 3 above) and add the folder to your user PATH.

## Device Builds

**File > Build Settings** → switch to Android → **Build And Run** deploys an APK over ADB. Slower loop (~5 min) but tests real performance, platform APIs, and actual UI scaling.
