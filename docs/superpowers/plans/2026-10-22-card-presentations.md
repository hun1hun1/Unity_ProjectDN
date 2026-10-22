# Card Presentations Implementation Plan
> **For agentic workers:** Use superpowers:executing-plans to implement task by task.
**Goal:** Optional reusable before-impact/after-impact sequences on cards, with existing effects remaining authoritative.
**Architecture:** CardPresentation and stateless ScriptableObject steps; a per-use disposable context owns motion, sprite playback and temporary objects. BattleManager wraps actual card uses only, including continuous-card activation. Empty presentations preserve current behavior.
**Tech Stack:** Unity 6000.0.77f1, C#, uGUI, Unity Test Framework.
**Spec:** The accepted design in this chat and docs/CardPresentations.md.
## Global Constraints
Preserve source encodings, effect order, costs, repeat count, death handling and existing asset GUIDs. No new packages or balance changes. Work in this authorized checkout; generated art remains untouched. No commits/push requested.
## Review Focus
Cancellation before/after impact restores motion and idle; layout changes preserve transient offsets; no-presentation and X-cost repeats keep legacy effects; shared assets hold no runtime state; missing visual references do not prevent game effects.
## Tasks
- [x] Add failing reflection-based Editor tests for optional configuration, ordering, cancellation, layout and one-shot sprites; run matching Unity Editor.
- [x] Implement definition, context, move/wait/sprite/VFX/projectile/sound steps, layout-safe offsets, one-shot playback and actual-use integration.
- [x] Add usable melee sample preset and editor importer for generated soldier firing frames, documentation and serialized-reference checks.
- [x] Run focused and complete Unity tests, review the complete change, fix material findings and report actual verification.

## Execution ledger
- Initial feature tests: 0/9 passed before implementation because the optional definition/bridge did not exist; 9/9 passed after the core implementation.
- Disabled sprite playback regression reproduced an unexpected timed wait before the fix; playback now observes disabled/replaced animators.
- Ruling: retain the current authorized checkout and untracked generated art, with no commit or branch switch, to preserve the user's ongoing asset workflow.
- Ruling: import the supplied Fire-v2 sheet and bind only the soldier's previously unassigned basic dream Attack card. This makes the feature concrete without classifying every attack as melee or changing game effects.
- Definitions/steps are stateless; execution contexts capture motion and complete sprite playback state and dispose via the existing EffectSequence stack.
- Independent read-only review found no material regressions. Asset scan found 10 presets, no missing GUIDs or duplicate GUIDs; original non-ASCII source bytes and CRLF were preserved.
- Final Unity 6000.0.77f1 EditMode run: 448/448 passed, zero failed/skipped, including all 15 CardPresentationTests. Logs/CardPresentation-full.xml contains the result.
- Restored the previously clean NotoSansKR font atlas after Unity's dynamic-font serialization during tests. No scene/prefab/layout changes were retained. New presentation art has not been manually inspected in Game View.

