# Pull 후 테스트와 문서 정합성 수정 계획

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task in the current checkout.

**Goal:** 최신 씬·글꼴 설정과 어긋난 테스트 및 문서만 수정한다.

**Architecture:** 기존 씬, 프리팹, 글꼴과 게임 코드는 유지한다. 테스트의 기대값과 격리용 글꼴 생성 방식을 현재 에셋에 맞추며, 실제 문자열 렌더링 검증은 유지한다.

**Tech Stack:** Unity 6000.0.77f1, Unity Test Framework 1.6.0, TextMeshPro, Markdown.

**Spec:** 사용자의 요청: 문제가 있는 테스트들만 수정하고 문서를 업데이트한다.

## Global Constraints

- `Assets/Tests/Editor/TooltipFontTests.cs`, `SettingsWindowRuntimeTests.cs`와 관련 문서만 수정한다.
- 기존 UTF-8 및 CRLF를 보존한다.
- 에디터 구성 도구의 이동 전 이미지 경로는 이번 수정 범위에 포함하지 않는다.
- Unity 실행 전 직렬화 에셋을 보관하고, 실행 후 자동 변경만 복구한다.
- 커밋·빌드·패키지 변경은 수행하지 않는다.

## Review Focus

- 정적 아틀라스에서도 현재 카드·상태 설명이 대체 문자 없이 표시되는지 검사한다.
- 동적 아틀라스 스트레스 테스트는 저장된 정적 글꼴을 변경하지 않고 별도 글꼴로 실행한다.
- 회전 검증은 씬의 45도 설정을 따르며 클릭 영역·복귀·비활성화 검증을 유지한다.
- 문서의 구현 완료와 자동 테스트·수동 화면 확인을 구분한다.
- 전체 테스트 결과에서 추가 실패가 있으면 원인과 요청 범위를 확인한다.

## Task 1: 테스트 정합성 수정

- [x] 수정 전 관련 테스트의 실제 실패를 확인한다.
- [x] `TooltipFontTests.cs`의 글꼴 연결과 정적 아틀라스 기대값을 갱신하고 문자열 렌더링 및 격리된 동적 스트레스 검증을 유지한다.
- [x] `SettingsWindowRuntimeTests.cs`의 회전 기대값을 45도로 맞춘다.
- [x] 관련 테스트를 다시 실행한다.

## Task 2: 문서 및 전체 검증

- [x] `docs/TitleMenu.md`, `docs/SettingsWindow.md`, `docs/CardDescriptions.md`, `게임_기획서.md`, `ToDo_List.md`를 현재 구현에 맞게 갱신한다.
- [x] 렌더링을 켠 전체 EditMode 테스트와 컴파일 오류를 확인한다.
- [x] 자동 직렬화 변경, 인코딩, 줄바꿈, 최종 변경 범위를 확인한다.
- [x] 실제 실행 결과와 미검증 항목을 기록한다.

## 검증 기록

실행 결과는 `.utmp/pull-test-repair-20261103/`에 보관한다.

- 수정 전: Unity 6000.0.77f1, 렌더링 활성화, 관련 10건 모두 실패. 글꼴 9건은 정적 아틀라스와 오래된 동적 생성·참조 전제, 회전 1건은 45도와 60도 차이였다.
- 1차 수정 후: 관련 21건 중 16건 통과. 새 아틀라스 검사 5건이 배열의 미사용 null 슬롯까지 검사하여 실패했다. TMP의 활성 atlasTextureCount 범위만 검사하도록 보완했다.
- 최종 관련 테스트: 21/21 통과, 실패 0, 건너뜀 0 (`focused-final.xml`).
- 전체 EditMode 테스트: 522/522 통과, 실패 0, 건너뜀 0 (`all.xml`). Unity 종료 코드 0, C# 컴파일 오류 0. NVIDIA GeForce MX450 렌더러로 실행하였다.
- 별도 정적 리뷰: 수정이 필요한 추가 지적 없음.
- 작업 범위 결정: 현재 체크아웃에서 테스트와 문서만 수정하고 커밋하지 않는다. Unity 테스트의 자동 직렬화 변경은 실행 전 보관본으로 복구한다.
- 수동 화면 배치 확인, 독립 실행 플레이어의 해상도·종료 검증은 수행하지 않는다.

실행 중 자동 변경된 Hahmlet 글꼴, Soldier 프리팹, BattleScene을 실행 전 내용으로 복구하였다. 보관한 직렬화 에셋·메타 1,128개는 원본 해시와 모두 일치한다. 관련 테스트와 전체 테스트는 게임 코드·씬·프리팹·글꼴의 의도적인 변경 없이 수행하였다.
