# 적 프리팹과 전투 화면

정리 기준일: 2026-10-20.

맵에서 선택한 전투 데이터의 적 프리팹을 BattleScene에 생성한다. 이미지·Animator·캐릭터 HP와 상태창은 프리팹에 포함하고, 전투 화면 공통 행동 예고는 생성한 적에 다시 연결한다.

## 현재 구성

- 기본 적 프리팹: `Assets/Prefabs/Enemy/TestEnemy.prefab`
- 전투 데이터: `Assets/MapData/NormalEncounter.asset`의 `Enemy Prefab`
- 생성 위치: BattleScene의 `EnemyArea/EnemySpawnRoot`
- 생성과 전투 대상 연결: `BattleManager.ConfigureEncounter()`
- 맵에서 선택한 전투 적용: `RunBattleBridge`

기존 테스트 적의 이미지, HP·상태 UI, 행동 설정을 프리팹으로 옮겼다. 새 그림이나 애니메이션을 제작한 것은 아니며, 적마다 다른 외형과 Animator를 가진 프리팹을 연결할 수 있는 구조이다.

## 다른 적 추가

1. `TestEnemy.prefab`을 복제하거나 Prefab Variant를 만든다.
2. 프리팹의 Image에 적 이미지를 지정한다. 애니메이션이 있으면 해당 적의 Animator와 Animator Controller를 프리팹에 설정한다. Animator의 기본 상태는 인스턴스가 활성화된 뒤 재생된다.
3. 프리팹 루트에는 `RectTransform`과 `Enemy`가 있어야 한다. 현재 화면은 Canvas 기반이므로 외형에는 uGUI Image를 사용한다. 기본 프리팹은 생성 영역을 채우는 루트와 비율 유지 이미지로 구성되어 있다.
4. HP·방어도·상태 UI는 프리팹 내부 참조로 유지한다. 프리팹의 `Action UI`에는 씬 오브젝트를 연결하지 않는다. 전투 화면의 공통 행동 예고는 생성 시 연결된다.
5. `Create → Map → Battle Encounter`로 전투 데이터를 만들고 `Enemy Prefab`, `Behavior`, `Enemy Max HP`, `Gold Reward`를 지정한다.
6. 사용할 층의 `FloorMapDefinition` 전투 풀에 전투 데이터를 등록한다. 노드에 선택된 전투 데이터와 프리팹은 전투 화면에서도 그대로 사용한다.

전투 데이터의 HP와 행동 설정이 해당 전투의 기준이다. 프리팹의 기본 HP나 행동이 다르더라도 선택된 전투 데이터를 먼저 적용하고 활성화한다. 이미지·Animator와 내부 오브젝트 구조는 프리팹의 구성을 그대로 사용한다. 기본 애니메이션은 활성화 후 재생되며, 공격·피격 등 행동별 트리거 호출은 별도 연결이 필요하다.

루트는 공통 생성 영역 안에서 배치하고, 외형의 별도 이동·확대가 필요하면 시각 요소를 자식으로 구성하여 그 자식에 애니메이션을 적용할 수 있다. HP UI까지 함께 움직이게 할지는 프리팹 구조에서 결정한다.

## 생성과 교체

`RunBattleBridge`가 BattleManager의 전투 초기화 전에 선택한 전투를 전달한다. 새 적을 비활성 상태에서 생성하여 전투 데이터를 적용하고, 이전 적은 즉시 비활성화한 뒤 제거한다. BattleManager의 대상과 사망 이벤트, 상태·행동 예고 연결을 새 적으로 바꾼 후 활성화한다.

맵이 적을 선택한 뒤 BattleScene에서 다시 적을 추첨하지 않는다. 생성한 인스턴스에 전투 상태를 보관하며 프리팹 원본이나 공유 전투 데이터를 수정하지 않는다.

기존 전투 데이터에서 `Enemy Prefab`이 비어 있으면 씬에 배치한 적에 HP·행동만 적용하는 기존 방식도 유지한다. BattleScene 단독 실행 시에는 기본 적 프리팹 인스턴스를 사용한다.

## 에디터 설정 도구

`ProjectDN → Setup Enemy Prefabs`는 누락된 기본 프리팹과 생성 위치를 만들고 기존 BattleScene과 일반 전투 데이터를 연결한다. 기존 프리팹이 있으면 덮어쓰지 않으며, 이미 지정한 일반 전투의 프리팹도 유지한다. 씬·프리팹은 Unity Editor API로 작성한다.

행동 선택 방식은 [적 행동 패턴](EnemyBehaviors.md), 맵과 전투 데이터 연결은 [맵 시스템](MapSystem.md)을 참고한다.

## 검증 범위

`EnemyPrefabTests`에서 직렬화 참조, 선택한 프리팹과 HP 적용, 사망 이벤트 교체, 생성 위치 누락, 프리팹 없는 기존 데이터, 실제 맵·전투·보상 왕복, Animator 재생과 반복 생성 시 이전 적 제거, 활성화 전 전투 설정 적용을 검사한다. 그래픽을 사용하는 테스트에서는 `.utmp/enemy-prefabs/battle.png`에 실제 전투 화면도 저장한다.

Unity 6000.0.77f1에서 전체 EditMode 테스트 392/392 통과, 실패·건너뛰기 0개를 확인하였다. 이 중 프리팹 검사는 8개이며, 기존 적 행동 패턴 11개와 함께 그래픽을 켠 실행에서도 통과하였다. 실제 Play Mode 진입과 Animator 재생·보상 후 맵 복귀를 검사했으며, 1280×720 전투 화면에서 적 이미지·HP·행동 예고를 확인하였다. 전체 결과는 `.utmp/enemy-prefabs/full.xml`, 그래픽 실행 결과는 `.utmp/enemy-prefabs/verified.xml`에 기록하였다. 실행 파일 빌드는 수행하지 않았다.
