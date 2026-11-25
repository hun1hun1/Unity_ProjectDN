# 캐릭터 이미지와 체급별 UI

플레이어와 적은 `BattleCharacterPresentation`으로 그림, HP·상태 UI, 적 행동 UI를 별도로 배치합니다. 전투 기능과 입력용 루트는 유지하며 그림의 크기를 변경해도 정보 UI의 글자와 아이콘을 함께 확대하지 않습니다.

## 현재 연결

| 프리팹 | 그림/idle | 기본 프로필 |
|---|---|---|
| `Assets/Prefabs/Player/Soldier.prefab` | Soldier Idle v2 | Normal |
| `Assets/Prefabs/Enemy/Slime.prefab` | Slime Idle v1 | Small |
| `Assets/Prefabs/Enemy/Scarecrow.prefab` | Scarecrow Idle v1 | Normal |
| `Assets/Prefabs/Enemy/Lizard.prefab` | Lizard Idle v1 | Large |
| `Assets/Prefabs/Enemy/LockedGatekeeper.prefab` | LockedGatekeeperIdle (왼쪽을 향함) | Large |

1층의 전반부·후반부·엘리트 조우는 각 풀의 적 프리팹을 사용하며, 13번 보스 조우는 잠긴 문지기 프리팹을 사용합니다. 전투별 HP와 행동은 `BattleEncounterDefinition`에서 정합니다. [1층 조우 목록](MapSystem.md#조우-순서)

## Inspector에서 크기와 UI 교체

1. 적 프리팹의 `BattleCharacterPresentation > Profile`에 Small / Normal / Large 중 하나를 지정합니다.
2. 프로필 에셋은 `Assets/CharacterPresentation/Profiles`에 있습니다.
3. `Visual Size`는 그림 프레임이 들어갈 최대 영역입니다. 원본 비율을 유지하며 발바닥을 같은 기준선에 맞춥니다.
4. `Visual Offset`으로 그림의 발 기준 위치를 보정합니다.
5. `Info Prefab`을 교체하면 다른 HP·상태 UI를 사용할 수 있습니다. `Info Size`, `Info Offset`으로 정보 UI만 조절합니다.
6. `Action Prefab`, `Action Size`, `Action Gap`으로 적 행동 UI와 간격을 설정합니다. 행동 UI는 투명 여백을 제외한 그림의 실제 상단 위에 배치됩니다.

기본 그림 영역은 Small 352×352, Normal 384×384, Large 480×480이며 HUD 폭은 각각 288 / 320 / 360입니다. 적 프로필은 높이 56의 행동 예고와 간격 12를 확보하며, 화면 높이가 부족하면 그림을 자동 축소합니다. HP·방어 글자는 기본 18, 상태 아이콘은 28×28로 유지됩니다. 방어도 방패는 HP 바 왼쪽에 세로 중앙을 맞추고, 상태 행은 HP 바 왼쪽에 맞추고 기준 해상도에서 아래로 6px 띄웁니다. 상태 행 배경은 활성 아이콘 범위에 맞춰 표시합니다. [상태 아이콘](StatusEffects.md#아이콘-설정) 참고.

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

## 군인 패배 연출

플레이어 HP가 0이 되면 전투를 패배로 마치고, 마지막 행동의 연출이 정리된 뒤 [SoldierDefeat](../Assets/CharacterPresentation/Animations/SoldierDefeat.asset)를 한 번 재생합니다. 마지막 프레임에 도달하면 `GameOverUI`가 패배 패널을 페이드 인합니다. 런에서는 군인 이름과 도달 층·노드, 마지막 조우를 표시합니다. `타이틀로 돌아가기`를 누르면 종료된 런의 체크포인트 정리를 확인하고 타이틀을 엽니다. 실패하면 패널에서 안내하고 다시 시도할 수 있습니다. [체크포인트 정리](RunCheckpoints.md#저장-기준)

2026-11-18 Unity 6000.0.85f2의 패배 연출 관련 EditMode 검사 43/43이 통과했습니다. 로컬 결과 `.utmp/game-over/final.xml`에는 쓰러짐 이후 패널 표시, 타이틀 복귀와 이어서 하기 무효화, 저장 정리 실패 후 재시도가 포함됩니다. 공용 매개체 변경 후의 관련 검사 기록은 [맵 검증](MapSystem.md#공용-매개체와-효과-검증)에 있습니다.

## 적 사망 모션

현재 1층 조우에 등장하는 모든 적에 연결했습니다. 전반부 일반 4종, 후반부 일반 5종, 엘리트 3종, 보스 1종으로 총 13종입니다. 각 적의 투명 시트는 `Assets/Images/Characters/<적 이름>/Defeat.png`에 저장합니다. 2048×2048 시트 한 장에 512×512 프레임 16개를 4열×4행으로 배치했습니다. 프리팹의 `BattleCharacterPresentation.defeatAnimation`은 해당 적의 `<적 이름>Defeat.asset`을 참조합니다. [프리팹 연결](EnemyPrefabs.md#사망-모션-연결)

적의 HP가 0이 되면 마지막 행동 연출이 사용하던 위치와 스프라이트가 복원될 시간을 두고, 사망 모션을 한 번 재생합니다. 대기와 사망 스프라이트의 Pixels Per Unit과 사망 클립의 바닥 피벗을 사용해 여백이 있는 프레임에서도 외형 크기와 발밑 기준점을 맞춥니다. 재생을 시작할 때 행동 예고를 내리고, 마지막 투명 프레임에 도달하면 적 이미지와 HP 바를 숨긴 뒤 보상창이 나타납니다. 치명 피해 숫자는 사망 모션과 함께 표시될 수 있습니다. [전투 보상창](BattleRewards.md#보상-화면)

### 1층 전반부 적 사망 모션

| 적 | 동작 | 길이 |
|---|---|---:|
| 슬라임 | 몸이 부풀었다 터지며 점액 방울이 튀고, 바닥에 얇은 웅덩이가 남습니다. | 0.72초 |
| 도마뱀 | 두 앞발이 들리며 뒤로 휘청인 뒤, 배가 드러난 옆 자세로 넘어져 잠시 멈춥니다. | 0.88초 |
| 접힌 종이병정 | 몸통과 팔다리가 접히고, 종이 조각들이 서로 다른 방향으로 떨어집니다. | 0.80초 |
| 등불 나방 | 빠르게 날갯짓하며 치솟았다 추락합니다. 몸통의 불빛이 꺼지고 작은 불씨가 떠오릅니다. | 0.92초 |

도마뱀이 넘어지는 프레임에는 옆으로 쓰러진 자세를 반영했습니다. [도마뱀 자세 원본](../GeneratedArt/Lizard/lizard-defeat-prone-v1.png)

### 1층 후반부 적 사망 모션

| 적 | 동작 | 길이 |
|---|---|---:|
| 재봉거미 | 바늘 다리가 몸 밑으로 접힙니다. 붉은 실이 풀리면서 실타래가 몸에서 떨어져 굴러갑니다. | 0.96초 |
| 허수아비 | 허리가 꺾이면서 몸통이 무너집니다. 모자가 옆으로 떨어지고 마른 짚이 흩어집니다. | 0.88초 |
| 움직이는 빗자루 | 공중에서 한 바퀴 돌고 떨어집니다. 손잡이와 솔이 갈라지고 낱가닥이 흩어집니다. | 0.84초 |
| 잉크 까마귀 | 날개를 접고 떨어지면서 깃털 끝과 몸통에서 먹물이 아래로 흘러 번집니다. | 0.90초 |
| 침대 시트 유령 | 시트가 위로 부풀고 영혼이 연기처럼 빠져나갑니다. 힘이 풀린 천이 바닥에 내려앉습니다. | 1.02초 |

시트 유령이 사라지는 프레임에는 힘이 풀려 내려앉은 천 모양을 반영했습니다. [내려앉은 천 원본](../GeneratedArt/SheetGhost/sheet-ghost-defeat-cloth-v1.png)

### 1층 엘리트와 보스 사망 모션

| 적 | 동작 | 길이 |
|---|---|---:|
| 양철 나무꾼 | 금속 관절에서 불꽃이 튀고 도끼를 놓칩니다. 무릎이 꺾이면서 상체가 주저앉습니다. | 1.04초 |
| 깨진 종 | 마지막으로 크게 흔들리며 울림이 퍼집니다. 금을 따라 종이 갈라지고 추와 파편이 떨어집니다. | 0.98초 |
| 뒤집힌 모래시계 | 모래가 잠시 멎고 유리에 금이 갑니다. 유리알이 깨지면서 모래가 아래로 쏟아지고 틀이 기웁니다. | 1.08초 |
| 잠긴 문지기 | 열쇠구멍의 불빛이 깜빡이다 꺼집니다. 돌 문틀에 틈이 나고 문짝이 내려앉으며 돌조각과 먼지가 떨어집니다. | 1.36초 |

잠긴 문지기의 마지막 동작에는 문짝과 돌 문틀이 무너진 모습을 반영했습니다. [무너진 문지기 원본](../GeneratedArt/LockedGatekeeper/locked-gatekeeper-defeat-v1.png)

### 적 사망 모션 검증

2026-11-25 Unity 6000.0.85f2의 관련 검사에서 적 13종을 실제 전투 씬에 생성해 시트의 16프레임 참조, 한 번 재생 후 마지막 프레임 도달, 크기와 바닥 기준점, 재생 중 보상 패널 숨김, 재생 완료 후 HP 바 숨김을 확인했습니다. 적별 1280×720 재생 화면은 로컬 `.utmp/reward-confirm/defeat-*.png`에 있습니다. [전투 보상 검증 결과](BattleRewards.md#검증)

## 신규 적 스프라이트와 대기 애니메이션

아래 9종의 기본 스프라이트, 대기 스프라이트 시트, GIF 미리보기를 제작했습니다. 각 시트는 4608×3072 투명 PNG이며 768×768 프레임을 6열×4행으로 배치합니다. 왼쪽 위부터 행 순서로 읽고, 프레임당 100ms로 재생하면 2.4초 동안 한 바퀴 돕니다.

| 적 | 기본 스프라이트 | 대기 애니메이션 | 동작 |
|---|---|---|---|
| 깨진 종 | [PNG](../GeneratedArt/BrokenBell/broken-bell-v1.png) | [스프라이트 시트](../GeneratedArt/BrokenBell/broken-bell-idle-spritesheet.png) · [GIF](../GeneratedArt/BrokenBell/broken-bell-idle-preview.gif) | 부유하며 좌우로 기울고 종추가 뒤따라 흔들립니다. |
| 등불 나방 | [PNG](../GeneratedArt/LanternMoth/lantern-moth-v1.png) | [스프라이트 시트](../GeneratedArt/LanternMoth/lantern-moth-idle-spritesheet.png) · [GIF](../GeneratedArt/LanternMoth/lantern-moth-idle-preview.gif) | 부유하며 천천히 날갯짓하고 더듬이와 날개 끝이 흔들립니다. |
| 잉크 까마귀 | [PNG](../GeneratedArt/InkCrow/ink-crow-v1.png) | [스프라이트 시트](../GeneratedArt/InkCrow/ink-crow-idle-spritesheet.png) · [GIF](../GeneratedArt/InkCrow/ink-crow-idle-preview.gif) | 날갯짓에 따라 날개 끝의 잉크 방울이 흩어졌다 모입니다. |
| 움직이는 빗자루 | [PNG](../GeneratedArt/HauntedBroom/haunted-broom-v1.png) | [스프라이트 시트](../GeneratedArt/HauntedBroom/haunted-broom-idle-spritesheet.png) · [GIF](../GeneratedArt/HauntedBroom/haunted-broom-idle-preview.gif) | 공중에서 몸체가 기울고 빗살 끝이 뒤따라 움직입니다. |
| 양철 나무꾼 | [PNG](../GeneratedArt/TinWoodcutter/tin-woodcutter-v1.png) | [스프라이트 시트](../GeneratedArt/TinWoodcutter/tin-woodcutter-idle-spritesheet.png) · [GIF](../GeneratedArt/TinWoodcutter/tin-woodcutter-idle-preview.gif) | 발을 고정하고 상체가 무게를 옮기며, 고개와 도끼가 뒤따라 움직입니다. |
| 침대 시트 유령 | [PNG](../GeneratedArt/SheetGhost/sheet-ghost-v1.png) | [스프라이트 시트](../GeneratedArt/SheetGhost/sheet-ghost-idle-spritesheet.png) · [GIF](../GeneratedArt/SheetGhost/sheet-ghost-idle-preview.gif) | 몸체가 천천히 오르내리고 시트 밑단이 뒤따라 물결칩니다. |
| 접힌 종이병정 | [PNG](../GeneratedArt/FoldedPaperSoldier/folded-paper-soldier-v1.png) | [스프라이트 시트](../GeneratedArt/FoldedPaperSoldier/folded-paper-soldier-idle-spritesheet.png) · [GIF](../GeneratedArt/FoldedPaperSoldier/folded-paper-soldier-idle-preview.gif) | 발을 고정하고 몸통과 접힌 팔이 조금씩 엇박자로 흔들립니다. |
| 뒤집힌 모래시계 | [PNG](../GeneratedArt/InvertedHourglass/inverted-hourglass-v1.png) | [스프라이트 시트](../GeneratedArt/InvertedHourglass/inverted-hourglass-idle-spritesheet.png) · [GIF](../GeneratedArt/InvertedHourglass/inverted-hourglass-idle-preview.gif) | 본체가 떠서 좌우로 기울고 모래가 아래에서 위로 흐릅니다. |
| 재봉거미 | [PNG](../GeneratedArt/SewingSpider/sewing-spider-v1.png) | [스프라이트 시트](../GeneratedArt/SewingSpider/sewing-spider-idle-spritesheet.png) · [GIF](../GeneratedArt/SewingSpider/sewing-spider-idle-preview.gif) | 몸통이 오르내리고 앞다리와 풀린 실 끝이 움직입니다. |

원본은 `GeneratedArt`에 보관합니다. Unity에서는 각 적의 `Assets/Images/Characters/<적 이름>/Base.png`와 `Idle.png`를 사용합니다. 분할한 24프레임은 `Assets/CharacterPresentation/Animations`의 대기 애니메이션에 연결했고, 프리팹은 `Assets/Prefabs/Enemy`에 저장했습니다. 9종 모두 1층 조우 풀에 등록되어 있습니다. [1층 조우 목록](MapSystem.md#조우-순서)

## 방어도 획득 연출

플레이어와 적이 방어도를 실제로 얻으면 캐릭터 그림 위에 방패가 나타나고, HP 바 옆 방어도 배지 위로 `+획득량`이 올라갑니다. 방어 파괴 등으로 실제 획득량이 0이면 연출을 표시하지 않습니다. 0.14초 안에 연달아 획득한 방어도는 한 수치로 합산합니다. 마지막 획득을 기준으로 약 0.45초 동안 표시하며 배지 크기는 연출이 끝나면 원래대로 돌아갑니다.

방패 이미지는 HUD의 방어도 배지에서 가져옵니다. [방어도 연출 테스트](../Assets/Tests/Editor/SharedHudArtworkTests.cs#L119)는 양쪽 캐릭터의 실제 획득량, 연속 획득 합산, 지연 카드에서 발생한 획득과 연출 종료를 다룹니다.

## 체력 변화 숫자 연출

공격·회복 카드, 지속·지연 효과, 적의 공격, 화상·가시로 HP가 바뀌면 HP 바 갱신과 함께 대상 옆에 숫자를 띄웁니다. 치명 피해는 사망 처리 직전에 표시를 시작하고, 저장한 HP를 복원할 때는 HP 바만 갱신합니다. 숫자가 떠오르는 동안에도 효과 처리와 턴 진행은 계속됩니다. [체력 변경 처리](../Assets/Scripts/BattleCharacter/BattleCharacter.cs#L89) · [공용 숫자 프리팹](../Assets/Resources/HealthChangeNumber.prefab)

표시 수치는 처리 전후 HP의 차이입니다. 피해 12 중 방어도가 5를 흡수하면 `−7`을, 최대 HP까지 3만 남은 상태에서 10을 회복하면 `+3`을 표시합니다. 방호막 상쇄나 최대 HP에서의 회복처럼 HP 변화가 0이면 숫자 표시를 생략합니다.

| 경과 시간 | 화면 연출 |
|---|---|
| 0초 | HP 바와 행동 예고 사이의 몸통 위쪽에서 전장 중앙 방향으로 살짝 비켜 숫자를 띄웁니다. 피해는 붉은빛 `−숫자`, 회복은 밝은 녹색 `+숫자`이며 어두운 외곽선을 적용합니다. |
| 0~0.10초 | 피해 숫자는 짧게 커졌다가 원래 크기로 돌아옵니다. 회복 숫자는 부드럽게 나타납니다. |
| 0.10~0.60초 | 숫자가 위로 28 Canvas 단위 이동하며, 마지막 약 0.20초 동안 서서히 사라집니다. |

간격이 있는 연타는 타격별로 표시하고, 앞 숫자가 남아 있으면 다음 숫자의 위치를 옆으로 조금 옮깁니다. 같은 프레임에 같은 대상에게 들어온 같은 종류의 변화는 합산합니다. [구현과 확인 목록](../ToDo_List.md#전투-체력-변화-숫자-연출)

2026-11-25 Unity 6000.0.85f2의 체력 숫자 대상 재검사 2/2가 통과했습니다. 실제 피해·회복량과 HP 복원 시 표시 생략, 사망 이벤트 순서, 양쪽 캐릭터의 숫자 배치와 같은 프레임 합산·다음 프레임 분리를 확인했습니다. 결과는 로컬 `.utmp/health-change/health-final.xml`에 있습니다. [체력 숫자 검사](../Assets/Tests/Editor/SharedHudArtworkTests.cs#L196)

## 기존 UI와 호환

- 기존 CharacterInfoUI 오브젝트는 참조와 씬 오버라이드를 보존하기 위해 비활성 상태로 남습니다.
- 루트의 기존 Image는 투명하게 두고, 표시용 Visual의 Raycast Target은 끕니다.
- 새 적은 자신의 행동 UI를 사용합니다. presentation이 없는 기존 적은 BattleManager의 씬 행동 UI를 사용합니다.
- 에셋 재생성 메뉴는 `ProjectDN > Setup Character Presentation`입니다. 기존 GUID·스프라이트 ID와 사용자가 수정한 프로필 값을 유지합니다.

검증용 테스트는 `CharacterPresentationTests`, `CharacterPresentationSceneTests`에 있습니다. 원본 그림, 카드 배경과 카드·적 행동의 밸런스 데이터는 변경하지 않습니다.
