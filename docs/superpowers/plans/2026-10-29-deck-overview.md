# Dual Deck Overview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** DeckButton toggles a panel showing every owned Dream card on the left and Nightmare card on the right.

**Architecture:** Reuse DeckListUI twice, retaining DeckCard prefab visuals, independent card scale, hover and right-click inspection. DeckOverviewUI owns the two lists, titles, toggle and battle-input lifetime. An Editor setup utility adds the serialized scene hierarchy and preserves existing art, layout and button events.

**Tech Stack:** Unity 6000.0.77f1, uGUI, TextMeshPro, Unity Test Framework.

**Spec:** User-approved design in this conversation: two three-column independently scrolling lists below TopArea; GetDeck(TypeDream/TypeNightmare) includes the whole current run deck and duplicate copies; name ordering for overview only; DeckButton and close button toggle; prevent battle inputs while viewing; allow card inspection and prevent overview toggling while inspection captures input.

## Global Constraints

- Preserve all current local scene/art/font/test edits, GUIDs and existing C# encodings.
- No gameplay/balance changes, packages, commits or worktree migration.
- Keep existing pile viewers' random presentation and card sizing defaults.
- Overview browsing must not mutate decks, piles, RNG or battle state.

## Review Focus

- Cards in hand/discard/exhaust remain listed; rewards/removals and duplicates appear on reopen.
- Overview input blocking must not block right-click inspection of its own cards.
- Switching, disabling and battle end release blockers and clear cloned cards.
- Two ScrollRects remain independent; Card Scale and fonts never compound.
- TopArea button remains reachable and existing pile viewer tests target the original list explicitly.

### Task 1: Regression contract

- [x] Add scene wiring and runtime tests for complete sorted decks, toggle, scroll, preview, input and lifecycle.
- [x] Run a scene contract test and observe missing DeckOverviewUI failure.

### Task 2: Runtime UI

- [x] Add optional serialized name sorting to DeckListUI, default false.
- [x] Implement DeckOverviewUI with configured lists/controller/battle/button, current deck snapshots, counts and cleanup.
- [x] Gate battle use/end-turn, hand hover and pile input while overview is open; retain CardInspectionUI's current semantics.

### Task 3: Scene and verification

- [x] Build two ScrollRect/Viewport/Content sections with GridLayoutGroup and content sizing, close button, headers and DeckCard bindings.
- [x] Connect existing DeckButton without altering user art or unrelated hierarchy.
- [x] Run focused tests and existing list/hover/inspection/status/tooltip regressions; render two reference resolutions.
- [x] Obtain one final read-only review; repair material issues and verify.
- [x] Restore automatically changed font data from this task's baseline; inspect scene/GUID diffs and document Inspector settings.
