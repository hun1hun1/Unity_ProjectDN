# Existing Test Regression Repairs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair the ten stale test cases and verify the graphics-dependent HUD test using a rendering-enabled Unity Editor.

**Architecture:** Update tests to match the current asset paths, serialized hand padding and asynchronous card resolution contract. Preserve gameplay code, existing assertions, user edits and serialized asset bytes.

**Tech Stack:** Unity 6000.0.77f1, Unity Test Framework 1.6.0, NUnit, uGUI.

**Spec:** User request to fix the previously diagnosed failing tests in this chat.

## Global Constraints

- Preserve existing UTF-8 encoding and each file's newline convention.
- Do not change gameplay, card balance, scenes, prefabs, packages, or existing unrelated work.
- Use the matching Editor with rendering enabled for graphics-dependent tests.
- Leave changes uncommitted for review.

## Review Focus

- Card front tests must load both moved images and still check bindings, artwork and backs.
- Hand tests must retain resize, draw, use, discard, drag, font and original-prefab checks.
- Effect tests must wait for real completion with bounded realtime deadlines.
- Turn tests must actually leave PlayerTurn and wait for the next ready hand.
- Rendering tests must execute with graphics rather than silently skip a required assertion.

## Task 1: Repair tests and verify

- [x] Confirm existing failures in all.xml and baseline-failures.xml; target test sources remain unchanged since those runs.
- [x] Update CardFrontTests.cs paths to Assets/Images/UI/Card/SoldierCardFronts/ and assert sprite loading before using the images.
- [x] Update HandLayoutTests.cs expectations for 44px vertical padding: 173.84615x226, 121.92308x158.5, 70x91; retain all font and animation assertions.
- [x] Move effect-completion waits before result assertions in DefenseBreakTests.cs and StatusEffectTests.cs; ensure initial hands and subsequent turns are ready.
- [x] Update ContinuousTurnEffectTests.cs to wait for accepted card resolution, assert accepted turn end, and observe expiry before the next draw.
- [x] Mark the SharedHudArtworkTests rendering test RequiresGraphics without weakening its rendering requirement.
- [x] Run the affected classes with Unity rendering enabled; investigate any newly exposed failures.
- [x] Run the complete EditMode suite with rendering enabled, including its PlayMode transitions.
- [x] Review the final test diff, restore only automatic serialization changes to the captured baseline, and report exact test results.

Verification: Unity 6000.0.77f1, rendering enabled, complete EditMode suite 487/487 passed with zero failures and zero skipped tests. The eleven previously failing cases all passed. Read-only review found no material issues. Automatic scene/prefab serialization was restored after verification. A concurrent user commit b466f82 included font cache changes; those committed font changes were preserved instead of restoring the older font snapshot. Results: Logs/TestRegressionFix-20261030/all.xml.
