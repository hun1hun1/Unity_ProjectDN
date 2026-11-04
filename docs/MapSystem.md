# 맵과 런 진행

정리 기준일: 2026-11-03. 현재 구현과 향후 콘텐츠 노드의 설계를 구분합니다. 강화 세부 설계는 [카드 강화 구현 설계](CardUpgrades.md)를 참고합니다.

## 구현 상태와 역할

맵 생성, 노드 선택, 전투 씬 왕복, 보상 완료 후 진행은 구현되어 있습니다. 상점·강화·이벤트·유물 선택 화면과 그 처리기는 아직 없습니다. 최대 탄약 증가 유물의 보유·적용 기능은 있으나, 맵에서 유물을 지급하는 기능과는 별개입니다.

| 구성 | 역할 | 소스 |
|---|---|---|
| `RunDefinition` | 씬 이름, 시작 HP·골드, 층 목록, 허용 콘텐츠 | [RunDefinition.cs](../Assets/Scripts/Map/RunDefinition.cs) |
| `FloorMapDefinition` | 노드 수, 생성 모드, 확률·가중치, 전투 풀과 콘텐츠 데이터 | [FloorMapDefinition.cs](../Assets/Scripts/Map/FloorMapDefinition.cs) |
| `MapGenerator` | 설정과 시드로 한 층의 노드·선택지 생성 | [MapGenerator.cs](../Assets/Scripts/Map/MapGenerator.cs) |
| `MapNode`, `MapChoice` | 노드 규칙·진행 상태와 선택지의 종류·데이터 | [MapNode.cs](../Assets/Scripts/Map/MapNode.cs) |
| `RunProgress` | 현재 층·노드·HP와 진행 단계, 완료 조건 관리 | [RunProgress.cs](../Assets/Scripts/Map/RunProgress.cs) |
| `RunSession` | 씬을 넘어 진행 상태 유지, 전투 이동, 완료 통지 | [RunSession.cs](../Assets/Scripts/Map/RunSession.cs) |
| `MapController` | 맵 UI 갱신, 선택 잠금, 콘텐츠 처리기 호출 | [MapController.cs](../Assets/Scripts/Map/MapController.cs) |
| `MapContentHandler` | 비전투 콘텐츠가 연결될 공통 진입점 | [MapContentHandler.cs](../Assets/Scripts/Map/MapContentHandler.cs) |
| `RunBattleBridge` | 전투 시작 시 런 정보 적용, 결과·보상 완료 전달 | [RunBattleBridge.cs](../Assets/Scripts/Map/RunBattleBridge.cs) |

설정 ScriptableObject는 공유 정의이고, `RunProgress`의 노드 상태와 각 인벤토리가 현재 런의 실행 상태입니다. 상품 구매 여부나 이벤트 선택 결과를 공유 설정 에셋에 기록하지 않습니다.

## 현재 실행 구성

- 빌드 시작 씬은 `TitleScene`입니다. 새 게임을 시작하면 `MapScene`으로 이동하며, `MapScene`을 직접 실행하면 새 런을 구성합니다.
- 런 설정: `Assets/MapData/TemporaryRun.asset`.
- 층 설정: `Assets/MapData/TemporaryFloor.asset`.
- 전투 설정: `Assets/MapData/NormalEncounter.asset`.
- 일반 전투만 5개 생성합니다. 각 전투의 승리 보상을 완료하거나 남은 보상을 포기하면 다음 노드로 이동합니다.
- 다섯 번째 전투의 보상 처리까지 끝나면 맵에서 `런 완료`를 표시하고 선택지를 숨깁니다. 앱이나 Unity Play Mode를 강제로 종료하지 않습니다.
- 패배 또는 전투 중단 시 진행을 멈추고 맵에 런 종료 상태를 표시합니다.
- 현재 임시 런은 최대 HP 80, 보유 골드 100으로 시작합니다. 1층 전투의 골드 보상은 노드 시드로 정한 20~25에 현재 노드 번호(1부터 시작)를 더합니다. 첫 노드 보상은 21~26골드입니다. [전투 보상](BattleRewards.md) 참고.

## 규칙 조정

`FloorMapDefinition.mode`는 두 모드를 제공합니다.

