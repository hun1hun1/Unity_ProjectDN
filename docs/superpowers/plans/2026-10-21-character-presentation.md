# Character Presentation Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan inline. The user has requested implementation of the previously discussed UI design.

**Goal:** Apply the generated player/enemy idle artwork, align feet with HUDs, and allow enemy-specific UI profiles and HUD prefab replacement without changing combat rules.

**Architecture:** Keep BattleCharacter roots, encounters and combat references. A separate Visual child plays a sprite sequence. CharacterUIProfile assets select independent artwork sizes, fixed-font HUD prefabs and intent placement above actual visible artwork. BattleCharacterPresentation swaps and rebinds owned HUDs while preserving HP, block, statuses and delayed effects.

**Tech Stack:** Unity 6000.0.77f1, uGUI, TextMeshPro, Unity Test Framework. No packages added.

**Spec:** The user's approved conversation design: independent visual/info sizing; small/normal/large enemy profiles; common ground baseline; current PNG idle frames; no balance changes.

## Global Constraints

- Existing CP949 C# and CRLF must be preserved.
- Preserve existing GUIDs, existing scene/prefab overrides and the user's unrelated changes.
- GeneratedArt originals are unchanged; copy selected PNG sheets into Assets.
- No new encounters or behavior/balance changes. Existing TestEnemy displays Slime; Scarecrow and Lizard are selectable enemy prefabs.
- Profiles expose visual size/offset, HUD prefab/size/offset and intent prefab/size/gap in the Inspector.
- All sequences contain 24 768x768 cells, ordered left-to-right/top-to-bottom. Durations: Soldier 0.06s, Slime 0.08s, Scarecrow 0.10s, Lizard 0.12s. Ground pivot (0.5,0.0625).

## Review Focus

- HUD replacement must preserve current HP/block/statuses and delayed-effect bindings.
- A long animation frame must wrap correctly; disable/enable must restart once without duplicate coroutines.
- Inactive staged enemy initialization must retain encounter HP and bind the selected intent before play.
- Existing legacy encounters without presentation must retain their external intent UI.
- Uniform fixed-cell import, full alpha, stable sprite IDs and actual artwork bounds must prevent ground jitter and HUD/intent overlap.

## Task 1: Runtime presentation and animation

Files: new Assets/Scripts/UI/{UISpriteAnimation,UISpriteAnimator,CharacterUIProfile,BattleCharacterPresentation}.cs; targeted changes to BattleCharacter.cs, BattleManager.cs and EnemyActionUI.cs; new CharacterPresentationTests.cs.

- [x] Add failing tests for sequence timing, profile replacement/vitals, foot alignment and missing imported configuration.
- [x] Run the focused Unity tests and confirm RED.
- [x] Implement independent presentation, owned HUD rebinding, periodic image animation and legacy intent fallback.
- [x] Run focused tests and inspect failures before proceeding.

## Task 2: Import and serialized wiring

Files: new Assets/Editor/CharacterPresentationAssetSetup.cs; Assets/Images/Characters; Assets/CharacterPresentation; new UI and enemy prefabs; existing Soldier/TestEnemy prefabs and BattleScene integration.

- [x] Add an idempotent Editor setup command that imports four full-resolution sheets with stable sprite IDs and creates animation/profile assets.
- [x] Create Small/Normal/Large HUD variants with fixed font/icon sizes and an owned intent UI prefab.
- [x] Preserve old HUD object IDs as inactive legacy children; add Visual/new HUD children and serialized presentation bindings.
- [x] Assign Soldier/Slime to existing prefabs; create Scarecrow/Lizard enemy prefabs inheriting existing combat values.
- [x] Keep existing encounter selection and make owned intent preferred over scene fallback.
- [x] Verify setup idempotence, imports, missing references and source encodings.

## Task 3: Unity verification and documentation

- [x] Run focused tests then the entire EditMode suite.
- [x] Exercise scene playback and profile/prefab replacement in Unity; capture rendered results at 1920x1080 and another aspect ratio.
- [x] Document exact Inspector choices, timing and legacy fallback in docs/CharacterPresentation.md.
- [x] Review final changes and report source, serialized changes, test results and runtime verification separately.

## Execution notes

Work remains in the current checkout because the generated art and existing uncommitted enemy-action work are task inputs. No worktree, branch, commit or package installation is needed. Unity CLI is installed; no connected Pipeline Editor was found, so use the matching installed Editor in batch mode.

## Verification results

- Unity 6000.0.77f1: all 11 new presentation tests passed, including real PlayMode frame advancement and enemy replacement.
- Full EditMode run: 418/419 passed; the sole failure was an existing capture helper requiring .utmp/enemy-prefabs. After creating that ignored output directory, the failed map/battle/reward test and 10 presentation unit tests passed (11/11). No test source outside this feature was changed.
- Rendered and inspected Soldier/Slime at 1920x1080, plus Scarecrow/Lizard at 1920x1080 and 1600x1200.
- Setup rerun: all 41 compared asset/prefab/scene files retained identical SHA256 hashes. Profile choices, sprite IDs and UI prefab links were preserved.
- No missing new .meta files. Existing BattleCharacter/BattleManager CP949 and CRLF were preserved. GeneratedArt sources and combat balance data were not changed.
