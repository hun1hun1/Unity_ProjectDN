# Soldier Ammo Implementation Plan

**Goal:** Soldier와 장전, 조건부 소모, 필수 보유/소모 카드를 기존 카드 구조에 구현합니다.

**Architecture:** Player를 상속하는 Soldier와 독립 AmmoSystem을 추가합니다. CardData에 사용 전 총알 조건을 두고 CardEffect 하위 타입으로 장전 및 추가 효과 묶음을 구현합니다.

**Tech Stack:** Unity 6000.0.77f1, C#, Unity Test Framework 1.6.0.

**Spec:** ../specs/2026-10-12-soldier-ammo-design.md

## Constraints

- 최대 10알 / 시작 0알, Inspector 설정 가능.
- 기본 BattleScene과 시작 덱 유지, 별도 Soldier 프리팹 추가.
- 기존 C# 인코딩과 에셋 GUID 유지. 패키지 추가나 게임 밸런스 변경 없음.
- 직접 구현합니다. 변경은 커밋하지 않고 검토 가능한 작업 트리에 남깁니다.

## Tasks

- [x] 먼저 기존 UseCard에 총알 부족 카드를 전달하여 코스트/효과가 실행되는 회귀 테스트를 작성하고 실패를 확인합니다.
- [x] AmmoSystem의 Load, HasAmmo, TrySpend와 Soldier를 구현하고 장전 상한, 부족 시 미소모, 초기화/음수 설정을 검증합니다.
- [x] ReloadAmmoEffect와 SpendAmmoBonusEffect를 추가하고 기존 효과를 실제로 실행하여 부족/충족 및 한 번의 묶음 소모를 검증합니다.
- [x] CardData의 requiredAmmo / consumeRequiredAmmo, CostSystem.CanPay, BattleManager.CanUseCard 및 사용 자원 지불을 연결합니다. 부족한 총알/코스트에서 모든 상태 유지, 보유 전용/소모 카드, 기존 카드 호환성을 검증합니다.
- [x] 재사용 입력과 턴 종료 중복을 방지합니다. 효과 배열의 null은 기존의 빈 효과와 동일하게 취급합니다.
- [x] Unity Editor API로 별도 Soldier 프리팹과 설정 예시 효과/조건 카드를 생성합니다. 기본 씬은 저장하지 않습니다. 설정 안내를 작성합니다.
- [x] Unity 전체 EditMode 테스트와 PlayMode 동작/직렬화 참조를 검증하고 별도 리뷰로 변경을 확인합니다.

## Review focus

- 코스트 부족에서 총알이 먼저 소모되지 않는지.
- 추가 효과 여러 개에 소모가 한 번만 적용되는지.
- 필요량 정확히 O인 경우와 O-1인 경우가 구분되는지.
- 기본 Player / 기존 CardData 에셋이 계속 동작하는지.
- 프리팹 UI, CostSystem 및 에셋 효과 참조가 실제로 연결되는지.

## Verification result

- `Logs/soldier-ammo-red.xml`: 기존 코드의 총알 조건 무시를 실제 사용 경로에서 확인했습니다 (Expected False / actual True).
- `Logs/soldier-ammo-inspector-red.xml`: 실행 중 Inspector 설정 변경의 UI 갱신 누락을 확인하고 OnValidate 알림을 다음 프레임에 전달하도록 수정했습니다.
- `Logs/soldier-ammo-final.xml`: Unity 6000.0.77f1, 41/41 통과. EditMode 경계값/에셋 검증과 EnterPlayMode를 사용하는 BattleScene 카드 연출·턴 종료·버림·보충·UI 검증을 포함합니다.
- 별도 읽기 전용 리뷰에서 Critical/Important 결함이 발견되지 않았습니다. 경미한 Inspector UI 관찰은 회귀 테스트 RED→GREEN으로 수정했습니다.
- 기본 씬, 폰트, 패키지, 에디터 버전 설정의 내용 유지 및 기존 C# 인코딩/CRLF, 신규 에셋 .meta를 확인했습니다.
- 기존 CardRewardSystem의 미사용 필드 경고 4개는 이번 범위에서 변경하지 않았습니다.