| 모드 | 규칙 |
|---|---|
| NormalBattlesOnly | `nodeCount`만큼 일반 전투만 생성합니다. 특수 노드·유물·보스를 배치하지 않습니다. 현재 임시 설정입니다. |
| PlannedFloor | 첫 일반 전투, 마지막 보스, 중간 특수 전투와 바로 다음 유물을 예약한 후 나머지 일반 노드를 생성합니다. 최소 4개 노드가 필요합니다. |

`PlannedFloor`의 일반 노드는 항상 일반 전투를 제공합니다. `nonBattleChance`로 추가 비전투 선택지의 등장 여부를 결정하고, `nonBattleContents`의 가중치로 상점·유물·이벤트·강화 중 하나를 고릅니다. `eliteChance`는 엘리트 선택지를 독립적으로 추가합니다. 선택지는 최대 3개입니다.

중간 특수 노드는 `midpointMinRatio`와 `midpointMaxRatio`로 정한 중앙 구간에서 층마다 정확히 한 번 배치합니다. 시작·보스와 충돌하지 않도록 위치를 제한하고, 작은 층에서 비율 구간에 정수 위치가 없으면 가장 가까운 유효 위치를 사용합니다. 이 노드는 일반 전투와 엘리트 전투만 제공하며 어느 쪽을 선택해도 다음 노드는 유물 하나만 제공합니다. 일반 노드의 확률적 엘리트 선택지는 확정 유물 규칙과 관계없습니다.

`RunDefinition.floors` 배열의 순서대로 층을 진행합니다. 마지막 노드의 콘텐츠와 보상이 완료되어야 다음 층으로 이동하며, 마지막 층이면 런이 완료됩니다. 층 진입 시 한 번 생성한 결과는 씬을 왕복해도 다시 추첨하지 않습니다. 맵 생성에는 전투·카드 보상과 분리된 `System.Random` 시드를 사용합니다.

정식 층의 노드 수는 아직 결정하지 않았습니다. 클래스의 `nodeCount = 12`는 기본값이며, 현재 임시 에셋은 5로 설정되어 있습니다.

## 노드 선택부터 완료까지

```mermaid
flowchart TD
    A[Map: 노드 선택] --> B{선택지 종류}
    B -->|전투| C[BattleScene: 전투]
    C -->|승리| D[Rewards: 보상 처리]
    D -->|보상 목록 완료| E[현재 노드 완료]
    B -->|비전투| F[Content: 맵 씬의 콘텐츠 UI]
    F -->|이용 종료 후 completed 호출| E
    E --> G{남은 노드 또는 층}
    G -->|있음| A
    G -->|없음| H[Completed: 런 완료]
    C -->|패배 또는 중단| I[Defeated 또는 Cancelled: 런 종료]
```

- 선택 전 노드는 `Pending`이며, 선택하면 `Resolving`이 되어 다른 선택을 막습니다.
- 전투는 `Battle → Rewards`를 거칩니다. 카드 선택 후 확인 버튼을 누르는 것은 카드 지급 단계이고, 보상 목록에서 완료하거나 남은 보상을 포기해야 노드가 완료됩니다.
- 비전투는 `Content` 상태에서 처리합니다. 상점 구매나 강화 미리보기 하나만으로 다음 노드에 넘어가지 않습니다. 해당 콘텐츠의 이용이 끝날 때 완료합니다.
- 완료된 노드는 `Completed`가 됩니다. 마지막 노드라면 다음 층을 생성하거나 런을 종료합니다.

## 아직 없는 콘텐츠의 연결 경계

상점·이벤트·유물 지급·카드 강화의 실제 기능은 구현하지 않았으며, 현재 설정에서는 생성하지 않습니다.

콘텐츠가 준비되면 `MapContentHandler`를 상속하는 컴포넌트를 맵 씬에 배치하고 `MapController.contentHandlers`에 연결합니다. `ContentType`은 처리하는 종류이며, `Begin(choice, progress, completed)`에서 화면과 실제 결과를 처리한 후 `completed`를 한 번 호출합니다. 콜백에는 런 객체·층·노드가 묶여 있어 오래된 콜백으로 다음 노드나 새 런을 완료할 수 없습니다. `choice.ContentData`로 콘텐츠별 ScriptableObject를 전달할 수 있습니다.

