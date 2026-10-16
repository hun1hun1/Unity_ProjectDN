# Delayed Effects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 카드 사용별 N턴 지연 일회 효과, 지속 카드의 지연 종료·버림/소멸, 상태창 예약 표시와 전투 종료 정리를 구현한다.

**Architecture:** DelayEffect는 정의만 가지며 BattleManager가 소유한 DelayedEffectSystem에 사용별 PendingDelayedEffect를 등록한다. 기존 순차 효과 실행에 원본 카드 컨텍스트를 전달하고, 턴 시작 후 지속 효과와 드로우 사이에서 만기 예약을 실행한다. 기존 상태창에 예약 아이콘을 연결하며 사망을 전투 종료 진입점에 연결한다.

**Tech Stack:** Unity 6000.0.77f1, C#, uGUI, TMP, Unity Test Framework. 패키지를 추가하지 않는다.

**Spec:** docs/superpowers/specs/2026-10-16-delayed-effects-design.md

## Global Constraints

- 사용한 턴은 계산하지 않는다. 플레이어 턴 시작을 N번 맞이했을 때 발동한다. N은 1 이상이다.
- 같은 시점에 발동하는 예약은 등록 순서대로 실행한다. 실행 중 새 예약이 생겨도 같은 단계에서 재실행하지 않는다.
- 카드 코스트와 필수 탄약은 사용 시 한 번 지불한다. 하위 효과 자체의 탄약 조건은 실제 발동할 때 평가한다.
- 지연 지속 카드의 비소멸 사용 건은 원래 꿈/악몽 버림 더미로 이동시킨다. 소멸 더미에 같은 사용 건을 중복 추가하지 않는다.
- 전투 종료·비활성화·초기화 시 예약을 취소한다. 취소 시에는 지연 효과나 카드 반환을 실행하지 않는다.
- 기존 카드에 효과를 임의로 추가하지 않으며 밸런스·보상 데이터베이스는 변경하지 않는다. 기존 인코딩·개행과 GUID를 보존하고 새 Unity 소스에는 .meta를 함께 둔다.
- 커밋·푸시와 패키지 설치는 구현에 필요하지 않다. 검토 시점까지 현재 체크아웃과 사용자 변경을 보존한다.

## Review Focus

- 턴 시작 중 새로 등록한 예약은 다음 플레이어 턴부터 계산한다. Task 1에서 반복 효과와 중첩 예약을 검증한다.
- 같은 에셋을 동시에 사용해도 원본 카드·예약·지속 수명이 섞이지 않는다. Task 1·2에서 중첩 효과와 중복 사용을 검증한다.
- 멀리건 입력 대기 중 종료·비활성화해도 다음 효과·카드 반환·잠금이 남지 않는다. Task 4에서 실제 씬 수명주기를 검증한다.
- 영구 지속 소멸 예외는 지연 종료 사용 건에만 적용한다. Task 2에서 기존 영구 카드와 실패한 조건부 지연 등록을 비교한다.
- 열린 툴팁, 상태창 비활성화와 다른 전투 재바인딩에서도 오래된 예약을 표시하지 않는다. Task 3에서 실제 프리팹으로 검증한다.

## Task 1: 일회 예약과 턴 실행

**Files:**
- Create: `Assets/Scripts/Card/CardEffects/DelayEffect.cs`, `Assets/Scripts/Card/PendingDelayedEffect.cs`, `Assets/Scripts/Card/DelayedEffectSystem.cs` 및 각 `.meta`.
- Modify: `Assets/Scripts/Manager/BattleManager.cs`, `Assets/Scripts/Card/ActiveContinuousCard.cs`, `Assets/Scripts/Card/ContinuousCardSystem.cs`.
- Test: `Assets/Tests/Editor/DelayedEffectTests.cs` 및 `.meta`.

