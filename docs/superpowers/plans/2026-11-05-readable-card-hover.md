# 해상도에 따른 카드 호버 가독성 구현 계획

> **For agentic workers** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal**

손패와 전체 덱·드로우·버림 더미 카드의 호버에서 본문을 화면 환산 크기 16 전후로 표시한다. 화면 안에 들어오는 크기를 상한으로 사용한다.

**Architecture**

CardHoverUI와 DeckCardHoverUI의 기존 HoverVisual 확대에 화면 배율과 현재 본문 크기를 반영한다. 공통 계산은 CardHoverSizing에 두고, 기존 위치 보정과 입력 차단을 재사용한다. 카드 높이 약 380픽셀은 현재 200×260 카드와 본문 크기 11에서 도출한 참고값이며, 구현의 기준값은 실제 본문 크기다.

**Tech Stack**

Unity 6000.0.85f2, C#, uGUI, TextMeshPro, Unity Test Framework.

**Spec**

이 채팅에서 합의한 방향을 따른다. 호버 시 전체 설명을 읽는 방식을 사용하고, 손패와 덱·카드 더미 목록에 같은 가독성 기준을 적용한다. 화면 환산 본문 크기 16은 최초 검증값이다.

## Global Constraints

- 기존 C# 인코딩과 줄바꿈을 보존한다.
- 사용자 작업 중인 옵션 프리팹, 글꼴, ScreenPixelOutline, RunAssetCatalog와 기획서의 변경을 보존한다.
- 기존 hoverScale은 기본 확대 배율로 사용하고, 부족한 경우 추가 확대한다.
- HoverVisual을 확대하고 화면 경계 보정을 유지한다. 카드 입력, 정렬 순서, 드래그·사용·버림 처리의 기존 계약을 유지한다.
- 새 씬, 패키지와 빌드를 생성하지 않는다.

## Review Focus

- 1280×720, 1600×900, 1920×1080에서 실제 표시 글자 크기를 측정한다.
- 호버를 유지한 채 해상도가 바뀌어도 확대가 누적되지 않아야 한다.
- 네 화면 가장자리에서 카드와 설명 패널이 화면 안에 표시되어야 한다.
- 포인터 이탈, 비활성화, 카드 교체와 드래그 시작 시 표시 상태가 복원되어야 한다.
- 현재 카드 중 가장 긴 설명이 실제 본문 영역에 들어가는지 확인한다. 기존 설명 영역 자체의 넘침이 발견되면 가독성 배율 문제와 함께 기록하고, 필요한 영역 변경을 검토한다.

## Task 1. 화면 크기 기반 호버 확대와 회귀 검증

**Files**

- Modify `Assets/Scripts/Card/CardHoverUI.cs`
- Modify `Assets/Scripts/UI/DeckCardHoverUI.cs`
- Modify `Assets/Scripts/Card/CardUI.cs` 및 `Assets/Scripts/UI/DeckCardUI.cs`에 기존 descriptionText를 읽는 내부 프로퍼티를 추가한다.
- Create `Assets/Scripts/UI/CardHoverSizing.cs`와 대응 `.meta`
- Modify `Assets/Tests/Editor/CardHoverTests.cs` 및 `Assets/Tests/Editor/DeckCardHoverTests.cs`
- Modify `Assets/Scripts/UI/CardRewardUI.cs`에서 기존 확인 버튼을 호버 경계로 제공한다. 보상 카드와 전체 덱 테스트의 고정 배율 기대값도 새 가독성 기준에 맞춘다.
- Modify `docs/CardHandLayout.md`에 실제 구현과 검증 범위를 기록한다.

**Interfaces**

- CardUI와 DeckCardUI의 `internal TMP_Text DescriptionText`를 공통 계산에서 사용한다.
- `CardHoverSizing.GetTargetScale(RectTransform visual, TMP_Text description, Canvas canvas, Vector3 baseScale, float configuredScale, float minimumFontPixels)`는 표시 배율을 반환한다.
- 기준 크기에서의 본문 화면 크기를 측정하고, 목표 배율을 `max(configuredScale, minimumFontPixels / currentFontPixels)`로 구한다. 화면 경계 여백을 포함한 가용 크기로 상한을 계산한다.
- 측정 시 현재 애니메이션 배율을 제거한 기준 상태를 사용한다. 본문 참조가 없는 경우 기존 확대 동작을 사용한다.

