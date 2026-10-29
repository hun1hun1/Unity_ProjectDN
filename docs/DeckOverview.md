# 꿈·악몽 전체 덱 보기

`BattleScene`의 `Canvas/TopArea/Decks/DeckButton`을 누르면 TopArea 아래에 전체 덱 패널이 표시됩니다. 왼쪽은 꿈 덱, 오른쪽은 악몽 덱입니다. 같은 버튼을 다시 누르거나 패널의 닫기 버튼을 누르면 닫힙니다.

## 표시 기준

- `DeckController.GetDeck()`이 반환하는 현재 보유 덱을 표시합니다. 패, 뽑을 더미, 버림 더미, 소멸 더미 사이의 이동으로 전체 목록이 줄어들지 않습니다.
- 같은 카드를 여러 장 보유하면 각각 표시하고, 카드를 획득하거나 제거한 결과는 다시 열 때 반영합니다.
- 전체 덱은 카드 이름순으로 표시합니다. 기존 뽑을·버림 더미의 무작위 표시 순서는 유지합니다.
- 두 목록은 각각 세로로 스크롤됩니다. 기준 해상도에서는 3열이며 좁은 영역에서는 열 수를 줄입니다.
- `DeckCard.prefab`의 기본 크기 200×260을 기준으로 표시합니다. 패 카드의 크기와는 독립적입니다.
- 목록 카드의 확대와 우클릭 상세보기를 재사용합니다. 일반 설명은 전투 문맥을 전달받고, 상세보기는 기존 규칙대로 기본 수치를 표시합니다.

## Inspector 설정

`Canvas/DeckOverview`의 **Deck Overview UI**에 컨트롤러, 전투, 전체 패널, 좌우 목록, 제목, 토글·닫기 버튼과 기존 더미 목록이 연결되어 있습니다.

`Canvas/DeckOverviewPanel/DreamSection`과 `NightmareSection`의 **Deck List UI**에서 각 목록을 조절합니다.

- **Card Scale**: 기본 `1`. `0.8`은 160×208, `1.25`는 250×325입니다. 글자와 간격도 함께 조절됩니다.
- **Sort By Name**: 전체 덱 목록은 켜짐, 기존 더미 목록은 꺼짐입니다.
- **Max Columns**: 전체 덱은 `3`. `0`이면 기존 GridLayoutGroup 열 설정을 그대로 사용합니다.
- 카드 외형은 **Card Prefab**의 `DeckCard.prefab`을 사용합니다.

각 영역의 `ScrollView/Viewport/Content`에는 카드 목록이 생성됩니다. GridLayoutGroup의 Padding/Spacing으로 목록 여백을 조절할 수 있습니다. 원본 덱과 전투 더미는 표시 과정에서 변경하지 않습니다.

## 입력과 정리

전투 입력이 안정된 플레이어 턴에 열 수 있습니다. 열린 동안 패 카드 사용·드래그·호버, 더미 열기, 턴 종료를 차단하며 TopArea의 토글 버튼과 목록 안의 상세보기는 사용할 수 있습니다. 상세보기가 입력을 잡고 있는 동안에는 전체 덱 패널을 닫거나 토글하지 않습니다. 닫은 프레임까지 전투 입력을 차단해 클릭이 뒤로 전달되지 않도록 합니다.

패널 외부 숨김, 컴포넌트 비활성화, 전투 종료 시에는 목록을 정리하고 입력 차단을 해제합니다. 게임 시간 배율은 변경하지 않습니다.

## 씬 재연결

`ProjectDN/Setup Deck Overview` 메뉴는 기존 DeckButton을 유지하면서 필요한 패널과 참조를 연결합니다. 이미 패널이 있으면 재생성하지 않습니다. 기존 버튼의 다른 On Click 이벤트도 보존합니다.

주요 구현 파일:

- `Assets/Scripts/UI/DeckOverviewUI.cs`: 전체 덱 조회, 토글, 제목과 입력 차단 수명
- `Assets/Scripts/UI/DeckListUI.cs`: 카드 생성, 이름 정렬, 목록 크기와 열 수
- `Assets/Editor/DeckOverviewAssetSetup.cs`: 씬 UI 생성과 직렬화 연결
- `Assets/Tests/Editor/DeckOverviewTests.cs`: 데이터, 입력, 상세보기, 스크롤과 정리 검증