생성 대상은 `RunDefinition.enabledContent`와 실제 사용 가능한 처리기의 교집합입니다. 전투 3종은 기본 처리하며 나머지는 등록된 활성 처리기가 필요합니다. 필수 유물 처리기가 없는 계획형 층은 런 시작 시 거부합니다. 일반 노드의 미지원 선택지는 생성하지 않습니다. 진행 중 처리기가 비활성화된 경우 해당 선택지는 비활성 상태로 표시됩니다.

계획형 층은 일반·엘리트·보스 전투 풀이 모두 필요합니다. 필수 전투 풀이 비었거나 HP·확률·가중치 설정이 잘못되면 다른 콘텐츠로 대체하지 않고 설정 오류로 알립니다.

## 상점·강화·유물·이벤트의 확장 설계 — 미구현

기존 방침대로 MapScene 안에 콘텐츠별 패널을 두고, 처리기가 그 패널을 열고 닫습니다. 콘텐츠마다 별도 씬을 추가하는 것은 이 설계에 포함하지 않습니다. 아래 클래스명은 추가할 구성의 예시이며 현재 존재하는 클래스가 아닙니다.

| 노드 | 추가할 구성 예시 | 처리와 완료 시점 |
|---|---|---|
| 상점 | `ShopDefinition`, `ShopHandler`, 상점 패널 | 상품 선택·구매 확인 → 골드 차감·지급 → 필요하면 추가 구매 → 나가기 때 노드 완료 |
| 강화 | `UpgradeDefinition`, `UpgradeHandler`, 강화 패널 | 런 덱 카드 선택 → 강화 전후 비교 → 확인 후 한 장 교체 → 정해진 이용 종료 시 노드 완료 |
| 유물 | `RelicRewardDefinition`, `RelicHandler`, 유물 선택 패널 | 유물 후보 표시 → 선택·확인 → 획득 처리 후 노드 완료 |
| 이벤트 | `EventDefinition`, `EventHandler`, 이벤트 패널 | 설명·선택지 표시 → 조건·비용 검증 → 결과 적용 → 이벤트 종료 때 노드 완료 |

### 공통 처리 계약

1. `ContentType`으로 자신의 종류를 알리고, `Begin(choice, progress, completed)`에서 `choice.ContentData`를 해당 정의 타입으로 검증합니다.
2. 상품·유물 후보나 이벤트 결과는 필요한 시점에 한 번만 추첨하고 실행 상태에 보관합니다. 패널 갱신이나 미리보기 때 재추첨하지 않습니다. 현재 `MapNode`에는 콘텐츠별 재고·이벤트 상태를 보관하는 필드가 없으므로 필요에 맞게 추가해야 합니다.
3. 미리보기와 선택만으로 골드·덱·HP·유물을 변경하지 않습니다. 확정 시 현재 런·층·노드, 비용, 선택 대상, 처리 여부를 다시 검증합니다.
4. 확정 처리는 한 번만 적용합니다. 복수 구매가 가능한 상점은 구매 건별로 검증하고, 품절 상품의 재지급을 막습니다.
5. 화면을 닫고 해당 장소 이용을 마쳤을 때 `completed`를 한 번 호출합니다. 입력 잠금이나 오류 메시지도 함께 정리합니다.

현재 완료 콜백의 검증은 오래된 콜백이 진행을 잘못 완료하는 것을 막습니다. **콜백을 부르기 전에 수행하는 골드·카드·유물 변경까지 취소해 주지는 않습니다.** 각 처리기가 실제 지급·차감 전에도 현재 맥락과 중복 처리를 검사해야 합니다.

### 런 데이터 접근 보완

| 데이터 | 현재 구현 | 추가할 기능 |
|---|---|---|
| 덱 | `RunDeckInventory`가 시작 DB 쌍별 런 덱을 유지하고 `DeckController`가 조회·변경 | 맵에서 현재 런 덱을 조회하고 카드 추가·제거·교체하는 API. 시작 DB 쌍 또는 활성 덱을 명확히 식별해야 함 |
| 골드 | `RunGoldInventory.Gold`, 양수만 받는 `Add()` | 잔액 검증과 차감을 함께 하는 `TrySpend()`. `Add(-가격)`은 차감되지 않음 |
| HP | `RunProgress.CurrentHP`, `MaxHP`, `CaptureHealth()` | 회복·손실의 규칙, 상태 변경 통지, HP 0이 되는 이벤트의 종료 정책 |
| 유물 | `RunRelicInventory`가 `AmmoCapacityRelic`의 보유 여부와 최대 탄약 보너스 유지 | 다양한 유물 정의와 획득·전투 시작·턴 시작 등 적용 시점의 공통 처리 |

