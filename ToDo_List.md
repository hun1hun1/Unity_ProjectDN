# ProjectDN ToDo List

작성 기준일: 2026-11-02

## 타이틀 버튼 디자인 적용

현재 상태: 버튼 디자인 PNG 8종은 제작하였으나 Unity 임포트·씬 연결·상태 전환·실행 확인은 아직 진행하지 않았다. 아래 체크박스는 실제 작업과 확인이 끝난 뒤 완료 처리한다.

- 대상 씬: `Assets/Scenes/TitleScene.unity`
- 원본 이미지: [GeneratedArt/UI/TitleMenuButtons-v1](GeneratedArt/UI/TitleMenuButtons-v1)
- 전체 이미지 묶음: [title-menu-buttons-v1.zip](GeneratedArt/UI/title-menu-buttons-v1.zip)
- 디자인 구성: [README.md](GeneratedArt/UI/TitleMenuButtons-v1/README.md)

기존 버튼 오브젝트와 `On Click()` 연결은 유지하고 외형을 변경한다. 문구는 이미지에 넣지 않고 기존 TextMeshPro 텍스트를 사용한다.

### 1. 이미지 임포트

- [ ] Unity Project 창에서 `Assets/Images/UI/Title` 폴더를 만든다.
- [ ] 원본 폴더의 PNG 8개를 해당 폴더에 복사한다. `GeneratedArt`에만 있는 파일은 Unity 에셋으로 사용할 수 없다.
- [ ] 복사한 PNG를 선택하고 아래 임포트 설정을 적용한 뒤 `Apply`를 누른다.

| 항목 | 설정 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Multiple |
| Mesh Type | Full Rect |
| Alpha Is Transparency | 체크 |
| Generate Mip Maps | 해제 |
| Filter Mode | Bilinear |
| Max Size | 4096 |
| Compression | None |

### 2. 스프라이트 영역과 기준점 설정

원본에는 넓은 투명 여백이 있으므로 PNG 전체를 버튼에 넣으면 그림이 작게 표시된다. Sprite Editor에서 실제 그림을 포함하는 사각형 하나를 지정한다.

- [ ] 각 PNG의 `Sprite Editor`를 열고 그림을 둘러싸는 Sprite Rect 하나를 만든다.
- [ ] 아래 값을 시작점으로 입력하고 그림과 테두리가 잘리지 않는지 확인한다.
- [ ] 버튼 7개는 같은 사각형과 `Center` 기준점을 사용한다.
- [ ] `Apply`를 누른 뒤 Project 창에서 PNG를 펼쳐 내부의 Sprite가 생성되었는지 확인한다.

| 대상 | X | Y | W | H | Pivot |
|---|---:|---:|---:|---:|---|
| 남청색·붉은 버튼 7개 공통 | 48 | 224 | 2080 | 312 | Center |
| title-selection-diamond.png | 400 | 210 | 460 | 860 | Center |

좌표는 현재 원본 이미지의 표시 영역을 분석한 시작값이며 Unity에서 적용하여 검증한 값은 아니다. Sprite Editor의 Y는 이미지 아래쪽을 기준으로 한다. 각 파일에 따로 자동 Trim을 적용하면 영역과 중심이 달라질 수 있으므로 상태 전환에 사용하는 버튼들은 공통 영역을 유지한다.

원본 버튼은 대부분 2172×724이며, `title-button-pressed.png`와 `title-confirm-red-selected.png`는 2171×724이다. 공통 Sprite Rect는 두 크기 모두에 들어간다. 생성 이미지이므로 영역을 맞추더라도 상태별 실루엣이 픽셀 단위로 완전히 같지는 않다.

### 3. 메인 메뉴 버튼에 기본 이미지 적용

적용 위치: `Canvas/TitleMenu/MainMenu`

- [ ] ContinueButton — 이어서 하기
- [ ] StartButton — 게임 시작
- [ ] TutorialButton — 튜토리얼
- [ ] OptionsButton — 옵션
- [ ] QuitButton — 게임 종료

위 다섯 버튼에 다음 설정을 적용한다.

| 컴포넌트 | 항목 | 설정 |
|---|---|---|
| Image | Source Image | title-button-normal.png 내부의 Sprite |
| Image | Color | 흰색, Alpha 1 |
| Image | Image Type | Simple |
| Image | Preserve Aspect | 체크 |
| Image | Raycast Target | 체크 |
| Outline | 컴포넌트 활성화 | 해제 |

