# 적 디버프와 복합 행동

정리 기준일: 2026-10-20.

`EnemyApplyStatusAction`은 상태이상을 부여하고, `EnemyCompositeAction`은 여러 `EnemyAction`을 등록 순서대로 한 턴에 실행합니다. 기존 공격·방어 행동을 그대로 재사용합니다. 행동 선택 방식은 [적 행동 패턴](EnemyBehaviors.md), 프리팹 연결은 [적 프리팹](EnemyPrefabs.md)을 참고합니다.

## 준비된 에셋

| 경로 | 내용 |
|---|---|
| `Assets/Enemy/Examples/Debuff.asset` | 플레이어에게 약점 노출 2턴 부여 |
| `Assets/Enemy/Examples/DebuffAttack.asset` | 약점 노출 부여 → 기본 공격 10 |
| `Assets/Enemy/Examples/BlockAttack.asset` | 방어도 8 획득 → 기본 공격 10 |
| `Assets/Enemy/Examples/AllActions.asset` | 방어 → 중첩 복합 행동(약점 노출 → 공격) |
| `Assets/Enemy/Examples/ExpandedBehavior.asset` | 위의 디버프, 디버프+공격, 방어+공격을 순서대로 사용하는 예제 패턴 |
| `Assets/Enemy/DefaultEnemyActionIcons.asset` | 공격·방어·디버프의 7가지 조합별 Sprite |
| `Assets/Images/UI/EnemyActions/` | 검·방패·하향 화살표로 구분하는 기본 아이콘 |

예제는 기본 전투에 자동으로 넣지 않습니다. `NormalEncounter`의 HP·보상·행동 참조와 `Behavior_Test`의 공격 10 → 방어 8 순서는 유지합니다. 기존 공격·방어 에셋에는 공통 아이콘 세트 참조만 추가했습니다.

## 디버프 행동 만들기

1. `Create → Enemy → Action → Apply Status`로 에셋을 만듭니다.
2. `Status`에 `Assets/StatusData/`의 상태 정의를 연결합니다.
3. `Turns`에 지속 턴, `Target`에 `Player` 또는 `Self`를 지정합니다. 기본값은 플레이어·2턴입니다.
4. `Icon Set`에 `DefaultEnemyActionIcons`를 연결하거나 `Action Icon`에 개별 아이콘을 지정합니다.
5. 해당 적이 사용할 `EnemyBehavior`의 `Actions`에 등록합니다.

기존 상태이상 적용 규칙을 사용합니다. 양쪽 캐릭터 모두 플레이어 턴 시작에 남은 턴이 1 줄어들므로, 적 턴에 부여한 **1턴 상태는 바로 다음 플레이어 턴 시작에 만료**됩니다. 2턴이면 다음 플레이어 턴에 1턴으로 남습니다. 이를 보정하려고 내부에서 지속 턴을 자동으로 더하지 않습니다. 같은 상태를 재부여하면 기간이 더해지고 배율은 중복되지 않습니다.

## 복합 행동 만들기

1. `Create → Enemy → Action → Composite`로 에셋을 만듭니다.
2. `Actions` 배열에 원하는 순서로 기존 공격·방어·디버프 행동을 연결합니다.
3. `Icon Set`에 `DefaultEnemyActionIcons`를 연결합니다.
4. 적의 `EnemyBehavior.Actions`에는 이 **복합 행동 에셋**을 하나의 항목으로 등록합니다.
5. 맵 전투는 `BattleEncounterDefinition.behavior`, 단독 전투는 Enemy의 Behavior를 통해 기존 방식으로 연결합니다.

`[디버프, 공격]`은 디버프를 먼저 부여하므로 이번 공격부터 피해가 달라질 수 있습니다. 예제의 기본 공격 10은 약점 노출 적용 후 15가 됩니다. `[공격, 디버프]`로 순서를 바꾸면 현재 공격에는 새 디버프가 반영되지 않습니다. 방어와 공격도 배열 순서대로 실행합니다.

중첩 복합 행동과 같은 하위 행동의 반복 사용을 허용하고, 빈 항목은 건너뜁니다. 유효 행동이 하나도 없거나 자기 참조·순환 참조가 있으면 설정 오류입니다. 전투 설정 검증과 복합 행동 실행 전에 전체 참조를 검사하므로 잘못된 뒤쪽 항목 때문에 앞쪽 효과만 적용하지 않습니다. 전투가 끝나면 뒤의 행동은 실행하지 않습니다.

복합 행동은 내부 행동 수와 관계없이 적의 한 번의 행동입니다. 고정 순서·무작위 선택과 3연속 금지 이력은 최상위 행동 에셋을 기준으로 유지합니다. 하위 행동을 Behavior에도 따로 등록하면 그 행동은 별도의 턴 후보가 됩니다.

## 조합별 아이콘

각 행동은 `EnemyActionKinds`로 공격(1), 방어(2), 디버프(4)를 제공합니다. 복합 행동은 중첩된 내부 행동까지 종류를 합칩니다. 같은 종류가 여러 번 있어도 종류 하나로 표시합니다.

| 내부 종류 | 아이콘 세트 필드 |
|---|---|
| 공격 | Attack |
| 방어 | Block |
| 디버프 | Debuff |
| 공격 + 방어 | Attack Block |
| 공격 + 디버프 | Attack Debuff |
| 방어 + 디버프 | Block Debuff |
| 공격 + 방어 + 디버프 | Attack Block Debuff |

