# Title menu implementation plan

> **For agentic workers:** Use superpowers:executing-plans. Track steps below.

**Goal:** Create an art-free TitleScene with functioning Start, Tutorial, Options and Quit buttons.

**Architecture:** TitleMenuUI owns title modals and starts the existing RunSession. SettingsUI retains its battle defaults and gains an explicit title context. An Editor setup saves a wired uGUI scene and prepends it to Build Settings.

**Tech Stack:** Unity 6000.0.77f1, uGUI, TMP, Unity Test Framework.

**Spec:** User request in this chat: background/logo later; implement the title scene and all four button functions now. Tutorial uses four instructional pages; options reuse the existing settings window; quitting requires confirmation.

## Global constraints
- Preserve all current uncommitted work and existing game balance/data.
- Preserve encodings, asset GUIDs and prefab connections. No package additions.
- No commits or worktree changes; changes must integrate with current uncommitted UI assets.

## Review focus
- Repeated Start clicks must not initialize two runs.
- Tutorial and cancelled quit must not modify run resources.
- Closing modals must not activate the underlying menu in the same frame.
- Title settings must remain open without a BattleManager; battle behavior must remain unchanged.
- Build startup must retain MapScene and BattleScene and load TitleScene first.

## Task 1: Title scene and menu
- [x] Add a failing scene/build wiring test; run it and observe missing scene.
- [x] Implement TitleMenuUI, shared settings context and TitleSceneAssetSetup.
- [x] Generate TitleScene using the matching Unity Editor, preserve prefab wiring.
- [x] Add runtime tests for tutorial navigation, options, quit guards and new-run transition; run them.

## Task 2: Verification and delivery
- [x] Run relevant settings/map/title regressions with the matching Editor.
- [x] Render and inspect title/modal layouts at 1920x1080 and 1280x720.
- [x] Read-only final review; address actionable findings and recheck.
- [x] Verify serialized references/diffs and document editable tutorial contents.