**Interfaces:**
- `DelayEffect : CardEffect`: 직렬화 필드 `delayTurns`, `effects`, `description`, `icon`; 읽기 전용 `DelayTurns`, `Effects`, `Description`, `Icon`, `IsValid`. `Execute(BattleManager)`와 `ActivateContinuousRoutine(BattleManager, ActiveContinuousCard)`는 유효한 예약만 등록하고 즉시 완료한다.
- `PendingDelayedEffect`: 읽기 전용 `Id`, `Card`, `DueTurn`, `Effects`, `Icon`, `Description`, `ContinuousSource`; `RemainingTurns`는 예약 시스템의 현재 턴을 기준으로 계산한다. Effects 배열은 등록 시 복사한다.
- `DelayedEffectSystem`: `long CurrentTurn`, `IReadOnlyList<PendingDelayedEffect> PendingEffects`, `event Action Changed`; `PendingDelayedEffect Schedule(DelayEffect effect, CardData card, ActiveContinuousCard source = null)`, `void AdvanceTurn()`, `IEnumerator ProcessDueEffectsRoutine(BattleManager battle)`, `void Clear()`.
- `BattleManager`: `DelayedEffectSystem DelayedEffects`, `CardData CurrentEffectCard`; 기존 `ExecuteEffectsRoutine(CardEffect[] effects, ActiveContinuousCard source = null, CardData sourceCard = null)`에 선택 인자를 추가한다. 기존 호출과 중첩 탄약 효과의 원본 컨텍스트를 보존하고 `finally`에서 복원한다.
- 지속 카드 사용 시에는 `source.Card`, 일반 사용은 `cardData`, 반복 효과는 `ActiveContinuousCard.Card`를 전달한다. 예약 발동은 `sourceCard: pending.Card`로 일반 일회 실행한다.

- [x] **Step 1:** `DelayedEffectTests`에 반사 기반 최소 진입 테스트를 먼저 작성하여 새 타입이 없다는 실패를 확인한다. 새 타입 도입 후 직접 호출 테스트로 확장한다.
- [x] **Step 2:** `OneTurnDelayFiresAtNextStartOnlyOnce`, `ThreeTurnDelayAndDuplicateUsesRemainIndependent`, `SameTurnReservationsRunInRegistrationOrder`를 작성한다. N=1·3, 같은 에셋 두 장, 카드 사용 직후 및 만기 이후의 HP와 예약 개수를 검사한다.
- [x] **Step 3:** `RecurringAndNestedDelaysCannotFireInTheirRegistrationTurn`, `ConditionalAmmoDelayKeepsOriginAndPaysOnlyAtTheAppropriateStage`, `InvalidAndEmptyDelaysDoNotSchedule`를 작성한다. N=0·음수·null/빈 배열도 검사한다.
- [x] **Step 4:** 승인된 API와 발동 예정 턴 방식으로 예약을 구현한다. `AdvanceTurn()`은 턴 시작에서 한 번만 호출한다. 만기 목록을 스냅샷으로 고정하고 각 예약을 실행 직전 제거한다. 실행 전후 `CanContinueEffects`를 확인한다.
- [x] **Step 5:** 시작 시 초기화, 코스트·방어도 초기화 → 기존 지속 효과 → 지연 효과 → 드로우 순서를 연결한다. 실패 테스트를 통과시키고 기존 설명·탄약·멀리건 테스트를 실행한다.

## Task 2: 지속 카드의 지연 종료와 카드 위치

**Files:**
- Modify: `Assets/Scripts/Card/ActiveContinuousCard.cs`, `Assets/Scripts/Card/ContinuousCardSystem.cs`, `Assets/Scripts/Card/DelayedEffectSystem.cs`, `Assets/Scripts/Card/CardData.cs`, `Assets/Scripts/Card/CardEffects/CardEffect.cs`, `Assets/Scripts/Card/CardEffects/Soldier/SpendAmmoBonusEffect.cs`, `Assets/Scripts/Manager/BattleManager.cs`.
- Test: `Assets/Tests/Editor/DelayedContinuousCardTests.cs` 및 `.meta`.

