# Map System Implementation Plan

> Execution: implement inline in the current authorized workspace. The user explicitly approved the conversational design and requested implementation; do not repeat the approval handoff. Preserve existing standalone battle behavior.

**Goal:** Build configurable floor generation and persistent run progression, with a playable temporary MapScene that ends after five normal battle victories and their rewards.

**Architecture:** ScriptableObject run/floor/encounter definitions feed a deterministic generator and a pure run progression model. A persistent RunSession owns scene transitions and existing run inventories; a scene-local bridge restores HP and configures battles before BattleManager starts. Map UI displays generated state and dispatches supported choices.

**Tech Stack:** Unity 6000.0.77f1, C#, existing uGUI/TMP/Input System, Unity Test Framework.

**Spec:** Approved conversation design plus the user's temporary normal-battles-only requirement.

## Global Constraints

- Preserve existing C# encodings and CRLF, shared asset definitions, battle/card rules and serialized references.
- Temporary floor: exactly five normal battle nodes; no boss, midpoint pair or nonbattle choices. Complete only after the fifth reward completion.
- Planned floor: first normal only, last boss only, one middle normal/elite choice, immediately followed by relic only regardless of selected battle.
- Ordinary planned nodes: normal mandatory, at most one weighted nonbattle choice, independent optional elite choice.
- No shops, event outcomes, relic grants, card upgrades, title scene or game-over animation in this task. Unsupported content cannot be selected; expose a handler boundary.
- Existing inventory data remains authoritative; run HP and map state survive scene changes. No disk save feature.
- Unity generates and saves scenes/assets/metas. Do not edit Unity-generated source/cache/build artifacts.

## Review Focus

1. Duplicate/stale completion must not advance a subsequent node or new run.
2. Victory must wait for rewards; defeat/cancellation must not advance or offer rewards.
3. Scene re-entry must preserve HP/deck/gold/relics and never regenerate the active floor.
4. Planned floor boundaries and unavailable mandatory content must fail clearly.
5. Direct BattleScene entry must retain existing behavior with no active run.

## Task 1: Generation and progress model

Files: Assets/Scripts/Map/{FloorMapDefinition,RunDefinition,BattleEncounterDefinition,MapNode,MapGenerator,RunProgress}.cs; Assets/Tests/Editor/MapSystemTests.cs.

Interfaces: MapGenerator.Generate(FloorMapDefinition, IReadOnlyCollection<MapContentType>, int); RunProgress.TrySelectOption(int), RecordBattleResult(BattleResult), CompleteNode(int floorIndex, int nodeIndex).

- [x] Write reflection-based absent-feature regression tests; run Unity and observe missing-system failures.
- [x] Implement configurable generation, validation, deterministic choices and guarded progress states.
- [x] Verify generation across floor lengths/seeds, probability boundaries, rewards gate and exactly-five completion.

## Task 2: Run/session and battle bridge

Files: Assets/Scripts/Map/{RunSession,RunBattleBridge,MapContentHandler}.cs; targeted additions to BattleCharacter.cs, Enemy.cs, BattleRewardSystem.cs; Assets/Tests/Editor/MapSceneTests.cs.

Interfaces: RunSession.StartNewRun(RunDefinition,int), SelectOption(int), CompleteContent(expected progress/floor/node); RunFinished event for later title/game-over integration.

- [x] Add health restoration and scene round-trip tests before production changes.
- [x] Implement coordinated resets, persistent progress, battle injection before start, outcome/reward handling and guarded scene transitions.
- [x] Preserve standalone BattleScene behavior; validate five real scene round trips, defeat/cancel and retained rewards/HP.

## Task 3: Map scene and serialized data

Files: Assets/Scripts/Map/MapController.cs; Assets/Editor/MapAssetSetup.cs; Assets/MapData/TemporaryRun.asset, TemporaryFloor.asset, NormalEncounter.asset; Assets/Scenes/MapScene.unity; BattleScene bridge binding; EditorBuildSettings.asset.

- [x] Implement uGUI display with runtime-generated node indicators, choice buttons, HP/gold/progress and terminal status.
- [x] Generate and save assets/scene through Unity Editor APIs; preserve existing battle content and add only the bridge.
- [x] Set MapScene as first build scene and retain BattleScene; assert references, configured normal-only offers and terminal UI.

## Task 4: Verification and documentation

- [x] Run full Unity EditMode suite including scene/PlayMode-entry tests; inspect XML and compile diagnostics.
- [x] Perform whole-change review; fix meaningful issues and rerun affected verification.
- [x] Document actual settings, extension points, limitations and verification evidence in docs/MapSystem.md.
- [x] Leave changes reviewable in the current workspace; no push, merge or unsolicited integration.
