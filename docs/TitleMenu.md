# 타이틀 화면

`Assets/Scenes/TitleScene.unity`가 Build Settings의 첫 씬입니다. 배경 이미지와 로고는 추가하지 않았으며 카메라의 단색 바탕 위에 기본 네 버튼이 있습니다. 유효한 진행 파일이 있으면 `이어서 하기`가 추가로 표시됩니다.

| 버튼 | 동작 |
|---|---|
| 이어서 하기 | 최근 체크포인트를 복원하고 전투·보상·비전투 노드 또는 맵을 재개합니다. |
| 게임 시작 | 기존 `RunSession.StartNewRun`으로 새 진행을 만들고 `MapScene`으로 이동합니다. 기존 런의 덱·유물·골드와 진행 저장은 새 게임으로 교체됩니다. |
| 튜토리얼 | 4쪽 설명창을 표시합니다. 이전·다음·닫기 버튼을 사용하며 게임 진행은 변경하지 않습니다. |
| 옵션 | 전투 화면과 같은 `SettingsPanel.prefab`과 설정 서비스를 사용합니다. 전체·배경음·효과음 음량, 해상도·전체 화면, 초기화, 종료 확인을 제공합니다. |
| 게임 종료 | 확인창의 종료 버튼을 눌렀을 때 설정을 저장하고 게임을 종료합니다. 취소 또는 ESC로 돌아옵니다. |

튜토리얼과 종료창은 ESC로 닫습니다. 옵션 창에서는 해상도 목록 → 종료 확인창 또는 옵션 창 순으로 앞에 열린 UI를 닫습니다. 창이 열려 있거나 닫히는 프레임에는 아래 메뉴가 실행되지 않습니다. 게임 시작 중에는 중복 시작을 막습니다.

저장 기준과 비전투 노드 연결 계약은 [RunCheckpoints.md](RunCheckpoints.md)에 있습니다.

## Inspector에서 내용 변경

- **버튼 위치·크기·문구:** `TitleScene`의 `Canvas/TitleMenu/MainMenu` 아래 각 버튼과 `Text`를 수정합니다.
- **튜토리얼 문구:** `Canvas/TitleMenu`의 `TitleMenuUI` 컴포넌트에서 `Tutorial Pages` 배열의 각 `Title`, `Body`를 수정합니다.
- **새 게임 데이터:** 같은 컴포넌트의 `Run Definition`은 현재 `MapScene`과 동일한 `TemporaryRun`을 참조합니다. 게임 규칙과 밸런스는 기존 데이터를 그대로 사용합니다.
- **공용 옵션 UI:** `Assets/Prefabs/UI/SettingsPanel.prefab`을 수정합니다. 타이틀 씬 인스턴스에는 전체 화면 크기와 `타이틀로 돌아가기` 문구만 별도로 적용되어 있습니다. `Canvas/Settings`의 `Require Battle`은 꺼져 있고 전투의 기본값은 켜져 있습니다.

`ProjectDN/Setup Title Scene`은 씬이 없을 때만 생성하며, 이미 만들어진 타이틀 씬의 수동 편집 내용을 덮어쓰지 않습니다.

Unity Editor에서는 `Application.Quit()`가 에디터를 닫지 않고 해상도 적용도 실제 창에 반영하지 않습니다. 실제 프로그램 종료와 창 크기 전환은 빌드에서 동작합니다.
