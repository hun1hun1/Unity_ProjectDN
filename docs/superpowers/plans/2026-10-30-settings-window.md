# Large Settings Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** SettingButton opens a deck-overview-sized settings window with three audio volumes, fullscreen, resolution, reset, return, close/ESC and quit confirmation.

**Architecture:** A settings service owns validated persisted values through a replaceable platform backend. SettingsUI binds uGUI controls and manages modal/input lifetime. SettingsAudioSource applies Music/Effects gain while AudioListener owns master gain. Card presentation audio uses the Effects bus.

**Tech Stack:** Unity6000.0.77f1, uGUI, TMP, PlayerPrefs, Unity Test Framework; no new packages.

**Spec:** User-approved settings proposal, expanded to the same large area as DeckOverviewPanel and including resolution selection.

## Constraints and rulings

- Work in the current checkout, preserving current source encoding, scene art/hierarchy, references and GUIDs; no Git mutations or commits.
- Window fills Canvas area below TopArea, with audio left, video right, bottom actions. Keep the existing SettingButton transform/art.
- Audio changes apply and save immediately. Resolution/fullscreen are staged together and committed with Apply. Reset restores full audio and the display configuration captured at service startup.
- Only open when battle.CanInspectCards and no card inspection owns input. Close deck/pile viewers first. Block hand/use/end-turn/pile/deck/inspection input while settings is open; restore after the closing frame.
- ESC closes the foremost settings subwindow: quit confirmation, dropdown, then settings. Return/X/SettingButton close normally. Quit requires confirmation.
- No Time.timeScale change; do not interrupt an in-flight card presentation.
- UI tests use a fake platform, never native resolution changes, actual quit or real preference writes. Editor native display changes are skipped; standalone player applies Screen.SetResolution.
- Preserve the original source gain for category audio; do not compound volume multipliers on repeated changes.

## Tasks

- [x] Test-first scene contract; observe missing settings wiring failure.
- [x] Implement state/service/platform persistence, supported display choices, clamping, reset and quit.
- [x] Implement large SettingsUI, audio routing and battle/modal guards; verify fake-platform tests.
- [x] Create serialized panel/controls/popup/confirmation with Editor utility and existing button callback.
- [x] Run focused and relevant regression tests, inspect two-resolution renderings and obtain one final fresh-context review.
- [x] Remove temporary validation artifacts from Assets, restore automatically changed font data, check source encoding/references/diff, and document settings locations and Editor limitations.