- [ ] 기존 Image의 어두운 녹색을 흰색으로 변경한다. 기존 색을 남기면 새 이미지도 녹색으로 어둡게 표시된다.
- [ ] 기존 Outline을 꺼서 새 이미지의 은빛 테두리와 겹치지 않도록 한다.
- [ ] 기존 RectTransform 크기 380×68을 시작점으로 확인한다.

Preserve Aspect를 켜면 그림 비율을 유지하므로 버튼 그림의 높이는 클릭 영역보다 작게 표시될 수 있다. 비율을 유지한 표시와 클릭 영역을 구분하여 확인한다. `Set Native Size`를 누르면 고해상도 원본 크기가 반영되므로 화면 표시 크기는 RectTransform에서 지정한다.

### 4. 상태별 이미지 연결

- [ ] 각 버튼의 `Button > Transition`을 `Color Tint`에서 `Sprite Swap`으로 변경한다.
- [ ] `Target Graphic`에 같은 버튼 오브젝트의 Image를 지정한다.
- [ ] 아래 상태별 Sprite를 연결한다.

| 항목 | 연결할 이미지 내부의 Sprite |
|---|---|
| Image > Source Image | title-button-normal.png |
| Highlighted Sprite | title-button-selected.png |
| Pressed Sprite | title-button-pressed.png |
| Selected Sprite | title-button-selected.png |
| Disabled Sprite | title-button-disabled.png |

기본 상태는 Image의 Source Image를 사용한다. Highlighted는 마우스 올림, Selected는 키보드 등으로 선택한 상태에 대응한다.

- [ ] 기존 `On Click()`의 대상과 메서드는 변경하지 않는다.
- [ ] 기존 자식 `Text` 오브젝트의 문구를 유지한다.
- [ ] 글꼴은 `Assets/Fonts/Hahmlet-SemiBold SDF.asset`, 글자색은 밝은 은백색을 기준으로 설정한다.
- [ ] Text의 Raycast Target은 꺼서 버튼 입력을 가로채지 않도록 한다.

| 버튼 | 유지할 On Click 메서드 |
|---|---|
| ContinueButton | TitleMenuUI.ContinueGame |
| StartButton | TitleMenuUI.StartGame |
| TutorialButton | TitleMenuUI.OpenTutorial |
| OptionsButton | TitleMenuUI.OpenOptions |
| QuitButton | TitleMenuUI.RequestQuit |

### 5. 종료 확인창의 붉은 버튼 적용

적용 위치: `Canvas/TitleQuitPanel/Dialog/ConfirmButton`

메인 메뉴의 QuitButton은 남청색 버튼을 사용하고, 종료 확인창의 확정 버튼에만 붉은 디자인을 적용한다.

- [ ] ConfirmButton의 Image 색상을 흰색으로 바꾸고 기존 Outline을 끈다.
- [ ] Image Type은 Simple, Preserve Aspect는 체크한다.
- [ ] Button의 Transition을 Sprite Swap으로 바꾸고 Target Graphic을 연결한다.
- [ ] 아래 상태별 Sprite를 연결한다.

| 항목 | 연결할 이미지 내부의 Sprite |
|---|---|
| Image > Source Image | title-confirm-red-normal.png |
| Highlighted Sprite | title-confirm-red-selected.png |
| Pressed Sprite | title-confirm-red-pressed.png |
| Selected Sprite | title-confirm-red-selected.png |
| Disabled Sprite | title-button-disabled.png |

- [ ] 같은 Dialog의 CancelButton에는 일반 남청색 버튼과 일반 상태별 Sprite를 적용한다.
- [ ] ConfirmButton의 `TitleMenuUI.ConfirmQuit`, CancelButton의 `TitleMenuUI.CancelQuit` 연결을 유지한다.
- [ ] 확인창 버튼의 기존 크기 220×60에서 그림과 글자가 맞는지 확인한다. 글자 크기는 약 22를 시작점으로 조정한다.

### 6. 선택 마름모 연결

- [ ] 각 메뉴 버튼 아래에 `SelectionMark`라는 UI Image를 추가한다.
- [ ] `title-selection-diamond.png` 내부의 Sprite를 지정한다.
- [ ] Preserve Aspect를 켜고 Raycast Target은 끈다.
- [ ] 약 14×28 크기로 버튼 왼쪽에 배치하고 기본 상태에서는 숨긴다.
- [ ] 마우스 올림과 키보드 선택 상태에 따라 마름모를 표시하고, 선택이 해제되면 숨기도록 별도 표시 처리를 연결한다.

