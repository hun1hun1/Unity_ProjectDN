# 옵션 창

타이틀·맵·전투에서 공용 `SettingsPanel`을 사용합니다. `BattleScene`에서는 `Canvas/TopArea/Options/OptionsButton`, `MapScene`에서는 `Canvas/MapUtilityButtons/OptionsButton`을 누릅니다. 왼쪽은 음량, 오른쪽은 화면 설정이며 맵과 전투의 하단에는 초기화·게임으로 돌아가기·타이틀로 돌아가기가 있습니다.

## 조작

- 전체 음량·배경음·효과음: 슬라이더를 움직이면 즉시 적용하고 저장합니다.
- 해상도·전체 화면: 목록과 체크박스를 선택한 후 **화면 설정 적용**을 누릅니다. 음량을 조절해도 적용 전 화면 선택은 유지됩니다.
- 설정 초기화: 음량은 100%, 화면은 설정 서비스가 시작될 때의 기본 화면 크기와 전체 화면 상태로 돌아갑니다.
- 닫기·게임으로 돌아가기: 설정 창을 닫습니다. 적용하지 않은 화면 선택은 다음에 열 때 현재 적용값으로 돌아갑니다.
- 타이틀로 돌아가기: 확인창에서 돌아가기를 누르면 최근 체크포인트를 유지하고 타이틀로 이동합니다. 저장 또는 이동 실패 시 확인창에서 안내하며 현재 화면에 머뭅니다. 타이틀 옵션에서는 확인 후 게임을 종료합니다.
- ESC: 종료 확인창 → 해상도 목록 → 설정 창 순서로 가장 앞에 열린 창 하나를 닫습니다.

전투에서는 플레이어가 입력할 수 있는 안정된 턴과 승리 후 보상 선택 중에 열 수 있으며, 열 때 기존 전체 덱·더미 창을 닫습니다. 카드 상세보기 중에는 설정 창을 열지 않습니다. 설정 중에는 전투·패·덱·더미·상세보기 입력을 차단하고, 닫은 프레임까지 차단해 클릭이 전투로 전달되지 않도록 합니다. 게임 시간 배율은 변경하지 않습니다. 맵에서는 옵션 창이 열려 있는 동안과 닫은 프레임에 노드 선택을 차단합니다. 상점에서 옵션을 열면 상점 위에 표시하고, 닫으면 같은 진열 화면으로 돌아옵니다. 열린 동안 상점 나가기 입력도 차단합니다.

## 화면 설정과 실행 환경

해상도 목록은 장치가 지원하는 크기를 사용하고, 같은 크기의 주사율 중복은 제거합니다. 현재 창 크기도 포함합니다. 저장된 해상도가 더 이상 지원되지 않으면 현재 창 크기를 사용합니다.

**Unity Editor에서는 Game 뷰의 실제 크기를 변경하지 않습니다.** 선택·적용·저장 로직은 작동하며, 실제 해상도와 전체 화면 전환은 독립 실행 플레이어에서 `Screen.SetResolution`으로 적용합니다. 전체 화면은 FullScreenWindow, 창 모드는 Windowed입니다. 타이틀의 게임 종료는 독립 실행 플레이어의 `Application.Quit`을 사용합니다. 맵과 전투의 타이틀 복귀는 에디터에서도 씬을 전환합니다.

## Inspector와 프리팹

- `Canvas/Settings`의 **Settings UI**: 전투, 기존 덱·더미 창, OptionsButton, 설정 패널, 각 컨트롤과 종료 확인창 참조
- `Assets/Prefabs/UI/SettingsPanel.prefab`: 패널 외형, 음량 행, 해상도 목록, 전체 화면 체크박스, 버튼과 확인창
- 패널 안 `MainContent/AudioSection`: 전체 음량·배경음·효과음 행과 수치
- `MainContent/VideoSection`: 해상도 목록·전체 화면·화면 설정 적용 버튼
- `QuitConfirmation`: 타이틀 복귀 또는 종료 확인 배경과 대화상자
- 맵과 전투의 `SettingsUI`는 `Return To Title`을 활성화하고 `Navigation Error`에 복귀 실패 안내를 연결합니다.

진행 저장의 상세 기준은 [RunCheckpoints.md](RunCheckpoints.md)에 있습니다.

맵과 전투의 OptionsButton은 투명한 Image로 클릭 영역을 유지하고, 자식 Icon Image를 Button의 Target Graphic으로 사용합니다. Icon에는 `Assets/Images/UI/OptionsButton/options-gear-silver-v2.png`가 연결됩니다. `ButtonHoverRotation`의 씬 설정은 45도·0.15초이며 마우스를 올리면 아이콘만 회전합니다. 포인터 이탈·옵션 열기·비활성화 시 원래 회전으로 복귀하며, 시간 배율과 무관하게 동작합니다. 기존 SettingsUI.Toggle의 On Click 연결은 유지합니다. 프리팹은 외형을 재사용하며, 외부 씬 컨트롤러를 호출하는 버튼 이벤트는 씬 인스턴스에 연결되어 있습니다. `ProjectDN/Setup Settings Window` 메뉴로 연결을 재구성할 수 있습니다. 기존 프리팹이나 패널을 재생성하지 않으며, 다른 버튼 이벤트도 유지합니다.

옵션 창을 닫을 때 전투에서는 이전 선택 오브젝트를 복원하고, 타이틀과 맵에서는 선택을 해제합니다. 세 화면 모두 닫은 프레임까지 아래 화면의 입력을 차단합니다.

## 오디오 연결

전체 음량은 `AudioListener.volume`으로 적용합니다. 배경음·효과음은 해당 AudioSource의 **Settings Audio Source** 컴포넌트에서 Bus를 Music 또는 Effects로 지정합니다. 컴포넌트는 원래 AudioSource Volume을 보존하고 채널 배율을 곱하므로 반복 변경 시 음량이 누적해서 줄어들지 않습니다.

기존 카드 연출이 생성하는 소리는 Effects로 자동 연결했습니다. 배경음을 추가할 때는 그 AudioSource에 SettingsAudioSource를 추가하고 Bus를 Music으로 지정합니다. AudioMixer나 새 패키지는 추가하지 않았습니다.

## 저장과 검증

설정은 `ProjectDN.Settings.v1.*` PlayerPrefs 키에 저장하며 게임 진행 데이터나 다른 키를 지우지 않습니다. 게임을 다시 시작할 때 불러와 적용합니다.

검증은 가짜 플랫폼으로 저장·해상도·종료를 대신 처리하여 사용자의 실제 환경 설정을 변경하지 않습니다. 실제 플레이어의 해상도 전환과 앱 종료는 별도의 플레이어 실행 검증 대상입니다.

주요 구현은 `Assets/Scripts/Settings/`와 `Assets/Scripts/UI/SettingsUI.cs`, 씬 구성은 `Assets/Editor/SettingsAssetSetup.cs`에 있습니다.
