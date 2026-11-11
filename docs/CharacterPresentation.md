# 캐릭터 이미지와 체급별 UI

플레이어와 적은 `BattleCharacterPresentation`으로 그림, HP·상태 UI, 적 행동 UI를 별도로 배치합니다. 전투 기능과 입력용 루트는 유지하며 그림의 크기를 변경해도 정보 UI의 글자와 아이콘을 함께 확대하지 않습니다.

## 현재 연결

| 프리팹 | 그림/idle | 기본 프로필 |
|---|---|---|
| `Assets/Prefabs/Player/Soldier.prefab` | Soldier Idle v2 | Normal |
| `Assets/Prefabs/Enemy/TestEnemy.prefab` | Slime Idle v1 | Small |
| `Assets/Prefabs/Enemy/Scarecrow.prefab` | Scarecrow Idle v1 | Normal |
| `Assets/Prefabs/Enemy/Lizard.prefab` | Lizard Idle v1 | Large |

기존 일반 전투의 Enemy Prefab은 TestEnemy로 유지됩니다. 다른 적을 선택하려면 해당 `BattleEncounterDefinition`의 Enemy Prefab에 Scarecrow 또는 Lizard를 지정합니다. 적 HP와 행동은 기존 방식대로 encounter 데이터가 결정하며, 이번 작업은 기존 encounter의 전투 수치를 바꾸지 않습니다.

## Inspector에서 크기와 UI 교체

1. 적 프리팹의 `BattleCharacterPresentation > Profile`에 Small / Normal / Large 중 하나를 지정합니다.
2. 프로필 에셋은 `Assets/CharacterPresentation/Profiles`에 있습니다.
3. `Visual Size`는 그림 프레임이 들어갈 최대 영역입니다. 원본 비율을 유지하며 발바닥을 같은 기준선에 맞춥니다.
4. `Visual Offset`으로 그림의 발 기준 위치를 보정합니다.
5. `Info Prefab`을 교체하면 다른 HP·상태 UI를 사용할 수 있습니다. `Info Size`, `Info Offset`으로 정보 UI만 조절합니다.
6. `Action Prefab`, `Action Size`, `Action Gap`으로 적 행동 UI와 간격을 설정합니다. 행동 UI는 투명 여백을 제외한 그림의 실제 상단 위에 배치됩니다.

기본 그림 영역은 Small 352×352, Normal 384×384, Large 480×480이며 HUD 폭은 각각 288 / 320 / 360입니다. 화면 높이가 부족하면 행동 UI가 들어갈 공간을 확보하면서 그림만 자동 축소합니다. HP·방어 글자는 기본 18, 상태 아이콘은 28×28로 유지됩니다. 방어도 방패는 HP 바 왼쪽에 세로 중앙을 맞추고, 상태 행은 HP 바 왼쪽에 맞추고 기준 해상도에서 아래로 6px 띄웁니다. 상태 행 배경은 활성 아이콘 범위에 맞춰 표시합니다. [상태 아이콘](StatusEffects.md#아이콘-설정) 참고.

프로필은 공유 에셋이므로 수정하면 해당 프로필을 사용하는 모든 캐릭터에 반영됩니다. 특정 적만 다르게 하려면 프로필을 복제해 해당 적에 지정합니다. 플레이 중 프로필 변경도 반영되며, 필요하면 `presentation.ApplyProfile(profile)`을 호출할 수 있습니다.

Inspector에서 지정한 프로필은 전투 실행 시 적용됩니다. 편집 화면의 저장된 배치까지 갱신하려면 `ProjectDN > Setup Character Presentation` 메뉴를 사용합니다.

사용자 정의 Info Prefab에는 `CharacterInfoUI`와 HP Fill / HP Text / Block Text 연결이 필요합니다. 기존 HP·방어·상태와 플레이어의 지연 효과 바인딩을 유지한 채 HUD를 교체합니다.

생성된 캐릭터 안의 정보·행동 UI는 원본 UI 프리팹과 연결되어 있으므로 해당 프리팹의 디자인 변경도 반영됩니다. 재생성 메뉴는 이미 지정한 캐릭터별 Profile / Idle Animation과 프로필·재생 간격의 사용자 설정을 보존합니다.

## 애니메이션

`UISpriteAnimator`가 uGUI Image의 스프라이트를 순서대로 변경합니다. 코루틴을 추가하지 않으며 비활성화 후 다시 활성화하면 첫 프레임부터 시작합니다. 시간 배율이 0이면 멈춥니다.

| 애니메이션 에셋 | 프레임 간격 | 반복 주기 |
|---|---:|---:|
| SoldierIdle | 0.06초 | 1.44초 |
| SlimeIdle | 0.08초 | 1.92초 |
| ScarecrowIdle | 0.10초 | 2.40초 |
| LizardIdle | 0.12초 | 2.88초 |

각 시트는 4608×3072, 6열×4행이며 프레임은 768×768입니다. 모든 프레임을 같은 셀 크기로 분할하고 Pivot `(0.5, 0.0625)`을 사용합니다. PNG 원본은 `GeneratedArt`에 유지하고 사용 시트만 `Assets/Images/Characters`로 복사합니다. 전체 애니메이션의 알파 영역을 측정해 행동 UI의 배치 기준을 저장합니다.

## 기존 UI와 호환

- 기존 CharacterInfoUI 오브젝트는 참조와 씬 오버라이드를 보존하기 위해 비활성 상태로 남습니다.
- 루트의 기존 Image는 투명하게 두고, 표시용 Visual의 Raycast Target은 끕니다.
- 새 적은 자신의 행동 UI를 사용합니다. presentation이 없는 기존 적은 BattleManager의 씬 행동 UI를 사용합니다.
- 에셋 재생성 메뉴는 `ProjectDN > Setup Character Presentation`입니다. 기존 GUID·스프라이트 ID와 사용자가 수정한 프로필 값을 유지합니다.

검증용 테스트는 `CharacterPresentationTests`, `CharacterPresentationSceneTests`에 있습니다. 원본 그림, 카드 배경과 카드·적 행동의 밸런스 데이터는 변경하지 않습니다.
