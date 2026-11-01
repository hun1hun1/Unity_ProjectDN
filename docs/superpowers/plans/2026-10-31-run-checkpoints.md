# Run checkpoint implementation plan

> **For agentic workers:** Use superpowers:executing-plans, with one fresh final read-only review.

**Goal:** Return from battle settings to TitleScene; show Continue only for a valid durable save; restart battles at entry and retain committed rewards/content acquisitions.

**Architecture:** Serializable run/inventory/reward/content snapshots use a runtime asset catalog with stable GUID keys and a single JSON checkpoint file replaced atomically. RunSession owns checkpoint boundaries, resume and suspension. RunBattleBridge restores either a new battle or its pending reward phase. MapContentHandler exposes state capture/restore and a transaction commit hook for future shop/relic implementations.

**Tech Stack:** Unity 6000.0.77f1, C#, JsonUtility, existing uGUI/TMP and ScriptableObject definitions.

**Spec:** Accepted design in this chat: checkpoint at node entry, then update when reward/purchase commits. Pending rewards and shop stock must not reroll or duplicate. No shop/relic gameplay UI exists yet; implement and test its reusable persistence contract without inventing that gameplay.

## Global constraints
- Preserve current dirty files, encoding/line endings and GUIDs. No balance changes, package additions or builds.
- Existing live workspace integrates prior uncommitted UI work; use .utmp/run-save/baseline and no git mutations.
- Test save files must be isolated from the user's actual saved game.
- IO/validation failure must preserve the previous save and surface a readable error; no silent fresh run on Continue.

## Review focus
- Stale battle/content callbacks during scene changes cannot commit or grant rewards.
- Reward ownership and receipt flags form one checkpoint, including partial rewards and fixed offers.
- Completed/defeated runs cannot resurrect an old checkpoint.
- Save restoration validates assets and all data before replacing inventories.
- Returning mid battle retains entry HP/deck/resources and initial draw order; content restores its last committed state.

## Task 1: Persistence and resume
- [x] Observe failing serialized UI/save contract tests.
- [x] Implement DTOs, catalog, validated run/inventory snapshots and atomic file storage.
- [x] Integrate node entry, victory/reward mutation, completion and content transaction checkpoints.
- [x] Test JSON roundtrips, missing assets, corrupt saves, write failure and transaction rollback.

## Task 2: Scene flow and UI
- [x] Add conditional Continue, connect battle title return with errors and confirmation.
- [x] Restore battle entry or reward UI; restore content handler state on MapScene load.
- [x] Patch scenes with Editor utilities and keep all existing prefab bindings.
- [x] Run runtime restart/partial reward/content tests and relevant regressions.

## Task 3: Delivery
- [x] Render title with/without Continue and battle confirmation; inspect both resolutions.
- [x] Final read-only review; fix actionable defects and verify.
- [x] Check serialized refs/diffs, restore incidental font changes and document save boundaries/API.