맵 처리를 위해 BattleScene의 `DeckController`나 `BattleManager`를 새로 배치하지 않습니다. 런 데이터 기능을 통해 수정하고, 화면은 변경 통지를 받아 갱신하도록 확장합니다. 기존 전투용 `CardEffect`는 `BattleManager`에 의존하므로 이벤트의 비용·결과를 그대로 대체한다고 가정하지 않습니다.

유물의 정의·효과는 공유 데이터로 두고, 보유 여부와 획득 시 확정된 값은 런 상태로 보관합니다. 중간 특수 노드 다음의 확정 유물 노드와 일반 노드의 유물 선택지는 같은 유물 처리기를 사용할 수 있습니다.

### Inspector와 생성 규칙 연결 순서

1. 처리기와 패널을 만들고, 처리기를 `MapController.contentHandlers`에 등록합니다.
2. **처리기 오브젝트·컴포넌트는 활성 상태로 유지하고, 자식 UI 패널만 숨깁니다.** 현재 코드는 `isActiveAndEnabled`인 처리기만 사용 가능한 콘텐츠로 인식합니다.
3. 각 콘텐츠 정의를 `FloorMapDefinition.nonBattleContents[].contentData`에 연결합니다. 확정 유물용 정의는 `guaranteedRelicData`에 연결합니다.
4. `RunDefinition.enabledContent`에 종류를 추가합니다. 확률과 가중치는 각각 `nonBattleChance`, `nonBattleContents[].weight`로 조정합니다.
5. 현재 `NormalBattlesOnly`에서는 종류를 추가해도 비전투 선택지가 나오지 않습니다. `PlannedFloor` 전환에는 일반·엘리트·보스 전투 풀, 허용 종류, 활성 유물 처리기가 함께 필요합니다. 상점만 추가해서 기존 계획형 규칙이 곧바로 동작하는 것은 아닙니다.
6. 새 런에서 확인합니다. 생성 허용 종류는 런 생성 시 확정되므로 진행 중 에셋이나 처리기를 수정해도 기존 노드가 자동 재생성되지는 않습니다. 타이틀에서 런을 시작할 때도 지원 콘텐츠 종류를 전달해야 합니다.

구현 순서의 기준은 공통 런 데이터 API → 유물·강화 → 상점 → 이벤트입니다. 뒤의 콘텐츠가 앞에서 만든 카드·유물·강화 처리를 재사용하도록 구성합니다. 가격·회복량·유물 중복 허용·강화 횟수·건너뛰기 정책은 각 콘텐츠 구현 시 별도로 정하며, 여기서 밸런스 값을 확정하지 않습니다.

### 콘텐츠 추가 후 확인할 항목 — 예정

- 선택 순간에는 결과가 적용되지 않고, 확정 시 한 번만 반영되는지 확인합니다.
- 골드 부족, 품절, 이미 획득한 유물, 강화 불가 카드와 취소 경로를 확인합니다.
- 오래된 UI나 중복 콜백이 새 런·다음 노드의 데이터와 진행에 영향을 주지 않아야 합니다.
- 맵에서 반영한 덱·골드·HP·유물이 다음 전투에 이어지고, 새 런에서는 초기화되어야 합니다.
- 완료 전에는 다음 선택지가 잠기고, 완료 후 정확히 한 노드만 진행해야 합니다.
- 일반 전투 5회 임시 모드와 기존 단독 전투 실행도 유지되어야 합니다.

## 선택 카드 UI

맵의 다음 행동은 화면 중앙 `Canvas/Content/Choices`의 `Horizontal Layout Group`에 배치합니다. 카드는 360×440 크기의 사각형이며, 한 장은 중앙에, 두세 장은 중앙을 기준으로 가로로 나란히 표시합니다. 카드 전체를 클릭하면 기존 노드 선택 동작을 실행합니다.

