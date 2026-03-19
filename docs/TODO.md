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

(to be filled in after implementation)

- [ ] Custom width slider: drag freely, drag in locked mode (snaps), +/- in locked mode, toggle lock.
- [ ] Custom height slider: same.
- [ ] Settings drag threshold: +/- step by 1, slider moves continuously.
- [ ] Settings zoom speed: +/- step by 0.1.
- [ ] Restore state: returning from a game restores the correct custom dimensions.
- [ ] Mobile: all buttons tappable, no layout overflow.