**Interfaces:**
- `ActiveContinuousCard`: `bool HasPendingDelays`, `bool UsesDelayedLifetime`, `bool HasMaintainedEffects`, `bool ExhaustsOnUse`; `RegisterDelay()`와 `CompleteDelay()`로 실제 등록된 사용 시 예약 수를 센다. 반복 목록에서 만든 예약은 수명과 연결하지 않는다. 사용 건의 `ExhaustsOnUse`는 `Card.exhaustOnUse && (UsesDelayedLifetime || Card.ExhaustsOnUse)`이며 `IsAbsorbed`는 그 반대다. 등록 여부가 확정된 뒤 소멸 위치를 정산한다.
- `ContinuousCardSystem`: `void CompleteDelayedCard(ActiveContinuousCard entry)`가 마지막 예약의 발동 직전에 사용 건을 해제하고 비소멸 카드 한 장만 원래 덱의 버림 더미로 보낸다. `ResetForBattle()`은 반환하지 않는다.
- `CardEffect`에 `bool ContainsDelay(HashSet<CardEffect> visiting)`를 제공한다. DelayEffect는 유효한 자신을, SpendAmmoBonusEffect는 하위 배열을 검사한다. 재귀 방문 집합으로 순환을 끊는다.
- `CardData.ExhaustsOnUse`의 기존 일반 카드 규칙은 유지한다. 지연 지속 카드의 실제 소멸은 활성 사용 건의 `ExhaustsOnUse`로 판정하며 BattleManager의 사용 애니메이션과 중단 정산에도 같은 판정을 전달한다. 실제 등록된 예약이 없는 조건부 경로는 `UsesDelayedLifetime`을 켜지 않아 기존 영구 흡수를 유지한다.

- [x] **Step 1:** `DelayedContinuousReturnsToDiscardOnLastDueEffect`에서 꿈·악몽, FixedTurns·UntilBattleEnd, 서로 다른 1·3턴 예약을 조합한다. 첫 예약 후 활성 유지, 마지막 예약 직전 유지 효과 해제·버림 한 장, 뽑을 더미 반환 없음, 하위 상태의 독립 지속 시간을 검사한다.
- [x] **Step 2:** `ExhaustedDelayedContinuousNeverReturnsOrExhaustsTwice`, `OrdinaryPermanentContinuousKeepsAbsorption`, `FailedConditionalDelayKeepsOrdinaryLifetime`, `RecurringDelayDoesNotExtendItsOwner`를 작성한다. 공격·스킬의 버림/소멸과 소유 덱 유지도 검사한다.
- [x] **Step 3:** 등록 성공 시에만 지연 수명을 시작하고 일반 만료 감소를 건너뛴다. 마지막 만기 효과를 실행하기 전에 원본 지속 사용 건을 정리하고 이후 효과는 일반 일회 실행한다. 카드 위치 정산은 활성 사용 건별 한 번으로 제한한다.
- [x] **Step 4:** 새 테스트와 기존 ContinuousCardTests·ContinuousTurnEffectTests·CardExhaustTests를 통과시킨다.

## Task 3: 예약 상태창과 효과 설명

**Files:**
- Create: `Assets/Scripts/Card/DelayedEffectDescriptionBuilder.cs` 및 `.meta`.
- Modify: `Assets/Scripts/UI/CharacterInfoUI.cs`, `Assets/Scripts/UI/StatusEffectsUI.cs`, `Assets/Scripts/UI/StatusIconUI.cs`, `Assets/Scripts/UI/StatusTooltipUI.cs`, `Assets/Scripts/BattleCharacter/BattleCharacter.cs`, `Assets/Scripts/Manager/BattleManager.cs`.
- Modify: `Assets/Scripts/Card/CardDescriptionBuilder.cs`, `Assets/Scripts/Card/CardPreviewContext.cs`, `Assets/Scripts/Card/CardEffects/CardEffect.cs`, `Assets/Scripts/Card/CardEffects/DelayEffect.cs`, `Assets/Scripts/Card/CardEffects/Soldier/SpendAmmoBonusEffect.cs`와 기본 효과의 설명 API 구현.
- Test: `Assets/Tests/Editor/DelayedEffectHudTests.cs`, `Assets/Tests/Editor/DelayedEffectDescriptionTests.cs` 및 `.meta`.