복합 행동은 조합 아이콘을 우선 사용하고, 조합 Sprite 또는 세트가 없으면 자신의 `Action Icon`을 사용합니다. 단일 행동은 지정한 개별 아이콘을 우선하고, 없으면 세트의 해당 종류 아이콘을 사용합니다. 사용자 그림은 공통 아이콘 세트의 Sprite만 교체하면 됩니다. 공격·방어·디버프의 순서는 아이콘 종류를 바꾸지 않습니다.

행동 예고 패널은 비율을 유지한 아이콘 하나와 TMP 설명을 한 묶음으로 가운데에 배치합니다. 설명은 너비에 맞춰 줄바꿈하며, 패널 높이는 설명의 높이와 위아래 여백에 맞춰 커집니다. 기준 최소 높이는 88입니다. 예고할 행동이 없으면 아이콘·설명·패널을 숨깁니다.

기본 공격·방어·디버프 그림은 `GeneratedArt/EnemyActionIcons/*-v1-256x256.png`를 사용합니다. 복합 4종은 같은 그림을 참고해 중앙에서 겹치는 하나의 표식으로 제작했으며, 원본과 생성 프롬프트는 `GeneratedArt/EnemyActionIcons/Composites`에 있습니다. 방패를 뒤에, 검과 디버프 표식을 앞에 배치합니다. 게임의 7개 PNG는 기존 경로와 GUID를 유지하고, 투명 배경·Single Sprite·최대 256 해상도·밉맵 없음으로 임포트합니다.

## 예고와 실행의 일치

`EnemyActionPreviewContext`는 양쪽 상태이상과 플레이어 HP·방어도를 복사합니다. 복합 행동 설명은 같은 복사본에 내부 행동을 순서대로 반영하므로, 앞에서 부여한 약점 노출·녹슬음·방어 파괴를 뒤의 피해·방어도 예고에 반영합니다. 치명 피해 뒤의 행동은 예고에서도 제외합니다.

HP·방어도 변경은 `BattleCharacter.VitalsChanged`로 통지하며, 적은 양쪽 캐릭터의 상태이상과 HP·방어도 변화를 구독합니다. 방어 카드 사용·피격·회복·턴 시작 방어도 초기화 시에도 현재 예고만 갱신하고 행동은 다시 추첨하지 않습니다.

미리보기는 실제 `Execute()`, 상태 부여, 피해 처리, 난수 추첨을 호출하지 않습니다. 공유 행동 에셋에는 현재 전투의 상태를 저장하지 않습니다. 새 종류의 행동을 추가할 때는 `GetActionKinds()`와, 뒤 행동의 수치에 영향을 주는 경우 `Preview(context)`도 함께 구현해야 합니다.

## 에디터 설정 도구와 검증

2026-11-10 Unity 6000.0.85f2에서 예고 패널과 관련 행동 검사가 33/33 통과했습니다. 결과는 로컬 `.utmp/enemy-intent-panel-20261110/verified.xml`에 있습니다.

`ProjectDN → Setup Enemy Actions`는 누락된 기본 아이콘·예제·아이콘 세트 연결을 생성하고 BattleScene의 예고 영역을 설정합니다. 기존 예제 행동 내용과 이미 지정한 아이콘은 덮어쓰지 않습니다. 메뉴를 실행하면 예고 영역 배치는 위의 기본 구성으로 다시 설정됩니다.

`ProjectDN → Import Generated Enemy Action Icons`는 생성 원본으로 기본 7개 PNG와 임포트 설정만 갱신하며, 씬 배치와 적 행동·밸런스 데이터는 변경하지 않습니다. 모든 원본이 있는지 먼저 확인한 뒤 교체합니다. 기본 설정 메뉴도 이미지가 없을 때 이 원본을 복원하며 이전의 원형 배지 그림은 생성하지 않습니다.

`EnemyActionExpansionTests`는 대상·기간·재부여, 효과 순서와 상태 반영, 미리보기의 무변경, 중첩·중복·순환 참조, 전투 종료 중단, 7가지 아이콘 조합·대체 아이콘, 직렬화 예제와 기존 패턴, 실제 BattleScene 예고·피해·여러 줄 배치를 검사합니다. 그래픽 실행 시 `.utmp/enemy-actions/{Debuff,DebuffAttack,BlockAttack,AllActions}.png`에 전투 화면을 기록합니다.

적 행동 확장 당시 검증 기록: Unity 6000.0.77f1 전체 EditMode 408/408 통과, 실패·건너뛰기 0개. 새 행동 테스트 16/16은 그래픽을 켠 실행에서도 통과했으며 실제 Play Mode 진입·예고와 피해·HP와 방어도 갱신·1280×720 화면 배치를 포함합니다. 당시 전체 결과는 `.utmp/enemy-actions/full.xml`, 그래픽 결과는 `.utmp/enemy-actions/verified.xml`입니다. 실행 파일 빌드는 수행하지 않았습니다.

2026-10-21 아이콘 교체 검증: Unity 6000.0.77f1에서 `GeneratedEnemyIconTests` 7개와 기존 `EnemyActionExpansionTests` 16개, 총 23/23 통과했습니다. 7개 원본 일치·GUID 유지·256×256 Sprite 임포트·투명 배경을 검사했으며, 실제 전투의 공격+방어 및 3종 복합 표식을 렌더링해 확인했습니다. 결과는 `.utmp/enemy-icon-replacement/verified.xml`입니다. 씬·행동·밸런스 데이터의 파일 내용은 교체 전과 동일합니다.
