# TODO: Custom Snap Slider Component

## Summary

Replace the raw `SliderInt`/`Slider` controls with a reusable `SnapSlider` component that
includes step buttons and an optional snap-to-grid lock. The immediate motivation is the
custom board-size picker: a range of 2–400 with step 1 makes precise values (e.g. 200, 300)
impractical to target on mobile.

## Design

### Layout (left to right)

```
[──────slider──────] [val] [+]
                           [-]  [■/□]
```

1. **Slider** — fills remaining space; styled as a flat bar (track height == dragger height,
   dragger is a brighter colour).
2. **Value label** — fixed-width display of the current value.
3. **Step buttons** — vertical column: `+` on top, `−` on bottom.  Step = `smallStep` in free
   mode, `snapStep` in locked mode.
4. **Lock button** — optional; `■` when locked (snapped to grid), `□` when free.  Hidden when
   `showLock = false` (used for settings sliders, which have no coarse grid).

### Snap grid for custom board size (2–400)

- Snap step: 10.  Valid snap points: 2, 10, 20, 30 … 390, 400.
- Starts locked by default.
- `+`/`−` step by 10 when locked, 1 when free.
- The slider also snaps on drag when locked.

### Settings sliders (no lock)

- Drag threshold (5–60 px, step 1, display as integer).
- Zoom speed (0.2–5, step 0.1, display as F1).

## Implementation Plan

- [x] Create `docs/TODO.md`
- [ ] `Assets/Scripts/View/SnapSlider.cs` — pure C# UI component, no MonoBehaviour.
- [ ] `Assets/UI/Root.uxml` — remove inline `SliderInt`/`Slider`+value-label elements; add
      named row and host containers.
- [ ] `Assets/UI/MainMenu.uss` — add `.snap-slider*` rules; change `preset-btn--custom` from
      fixed height to `height: auto`.
- [ ] `Assets/Scripts/View/MainMenuController.cs` — replace raw slider fields with
      `SnapSlider` instances; rewire callbacks.
- [ ] `Assets/Tests/PlayMode/UILayoutTests.cs` — update `Settings_AllElementsVisible` to
      query the new named row containers instead of the removed named sliders.

## Open Questions

None — all design decisions resolved before implementation.

## Manual Test Cases

### Custom board-size sliders (width & height)

- [ ] **Locked snap drag** — Slider starts locked (■). Drag the slider; value should snap to multiples of 10 (2, 10, 20, 30 … 400). Label updates to match.
- [ ] **+/- buttons always step by 1** — In both locked and unlocked mode, `+` and `−` step by 1. Verify clamping at 2 (min) and 400 (max).
- [ ] **Unlock toggle** — Click the lock button (■ → □). Slider should now move freely in steps of 1.
- [ ] **Re-lock preserves value** — Set a non-grid value (e.g. 37) via +/-, then click lock (□ → ■). Value should stay at 37 (no snap on toggle). Next drag will snap.
- [ ] **Play and return** — Select custom preset, set width=50 height=30, start a game, go back to menu. Custom card should still show 50×30.

### Settings sliders

- [ ] **Drag threshold** — Open settings. Slider range 5–60. Drag moves continuously. `+`/`−` step by 1. No lock button visible. Value displays as integer.
- [ ] **Zoom speed** — Slider range 0.2–5.0. `+`/`−` step by 0.1. No lock button visible. Value displays with one decimal (e.g. "1.5").
- [ ] **Persistence** — Change drag threshold to a non-default value, close and reopen the app. Value should be restored from PlayerPrefs.

### Layout

- [ ] **Desktop** — All snap-slider rows fit within their containers. No overflow or clipping on the custom card or settings panel.
- [ ] **Custom card height** — Selecting the custom card expands it (height:auto). Deselecting should collapse it cleanly.

### Integration

- [ ] **Start game with custom size** — Select custom, set 15×25, press Start. Board should generate at 15×25.
- [ ] **Preset still works** — Select Small (10×10), start game. Board generates at 10×10 (presets bypass snap sliders).