`MapController` 오브젝트의 **Choice Presentations** 배열에서 노드 종류별 `Artwork`, `Title`, `Description`을 설정합니다. 제목과 설명을 비우면 해당 종류의 기본 문구를 표시합니다. 이미지가 없으면 아트워크 자리 표시를 사용하고, 연결된 이미지는 비율을 유지해서 표시합니다. 일반 전투에는 기존 적 테스트 이미지를 임시로 연결했습니다. 실제 신규 아트워크는 생성하지 않았습니다.

각 카드의 `MapChoiceCardUI`는 아트워크·제목·설명을 분리해 표시합니다. 카드 내부 이미지와 텍스트는 클릭을 가로채지 않으며 기존 Button의 노드 선택 연결을 유지합니다. 미지원 콘텐츠 선택지는 기존과 같이 비활성화합니다. 현재 런 설정은 일반 전투 5회로 유지합니다.

하단 진행 표시는 고정된 마스크 영역 안에서 세로로 스크롤됩니다. 노드 수가 많아져도 카드·상태 문구와 겹치지 않으며, 현재 노드가 있는 행을 자동으로 표시합니다.

`ProjectDN/Update Map Choice Cards`는 기존 맵의 버튼과 클릭 연결을 보존하면서 이 카드 배치를 적용하는 Editor 도구입니다. `Setup Map System`으로 맵을 새로 구성할 때도 같은 배치를 적용합니다.

## 맵 화면과 전투 상단 표시

- `MapScene`은 1층 배경, HP·골드 배지, 전투 선택 카드 프레임을 사용합니다. 진행 목록은 연결선과 완료·현재·대기 노드 마커를 스크롤 영역에 표시합니다. 관련 스프라이트는 `Assets/Images/UI/MapScene/`에 있습니다.
- `Canvas/MapUtilityButtons`의 왼쪽 덱 버튼은 현재 런의 꿈·악몽 보유 덱을 열고, 오른쪽 옵션 버튼은 공용 설정 창을 엽니다. 덱·옵션 창이 열려 있는 동안과 닫은 프레임에는 노드를 선택할 수 없습니다. [전체 덱 보기](DeckOverview.md), [옵션 창](SettingsWindow.md) 참고.
- `BattleScene`의 `TopArea/RunInfo`는 군인 심볼, 현재 층·노드, 보유 골드를 표시합니다. 층·노드는 맵과 같은 `1층 · 2 / 5` 형식을 사용합니다. 골드를 수령하면 상단 보유액이 갱신되고, 단독 전투에서는 층·노드에 `—`가 표시됩니다. 표시 연결은 [`BattleRunInfoUI`](../Assets/Scripts/UI/BattleRunInfoUI.cs)가 담당합니다.

## 상태와 전투 연결

- `MapGenerator`: 화면과 씬에 의존하지 않는 생성 규칙.
- `RunProgress`: 생성 결과, 현재 층·노드, 선택·완료 상태, HP, 런 결과.
- `RunSession`: 씬을 넘어 유지되는 진행 관리자. 기존 `RunDeckInventory`, `RunGoldInventory`, `RunRelicInventory`를 재사용합니다.
- `RunBattleBridge`: 모든 캐릭터의 Awake 이후, BattleManager.Start 이전에 HP와 적·보상 설정을 적용합니다. 전투 결과와 보상 완료를 현재 런에 전달합니다.
- `MapController`: 진행 상황과 선택지 표시, 콘텐츠 처리기 연결.

승리 시 `Battle → Rewards`로 전환하며 `BattleRewardSystem.Completed` 전에는 노드가 완료되지 않습니다. 선택은 즉시 잠기고, 전투 결과·보상 완료·비전투 완료에는 현재 런과 노드 검증을 적용합니다. 패배와 중단은 보상을 생성하거나 노드를 완료하지 않습니다.

HP와 최대 HP는 다음 전투에 복원합니다. 꿈·악몽의 런 덱과 골드·유물은 유지하며, 패·드로우/버림/소멸 더미·상태이상·지속 카드·지연 효과·탄약과 코스트는 기존 전투별 초기화 규칙을 유지합니다. 데이터베이스와 CardData 원본을 런 상태로 수정하지 않습니다.