**Interfaces:**
- `StatusEffectsUI.Bind(StatusEffectSystem status, ContinuousCardSystem continuousCards = null, DelayedEffectSystem delayedEffects = null)`와 `CharacterInfoUI.BindStatusEffects`에 호환 가능한 선택 인자를 추가한다. 전투 시작 시 플레이어 UI에 전투 예약 시스템을 바인딩한다.
- `StatusIconUI.SetDelayedEffect(PendingDelayedEffect effect)`와 `long DelayedEffectId`; `StatusTooltipUI.ShowDelayed(...)`, `RefreshDelayed(...)`를 기존 지속 카드 경로와 같은 방식으로 제공한다.
- `CardEffect.CollectReferencedStatuses`는 DelayEffect 하위 상태도 수집한다. `DelayEffect.Preview`는 독립적인 미리보기 분기만 계산하여 뒤의 즉시 효과를 오염시키지 않는다.
- `DelayedEffectDescriptionBuilder.Build(DelayEffect effect)`는 전용 설명 토큰을 하위 효과 기준으로 해석한다. `CardDescriptionBuilder.BuildEffects(string template, CardEffect[] effects)`를 기본 수치 표시용으로 제공한다.
- 비어 있는 설명의 기본 효과 요약은 `CardEffect.GetEffectSummary(HashSet<CardEffect> visiting)` 가상 API로 제공한다. 피해·방어도·회복·상태·코스트·장전·탄창·탄약 조건·멀리건·지연은 자신의 정의를 읽어 한국어 요약을 반환하고 중첩은 방문 집합을 공유한다. 알 수 없는 사용자 효과는 에셋 이름을 표시한다. 실행 메서드나 실전 상태 변경 API는 호출하지 않는다.

- [x] **Step 1:** 실제 CharacterInfoUI 프리팹에서 `DuplicateReservationsHaveSeparateCountdownIcons`, `OpenTooltipUpdatesAndClosesOnResolution`, `DisabledAndReboundHudDoesNotKeepOldReservations`를 작성한다. 아이콘 N→N-1, 카드 이름, ‘N턴 뒤 발동’, 효과 설명과 아이콘 대체 이미지를 검사한다.
- [x] **Step 2:** `CustomDelayedDescriptionUsesChildTokensAndBaseValues`, `EmptyDescriptionSummarizesBuiltInEffects`, `DelayedPreviewCannotChangeImmediateStatusOrAmmo`, `CyclicMetadataDoesNotRecurseForever`를 작성한다. 설명 전후 HP·탄약·상태·예약 수가 동일한지 검사한다.
- [x] **Step 3:** 기존 프리팹 참조를 재사용하여 예약 딕셔너리·변경 구독·아이콘 제거를 구현한다. 재바인딩 시 전투 로컬 ID가 겹쳐도 과거 아이콘이 남지 않게 한다. 유지 효과가 없는 지연 지속 카드에는 불필요한 기존 지속 아이콘을 중복 생성하지 않는다.
- [x] **Step 4:** 기존 호버·상세창의 상태 수집과 전용 설명 미리보기를 연결한다. HUD·설명·기존 툴팁 테스트를 통과시킨다.

## Task 4: 전투 종료와 실제 씬 통합 검증

**Files:**
- Modify: `Assets/Scripts/BattleCharacter/BattleCharacter.cs`, `Assets/Scripts/Manager/BattleManager.cs`, `Assets/Scripts/Card/DelayedEffectSystem.cs`.
- Test: `Assets/Tests/Editor/DelayedEffectLifecycleTests.cs` 및 `.meta`.
- Create: `docs/DelayedEffects.md`.

