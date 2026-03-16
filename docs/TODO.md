# Feature: Minimal Start Menu

## Goal

Add a functional start menu using UI Toolkit in the existing `MainMenu` scene. The player can configure board size, access a settings screen, and launch the game. Scene transition passes chosen parameters to `GameController`.

---

## Design

### Screens

1. **Main Menu** — title + two buttons: **Play**, **Settings**. On desktop platforms only, a small **X** button in the top-left corner opens a quit confirmation modal ("Quit game?" with Yes / No).
2. **Mode Select** (navigated from Play) — board-size picker (preset buttons: Small 10×10, Medium 20×20, Large 40×40) + a **Start** button.
3. **Settings** (navigated from Settings) — placeholder screen with a **Back** button. Actual settings (volume, etc.) are out of scope for this pass.

All three screens live in the same `MainMenu` scene, toggled via USS display classes (no additional scene loads within the menu).

### Scene Transition

- Mode Select's **Start** button loads `Game` scene via `SceneManager.LoadScene`.
- Board parameters (width, height) are passed to `GameController` via a small static `GameSettings` class (lives in domain layer, no MonoBehaviour). `MaxArrowLength` is derived as `2 * max(width, height)`. `GameController.Awake` reads from `GameSettings` instead of its serialized fields when values are present.

### UI Toolkit Setup

- Single `Root.uxml` containing all screens (main menu, mode select, settings, quit modal) as sibling `VisualElement`s. Screen switching toggles the `screen--hidden` USS class.
- `Root.uxml` references `MainMenu.uss` via `<Style src="MainMenu.uss" />`.
- One `UIDocument` component in the scene, with `Root.uxml` as its source asset.
- A `MainMenuController` MonoBehaviour references that UIDocument and registers all button callbacks.

### File Layout

```
Assets/
  UI/
    Root.uxml         # all screens in one document
    MainMenu.uss      # shared stylesheet
  Scripts/
    Models/
      GameSettings.cs        # static class holding chosen board params
    View/
      MainMenuController.cs  # wires UI Toolkit callbacks, screen nav, scene load
```

---

## Implementation Plan

- [x] **1. GameSettings static class** — `GameSettings.cs` in Models.
- [x] **2. USS stylesheet** — `MainMenu.uss` with layout, button, modal, and preset styles.
- [x] **3. Root UXML** — `Root.uxml` with all screens (main menu, mode select, settings, quit modal) inline.
- [x] **4. MainMenuController** — screen navigation, preset selection with highlight, scene transition, desktop-only quit modal.
- [x] **5. Wire scene** — UIDocument with Root.uxml, PanelSettings, MainMenuController reference.
- [x] **6. Update GameController** — `GameSettings` integration, `useRandomSeed` toggle, `Awake` initialization. Menu flow always randomizes seed regardless of inspector toggle.
- [x] **7. Fix input after scene transition** — removed unused UI action map from InputActionAsset that was overriding Gameplay map.
- [x] **8. Fix mode select overflow** — reduced element sizes and added scroll fallback on `.screen` container.
- [x] **9. Update docs** — TechnicalDesign.md, GDD.md, CLAUDE.md updated with menu architecture.

---

## Manual Test Cases

### Main Menu Screen

- [x] **MM-1: Initial state** — Title "Arrow Thing" visible. Play and Settings buttons visible and centered.
- [x] **MM-2: Quit button visibility (desktop)** — X button visible in top-left on desktop.
- [x] **MM-3: Quit button hidden (mobile)** — X button not visible on Android build.
- [x] **MM-4: Play navigates to Mode Select** — Main Menu disappears, Mode Select appears.
- [x] **MM-5: Settings navigates to Settings** — Main Menu disappears, Settings appears.

### Mode Select Screen

- [ ] **MS-1: Initial state** — All elements visible and on-screen. *(was off-screen before overflow fix — retest)*
- [x] **MS-2: Default selection** — Small preset highlighted by default.
- [x] **MS-3: Preset selection highlight** — Highlight toggles correctly between presets.
- [x] **MS-4: Back returns to Main Menu** — Mode Select disappears, Main Menu reappears.
- [x] **MS-5: Selection persists after Back** — Selected preset retained after Back → Play.
- [x] **MS-6: Start with Small** — 10×10 board loads correctly.
- [x] **MS-7: Start with Medium** — 20×20 board loads correctly.
- [x] **MS-8: Start with Large** — 40×40 board loads correctly.
- [x] **MS-9: Random seed** — Different boards on repeated starts.

### Settings Screen

- [x] **ST-1: Initial state** — "Settings" label, "Coming soon" text, Back button visible.
- [x] **ST-2: Back returns to Main Menu** — Settings disappears, Main Menu reappears.

### Quit Modal (Desktop Only)

- [x] **QM-1: X opens modal** — Dark overlay with "Quit game?" and Yes/No.
- [x] **QM-2: No dismisses modal** — Modal dismisses, menu interactive.
- [x] **QM-3: Yes quits** — Play mode stops in editor.
- [x] **QM-4: Modal blocks menu** — Buttons behind overlay not clickable.

### Input After Scene Transition

- [x] **IN-1: Tap to clear** — Clearable arrow clears with animation.
- [x] **IN-2: Tap blocked arrow** — Bump animation plays.
- [x] **IN-3: Pan** — Camera pans on drag.
- [x] **IN-4: Zoom (scroll)** — Scroll wheel zooms.
- [x] **IN-5: Zoom (pinch)** — Pinch gesture zooms on Android build.

### Editor Workflow

- [x] **ED-1: Direct Game scene play** — Inspector values used, input works.
- [x] **ED-2: Fixed seed override** — Identical boards with fixed seed.
- [x] **ED-3: Random seed default** — Different boards each run.

---

## Known Issues (Out of Scope)

- **Mobile UI scaling** — UI is too zoomed in on vertical mobile form factor. Scoped out for now; mobile UX is a separate pass.

---

## Open Questions

_None — all scoping decisions resolved._