Sprite Swap은 Target Graphic의 이미지만 바꾸므로 자식 마름모를 자동으로 켜고 끄지 않는다. 현재 TitleMenuUI에는 마름모 상태 제어가 없다. 별도 스크립트 또는 애니메이션 연결이 필요한 항목이며, 마우스 올림과 키보드 선택이 겹칠 때도 표시가 올바르게 유지되는지 확인한다.

### 7. 배치와 실행 확인

- [ ] 로고 아래에 메뉴 버튼을 같은 중심선으로 세로 정렬한다. 버튼 간격은 약 14~18을 시작점으로 조정한다.
- [ ] 최대 5개 메뉴가 들어가는 공간을 확보하고 로고·배경의 주요 그림과 겹치지 않는지 확인한다.
- [ ] 이어서 하기가 표시될 때와 숨겨질 때 모두 배치를 확인한다. 자동으로 빈자리를 당겨 정렬하려면 별도 레이아웃 구성이 필요하며 현재 메뉴는 고정 위치 방식이다.
- [ ] Play Mode에서 기본·마우스 올림·키보드 선택·누름 상태를 확인한다.
- [ ] 상태 변경 시 버튼의 위치나 크기가 튀는지 확인한다.
- [ ] 사용 불가 상태에서 이미지가 변경되고 클릭되지 않는지 확인한다. 확인 후 테스트용 Interactable 변경은 원래대로 돌린다.
- [ ] 튜토리얼·옵션·종료 확인창의 열기와 닫기, 취소, ESC 동작이 유지되는지 확인한다.
- [ ] 유효한 저장이 있을 때 이어서 하기의 표시·선택과 실행을 확인한다.
- [ ] 1920×1080과 다른 화면 크기에서도 버튼·글자·마름모가 잘리거나 겹치지 않는지 확인한다.
- [ ] TitleScene을 저장한다. 실제 프로그램 종료 동작은 독립 실행 플레이어에서 별도로 확인한다.

## 아이디어

기록일: 2026-11-02. 아래 내용은 캐릭터 선택 화면과 로딩·화면 전환에 관해 논의한 제안이다. 확정 기획이나 구현 완료 항목과 구분하며, 배치 비율·시간·문구는 조정 가능한 예시이다.

### 1. 캐릭터 선택 화면

#### 진입 흐름과 확정 시점

타이틀의 게임 시작 -> 캐릭터 선택 -> 모험 시작 확정 -> 맵 순서로 구성하는 방향이다.

현재 게임 시작 버튼은 바로 새 런을 생성한다. 캐릭터 선택 화면을 도입할 때에는 새 런 생성과 기존 진행 저장 교체를 모험 시작 확정 시점으로 옮긴다. 캐릭터를 살펴보다 뒤로 돌아온 경우에는 기존 진행을 유지한다.

이어서 하기는 캐릭터 선택을 거치지 않고 저장된 캐릭터와 진행을 복원하는 방향이다. 선택 캐릭터를 런과 저장 데이터에 연결하는 처리가 필요하며, 현재 구현되어 있다는 의미는 아니다.

#### 화면 구도

캐릭터의 분위기를 큰 그림으로 보여주고, 전투 특징과 꿈·악몽 시작 덱을 확인한 뒤 시작할 수 있도록 구성한다.

| 영역 | 표시 내용 |
|---|---|
| 상단 | 작은 로고와 캐릭터 선택 제목 |
| 왼쪽 약 45% | 선택한 캐릭터의 큰 전신 그림 또는 대기 애니메이션 |
| 오른쪽 위 | 캐릭터 이름, 짧은 소개, 고유 자원과 전투 특징 |
| 오른쪽 아래 | 꿈·악몽 시작 덱 요약과 카드 상세보기 |
| 하단 | 캐릭터 초상화 목록 |
| 맨 아래 양 끝 | 뒤로와 모험 시작 버튼 |

배경은 기존 타이틀과 같은 장소를 이어 사용하는 방향이다. 로고와 메뉴가 사라지고 안개 낀 호숫가를 조금 가까이 보여주는 느낌으로 전환한다. 캐릭터는 왼쪽의 어두운 지면에 서 있고, 정보 패널 뒤에는 흐릿한 마을과 수면이 보이도록 구성한다.

정보 패널은 짙은 남청색의 반투명 바탕과 얇은 은빛 테두리를 사용한다. 캐릭터 그림이 있는 왼쪽은 비교적 열어두고, 글자가 있는 오른쪽은 배경 세부 묘사를 낮춰 가독성을 확보한다.