활성 런 없이 BattleScene을 직접 실행하면 단독 전투를 시작합니다. 타이틀에서 이어서 하기를 선택하면 최근 체크포인트의 맵 진행 또는 전투 입장·보상 단계로 복귀합니다. [노드 체크포인트와 이어서 하기](RunCheckpoints.md) 참고.

## 타이틀·종료 시퀀스용 진입점

타이틀에서 새 런을 시작하려면 [캐릭터 선택 창](TitleMenu.md#캐릭터-선택)에서 군인을 선택하고 `모험 시작`을 확정합니다. 현재 `TemporaryRun`으로 맵에 진입하며 시작 골드는 100입니다.

- `RunSession.GetOrCreate().StartNewRun(definition, seed, supportedContent)`: 설정을 검증한 뒤 모든 런 인벤토리와 HP·진행을 새로 시작합니다. 맵 씬에서는 등록된 처리기 종류를 전달합니다. 처리기를 전달하지 않으면 전투만 지원하는 것으로 취급합니다.
- `RunSession.RunFinished`: `Completed`, `Defeated`, `Cancelled` 중 결과를 런당 한 번 알립니다. 현재는 종료 상태를 맵에 표시합니다.
- `RunSession.Progress`: 진행·HP·결과 조회.

`ProjectDN/Setup Map System`은 Unity Editor API로 기본 에셋과 씬을 생성하고 전투 연결을 구성합니다. 이미 존재하는 설정값과 맵 UI는 재생성하지 않습니다. 기존 맵에서 런 설정 참조가 누락됐을 때만 기본 설정을 복구합니다.

## 검증

맵 시스템 최초 구현 시(2026-10-18), Unity 6000.0.77f1에서 전체 EditMode 테스트 **369/369 통과**, 실패·건너뛰기 0개입니다. 컴파일 오류가 없으며, 이 중 맵 관련 테스트는 36개입니다.

생성 규칙, 확률 경계, 시드 독립성, 중복·과거 콜백, 필수 콘텐츠 누락을 검사했습니다. Play Mode에 진입하는 씬 테스트에서는 다섯 번의 실제 전투 왕복, 보상 대기와 완료, HP·덱·골드·유물 유지, 새 런 초기화, 패배·중단, 단독 전투 실행 및 직렬화 참조를 확인했습니다. 시작·완료 화면을 1280×720으로 렌더링해 텍스트와 버튼 배치도 확인했습니다.

최종 결과: `.utmp/map-system/final-full.xml`, 로그: `.utmp/map-system/final-full.log`. 관련 회귀 테스트는 `.utmp/map-system/regression.xml`의 11/11 통과 결과로도 남아 있습니다. 화면 검수 자료는 `.utmp/map-system/map-start.png`, `map-completed.png`입니다. 실행 파일 빌드는 요청 범위에 포함하지 않았습니다.

선택 카드 UI 변경 후에는 영향 범위의 맵 관련 테스트 **39/39 통과**, 실패·건너뛰기 0개를 확인했습니다. 컴파일 오류와 실행 예외가 없으며, 카드 1·2·3장 중앙 정렬, 이미지·문구 교체, 이미지 제거 시 이전 이미지 정리, 12·21개 노드의 마스크/스크롤 및 현재 노드 표시, 기존 전투 5회 왕복을 검증했습니다. 별도 검토에서 발견한 긴 층의 진행 표시 겹침과 같은 프레임에 노드 수를 연속 변경하는 경우도 회귀 테스트로 검증했습니다. 전체 프로젝트 테스트는 UI 변경 후 다시 실행하지 않았습니다.

UI 변경 검증 결과: `.utmp/map-cards/verified-tests.xml`, 로그: `.utmp/map-cards/verified-tests.log`. 선택지 1·2·3장과 긴 층의 화면도 `.utmp/map-system/map-card-*.png`에 렌더링해 확인했습니다.

2026-11-03에는 관련 테스트로 시작 골드, 맵의 덱·옵션 버튼과 노드 입력 차단, 보상 카드의 맵 덱 반영, 전투 상단의 다음 노드 표시와 골드 수령·저장 실패 시 갱신을 확인했습니다.
