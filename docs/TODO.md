# Feature: Automated UI Layout Tests

## Goal

Add a PlayMode test suite that programmatically verifies all UI elements are visible and not clipped across multiple aspect ratios (16:9, 4:3, 21:9, 9:16 portrait, 1:1 square). Catches layout regressions on every PR without manual testing.

## Design Decisions

- **PlayMode tests** (not EditMode) because UI Toolkit layout requires a runtime panel to resolve element bounds.
- **No test scene** — tests load UXML + PanelSettings via `AssetDatabase` onto a runtime `UIDocument`. Everything is in code.
- **PanelSettings.referenceResolution** is modified at runtime to simulate aspect ratios. Original values are saved/restored in SetUp/TearDown.
- **Portrait (9:16) known failures** use `Assert.Warn()` so CI stays green. Convert to hard asserts when responsive CSS is implemented.
- **Victory font tiers** — all 3 (40px, 28px, 20px) are tested with representative messages.

## Implementation

- [x] `Assets/Tests/PlayMode/ArrowThing.Tests.PlayMode.asmdef` — PlayMode test assembly
- [x] `Assets/Tests/PlayMode/UILayoutTestHelper.cs` — Reusable assertion utilities
- [x] `Assets/Tests/PlayMode/UILayoutTests.cs` — 7 UI states x 5 ratios = 35 test cases
- [x] `.github/workflows/ci.yml` — Added `test-playmode` job
- [x] `docs/TechnicalDesign.md` — Updated Testing Strategy, CI, and Decision Log sections

## Manual Test Cases

1. **Test Runner visibility** — Open Unity Test Runner > PlayMode tab. Verify 35 tests appear under `UILayoutTests`.
2. **Desktop ratios pass** — Run all tests. 16:9, 4:3, 21:9, 1:1 tests should pass (green).
3. **Portrait warnings** — 9:16 tests should show as warnings (yellow), not failures (red).
4. **PanelSettings restoration** — After running tests, verify PanelSettings asset hasn't been permanently modified (check reference resolution in Inspector is still 1200x800).
5. **CI integration** — Push to a branch and open PR. Verify `PlayMode tests` check appears in GitHub alongside `EditMode tests` and `format`.

## Test Results

| Test Case | Result |
|-----------|--------|
| Test Runner visibility | |
| Desktop ratios pass | |
| Portrait warnings | |
| PanelSettings restoration | |
| CI integration | |