- [x] 파일 인코딩과 기존 테스트 구조, Unity CLI 및 실행 중 Editor 상태를 확인한다.
- [x] 실제 프리팹을 사용해 720p·900p·1080p의 본문 화면 크기, 확대 상태의 해상도 변경, 화면 경계, 복원 동작을 검사하는 회귀 테스트를 먼저 작성한다.
- [x] 기존 코드에서 새 가독성 테스트가 실패하는 것을 확인한다.
- [x] 공통 확대 계산과 두 호버 진입점의 연결을 구현한다.
- [x] 고정 배율만 기대하던 테스트를 가독성과 기존 동작 계약을 함께 검사하도록 갱신한다.
- [x] 관련 테스트와 기존 카드 설명·툴팁·입력 차단 테스트를 실행한다.
- [x] 현재 데이터의 긴 설명과 실제 렌더링을 확인한다.
- [x] Unity 컴파일 오류와 완료된 검사 결과를 확인하고, 실제 통과·실패·중단 항목을 기록한다.
- [ ] 최종 코드의 전체 EditMode 검사 완주 결과를 확보한다. 사용자 요청으로 중단했다.

관련 테스트 명령은 다음을 사용한다. 실행 중 Editor와의 연결 방식은 실행 전에 확인한다.

```powershell
unity test . --mode EditMode --editor-version 6000.0.85f2 --filter 'CardHoverTests|DeckCardHoverTests' --output .utmp/readable-card-hover/focused.xml
unity test . --mode EditMode --editor-version 6000.0.85f2 --output .utmp/readable-card-hover/all.xml
```

## 현재 상태

구현과 관련 검증을 완료했다. 최종 전체 EditMode 검사는 사용자 요청으로 중단하고 문서를 갱신했다.


## 검증 기록

- Unity 6000.0.85f2와 NVIDIA GeForce MX450의 Direct3D 11 렌더링을 사용한다.
- 수정 전 새 가독성 회귀 테스트 2건이 실패했고, 구현 후 2/2 통과했다.
- 기존 호버·카드 상세보기·덱 목록·손패와 새 가독성·본문 넘침 검사를 포함한 관련 테스트 22/22가 통과했다.
- 720p·900p·1080p 손패 및 목록의 렌더링 6장을 저장하고 720p 두 화면을 확인했다.
- 독립 검토에서 수정이 필요한 기능 결함은 발견되지 않았다.
- 전체 결과와 로그는 로컬 .utmp/readable-card-hover/에 기록한다.
- 최초 전체 검사 527건 중 525건이 통과했다. 보상 카드와 전체 덱 테스트의 고정 1.3배 기대값 2건을 갱신했다. 후속 검사에서 작은 화면의 보상 호버가 확인 버튼을 가리는 회귀를 재현해, 확인 버튼 위쪽으로 표시 영역을 제한하는 보정을 추가했다. 720p의 가독성과 확인 버튼 노출을 함께 검증한다.
- 최종 관련 회귀 검사 18/18이 통과했다. 결과는 `.utmp/readable-card-hover/regression-final.xml`에 있다. 최초 전체 검사에서 실패했던 두 검사가 포함된다.
- 720p 보상 호버의 추가 검증과 렌더링을 완료했다. 확대된 카드의 본문과 확인 버튼을 함께 확인했다.
- 검증에는 설치된 Unity Editor의 `-runTests` 실행을 사용했고 C# 컴파일 오류는 없었다.
- 최종 전체 재검사는 2026-11-05 사용자 요청으로 실행 중단했다. 해당 실행은 완료된 XML 결과를 남기지 않았다. 로그는 `.utmp/readable-card-hover/all-final-editor.log`에 있다.
- 작업 브랜치는 `codex/readable-card-hover`이며 기존 프로젝트 폴더에서 수정했다. 테스트의 자동 씬·프리팹·글꼴 재직렬화는 원본 해시를 확인한 백업으로 복원했다.
