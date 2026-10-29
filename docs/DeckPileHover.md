# 카드 더미 확대와 설명 툴팁

`Assets/Scenes/BattleScene.unity`의 네 카드 더미에 적용되어 있습니다.
포인터를 올리면 이미지가 1.1배로 확대되고 0.2초 후 설명이 표시됩니다.
포인터를 떼거나 더미를 클릭하면 툴팁이 닫힙니다. 클릭 시 기존 카드 목록이 열립니다.

## 설명 작성 위치

Play 모드를 종료한 상태에서 `BattleScene`을 열고 다음 오브젝트 중 하나를 선택합니다.

- `Canvas/DeckCostZone/DreamZone/DreamDrawPile/DreamDrawPileButton`
- `Canvas/DeckCostZone/DreamZone/DreamDiscardPile/DreamDiscardPileButton`
- `Canvas/DeckCostZone/NightmareZone/NightmareDrawPile/NightmareDrawPileButton`
- `Canvas/DeckCostZone/NightmareZone/NightmareDiscardPile/NightmareDiscardPileButton`

Inspector의 **Deck Pile Hover UI → Tooltip Text**에 설명을 입력하고 씬을 저장합니다.
설명은 각 오브젝트에 별도로 저장됩니다. 현재는 더미 이름만 들어 있습니다.
여러 줄 입력과 TMP 리치 텍스트(`<b>강조</b>`, `<color=#FFD966>색상</color>`)를 사용할 수 있습니다.
내용을 비우면 확대만 동작하고 툴팁은 표시되지 않습니다.

- **Hover Scale**: 확대 배율, 기본 `1.1`
- **Hover Duration**: 확대·축소 시간(초), 기본 `0.12`
- **Tooltip Delay**: 툴팁 표시 지연(초), 기본 `0.2`

설명을 바꾸기 위해 C# 파일을 수정할 필요는 없습니다.
저장되는 파일은 `Assets/Scenes/BattleScene.unity`이며, 필드는
`Assets/Scripts/UI/DeckPileHoverUI.cs`의 `tooltipText`입니다.

## 외형과 구성

`Assets/Prefabs/UI/DeckPileTooltip.prefab`에서 배경, Content의 글꼴·크기·색상을 조절합니다.
기존 카드 상태 툴팁과 같은 외형을 사용하며, 창 가장자리에서 화면 안으로 위치를 보정합니다.
툴팁은 포인터 입력을 가로채지 않습니다.

카드 더미 이미지의 Sprite는 각 버튼 아래 `HoverVisual`의 Image에 있습니다.
버튼 루트는 기존 128×128 클릭 영역을 유지하며, 확대는 `HoverVisual`에만 적용됩니다.

`ProjectDN/Setup Deck Pile Hover` 메뉴는 네 버튼과 프리팹을 다시 연결하는 용도입니다.
이미 작성한 Tooltip Text와 확대 설정은 유지합니다.