**Interfaces:**
- `BattleCharacter`에 `event Action<BattleCharacter> Died`를 추가하여 HP가 양수에서 0이 될 때 한 번 알린다. 기존 Player/Enemy.Die 로그는 보존한다.
- `BattleManager.EndBattle()`는 중복 호출에 안전하게 BattleEnd로 전환하고 예약·지속 사용 건·선택 대기를 정리한다. 사망 이벤트는 전투 시작에 구독하고 비활성화 시 해제한다.
- 종료 시 현재 실행 중인 IEnumerator를 안쪽에서 강제 Dispose하지 않는다. 실행 경계의 `CanContinueEffects`와 취소 신호로 안전하게 빠져나가며, 비활성화 시에는 기존 EffectSequence.Stop 경로를 사용한다. 적 행동 후 종료 상태를 검사하여 다음 플레이어 턴을 시작하지 않는다.

- [x] **Step 1:** `EndingBattleCancelsAllReservationsWithoutReturningCards`, `DeathDuringDelayedEffectsSkipsRemainingEffectsAndReservations`, `EnemyLethalAttackCannotRestartPlayerTurn`, `DisablingOrReinitializingBattleClearsPendingState`를 작성한다.
- [x] **Step 2:** 실제 BattleScene을 사용하는 UnityTest에서 카드 사용 직후 버림/소멸·손패 보충, 이후 턴의 예약 발동과 HUD 제거, 지연 멀리건의 입력 대기·후속 효과 순서를 검증한다. 멀리건 대기 중 EndBattle/비활성화를 수행하여 떠 있는 UI와 잠금이 없는지 검사한다.
- [x] **Step 3:** 종료 연결과 가드를 구현하고 수명주기 테스트를 통과시킨다. 승패 UI·보상·씬 전환은 추가하지 않는다.
- [x] **Step 4:** Unity CLI 상태 및 실제 명령 도움말을 확인하고 Unity 6000.0.77f1에 연결한다. CLI가 없으면 설치·패키지 변경 없이 기존 Editor/Test Framework 실행 경로의 가용성을 확인한다.
- [x] **Step 5:** `unity test --help`로 확인한 프로젝트/결과 파일 인자와 `--filter`를 사용하여 새 Delayed 계열 테스트를 실행한다. 결과 XML에서 실패 0개와 실행 수를 확인한 다음 기존 전체 EditMode 회귀 테스트를 한 번 실행한다. Unity 컴파일 오류를 별도로 확인한다. 실행 불가 항목은 원인과 함께 미검증으로 기록한다.
- [x] **Step 6:** `docs/DelayedEffects.md`에 Inspector 설정, N턴 의미, 지속/버림/소멸, 설명 토큰·기본 요약, 전투 종료 취소 규칙을 기록한다. 새 .meta 중복 GUID, 기존 씬·프리팹·카드 에셋 보존, 인코딩과 변경 범위를 확인한다.
- [x] **Step 7:** 최종 변경을 별도 리뷰하고 발견한 문제를 수정한 뒤 해당 검증만 다시 실행한다. 실제 실행한 테스트와 정적 검증을 구분하여 보고한다.

## Execution Handoff

계획 자체 검토 완료: 설계서의 턴 기준, 중복 예약, 지속 카드 반환, 소멸, 상태창, 설명 격리와 종료 정리를 각 작업과 검증에 연결했다. 사용자가 계획과 Native 실행을 승인하여 구현을 진행했다.

사용자가 승인한 Native 방식으로 현재 체크아웃에서 구현한다. 테스트는 DelayedEffectTests, DelayedEffectIntegrationTests, DelayedEffectSceneTests에 통합하여 중복 설정을 줄였다. 중간 결과와 판정은 .utmp/delay-progress.md에 기록한다.

## 최종 검증 결과

Unity 6000.0.77f1에서 전체 EditMode 테스트 258개 통과, 실패 0개, 생략 0개. 실제 BattleScene에서 PlayMode로 전환하는 통합 테스트를 포함한다. 결과: `.utmp/delay-full-suite-final.xml`. 코드 검토에서 확인한 종료 경계·재활성화·순환 참조·상태창 정리·초기화 순서 문제를 재현 테스트와 함께 수정했다.