#### 캐릭터 정보와 시작 덱

군인 선택 시 표시 예시는 다음과 같다.

| 항목 | 표시 예시 |
|---|---|
| 이름 | 군인 |
| 전투 특징 | 탄약을 장전하고 소비하며 공격과 추가 효과를 조합한다. |
| 고유 자원 | 탄창 아이콘과 탄약 시스템 설명 |
| 꿈 시작 덱 | 8장 — 공격·방어·장전 구성 |
| 악몽 시작 덱 | 6장 — 피해와 부작용·상태 효과의 조합 |
| 상세 확인 | 시작 덱 보기 |

꿈 시작 덱과 악몽 시작 덱은 푸른 영역과 붉은 영역으로 나란히 표시한다. 각 영역에 이름도 함께 표기하여 색에만 의존하지 않으며, 캐릭터가 두 덱을 함께 운용한다는 점을 전달한다.

기본 화면에는 대표 카드 몇 장과 총 장수만 표시한다. 상세보기에서는 공격 ×3, 방어 ×3처럼 중복 카드를 묶어 보여주는 방향이다. 기존 카드 확대·설명 UI를 재사용하는 방안을 고려한다.

#### 초상화 선택과 반응

| 상태 | 표현 |
|---|---|
| 기본 | 어두운 초상화와 은빛 프레임 |
| 마우스 올림 | 테두리에 약한 푸른빛 |
| 선택 | 밝은 테두리와 마름모 표시 유지 |
| 선택 변경 | 큰 캐릭터 그림과 정보가 짧게 전환 |

초상화 클릭은 캐릭터 선택과 정보 확인까지만 처리한다. 실제 런 시작은 모험 시작 버튼으로 별도 확정한다. 마우스를 올린 초상화와 현재 선택한 초상화가 구분되도록 한다.

캐릭터가 늘어나면 같은 크기의 초상화를 가로로 스크롤하는 구성을 사용한다.

### 2. 로딩과 화면 전환

#### 현재 확인한 상태

현재 RunSession은 LoadSceneAsync로 씬을 전환하며 타이틀은 시작 버튼의 중복 입력을 제한한다. 타이틀에는 시작 중이라는 별도의 시각적 안내가 없다.

실제 빌드의 전환 시간을 측정한 것은 아니므로 현재 로딩이 빠르거나 별도 로딩 화면이 반드시 불필요하다고 확정하지 않는다.

#### 제안 방향

현재 단계에서는 전용 로딩 씬을 별도로 만드는 것보다, 짧은 화면 전환과 대기가 길어질 때의 로딩 표시를 함께 사용하는 방향이다.

| 전환 구간 | 제안 |
|---|---|
| 타이틀 -> 캐릭터 선택 | 같은 씬의 패널 전환이라면 짧은 페이드 사용 |
| 캐릭터 선택 확정 -> 맵 | 어두워지는 전환 화면으로 로딩 구간을 덮음 |
| 맵 -> 전투 | 같은 전환 화면을 재사용하고 대기가 길면 로딩 표시 |

남청색 안개가 화면을 덮고 다음 화면이 안개 속에서 나타나는 연출로 기존 미술 방향을 이어간다. 대기가 길어지면 작은 초승달 애니메이션과 불러오는 중… 문구를 표시한다.

- 빠르게 준비되면 전환 연출만 보여준다.
- 약 0.5~1초 이상 대기하면 로딩 표시를 추가하는 방안을 검토한다. 이 값은 확정 기준이 아닌 예시이다.
- 로딩 화면을 보여주기 위한 불필요한 긴 대기 시간은 추가하지 않는다.
- 캐릭터 선택 화면에서는 초상화·선택 캐릭터 변경에 즉시 반응하도록 하고, 모험 시작 확정 후 실제 게임 데이터를 준비하는 구간에 로딩 처리를 둔다.
- 최종 필요성과 표시 기준은 독립 실행 빌드의 첫 실행과 전투 진입 시 대기를 측정하여 판단한다.

## 참고 문서

- [게임 기획서](게임_기획서.md)
- [타이틀 화면](docs/TitleMenu.md)
- [옵션 창](docs/SettingsWindow.md)
- [Unity Sprite Editor](https://docs.unity.com/en-us/engine/6000.0/manual/unity2d/sprite/sprite-editor-window-reference/reference)
- [Unity 버튼 상태 전환](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-SelectableTransition.html)
