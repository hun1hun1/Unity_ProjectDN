# Soldier와 총알 카드 설정

## 캐릭터

`Assets/Prefabs/Player/Soldier.prefab`에 Soldier, CostSystem, DeckController, AmmoSystem이 포함되어 있습니다. 총알 UI는 캐릭터 프리팹에 포함하지 않고 BattleScene의 별도 HUD에 배치합니다. 기본 최대 총알은 10알, 전투 시작 총알은 0알입니다. AmmoSystem의 Max Ammo / Starting Ammo를 Inspector에서 변경할 수 있습니다. 총알은 턴 전환 시 유지되고, 캐릭터 또는 전투가 초기화될 때 시작값으로 설정됩니다.

실행 중 Inspector에서 최대량이나 현재량을 변경하면 다음 프레임에 총알 UI가 갱신됩니다. Starting Ammo를 변경해도 진행 중인 전투의 잔탄을 초기화하지는 않습니다.

현재 BattleScene은 Soldier로 플레이하도록 연결되어 있습니다. 기존 Player 오브젝트의 컴포넌트를 Soldier로 교체하여 BattleManager, 꿈/악몽 코스트·게이지 HUD, 덱 목록 버튼의 기존 참조를 유지했습니다. 총알 UI와 AmmoSystem이 포함되며 기본 최대량은 10알, 시작량은 0알입니다. 씬과 프리팹의 꿈/악몽 시작 덱은 기존 기본 덱을 사용하며 총알 예시 카드는 자동으로 추가하지 않습니다.

## 총알 HUD

총알 텍스트와 Fill 바는 `BattleArea/PlayerArea/PlayerSpecialAbility`에서 표시합니다. Soldier 프리팹과 씬의 Soldier 하위에는 총알 UI가 없습니다.

- `PlayerSpecialAbility`의 AmmoUI가 씬 Soldier의 AmmoSystem, 자식 AmmoText, AmmoBarBackground/Fill을 참조합니다.
- Fill은 Image의 Filled / Horizontal / Left 설정이며 `현재 총알 / 최종 최대 총알` 비율로 표시합니다. 최대량이 0이면 빈 상태입니다.
- 텍스트와 Fill은 장전, 소모, 카드·유물의 최대량 증가, 전투/새 게임 초기화 때 함께 갱신됩니다. HUD를 다시 활성화할 때도 현재 상태를 표시합니다.
- 이미지는 Unity 기본 UI 스프라이트를 사용합니다. 이후 Fill의 Image > Source Image에서 이미지를 교체할 수 있습니다.
- 런타임에 HUD가 참조하는 캐릭터를 바꿀 때는 `AmmoUI.Bind(newAmmoSystem)`을 호출합니다. 이전 총알 시스템의 이벤트 구독은 해제됩니다.

## 카드 사용 조건

CardData의 Ammo Requirement에서 설정합니다.

| Required Ammo | Consume Required Ammo | 동작 |
|---|---|---|
| 0 | 꺼짐 또는 켜짐 | 총알 사용 조건 없음. 기존 카드와 동일 |
| O (양수) | 꺼짐 | 사용 전 O알 이상 보유해야 함. 총알 미소모 |
| O (양수) | 켜짐 | 사용 전 O알 이상 보유해야 함. 카드 코스트 지불 성공 후 O알 소모 |

조건이 실패하면 코스트, 총알, 효과, 손패 및 버림 더미를 변경하지 않습니다. `BattleManager.CanUseCard(CardData)`는 자원을 변경하지 않는 사용 가능 판정입니다. 총알 기능이 없는 기본 Player는 Required Ammo가 양수인 카드를 사용할 수 없습니다.

## 장전 효과

Create > Card > Effect > Reload Ammo에서 ReloadAmmoEffect 에셋을 만듭니다. Amount를 설정하고 CardData의 Effects에 연결합니다. 최대치까지 장전하며 이미 가득 차 있으면 해당 장전 효과만 무효입니다. 기본 카드 코스트와 나머지 효과는 그대로 처리됩니다.

## 총알 소모 추가 효과

Create > Card > Effect > Spend Ammo Bonus에서 SpendAmmoBonusEffect 에셋을 만듭니다. Ammo Cost에 O를 지정하고 Bonus Effects에 기존 피해·방어·회복 등 효과를 연결합니다. 이 효과를 CardData의 기본 효과와 함께 Effects에 넣습니다.

O알 이상이면 한 번 O알을 소모하고 Bonus Effects 전체를 순서대로 실행합니다. O알 미만이면 부분 소모 없이 추가 효과만 생략합니다. 이 유형을 총알 부족 때도 사용할 수 있도록 하려면 CardData의 Required Ammo는 0으로 둡니다. 두 설정을 함께 사용하는 카드에서는 필수 소모와 추가 소모가 각각 적용됩니다.

장전과 추가 효과가 한 카드에 있으면 Effects 배열 순서가 적용됩니다. 필수 보유 조건은 사용 전에 검사하므로 카드 자신의 장전으로 충족할 수 없습니다. 필수 소모는 모든 카드 효과보다 먼저 진행됩니다.

## 예시 에셋

`Assets/CardData/Soldier/Examples/`에 Reload, AmmoBonus, RequireAmmo, ConsumeAmmo 카드와 해당 효과가 있습니다. 필요량/소모량 1알의 설정 예시이며 현재 기본 덱·보상 목록에는 포함하지 않았습니다. 공격 예시는 기존 기본 공격의 효과를 참조합니다.

`ProjectDN > Create Soldier Assets` 메뉴는 누락된 프리팹/예시 에셋을 생성합니다. 기존 에셋을 덮어쓰거나 BattleScene을 저장하지 않습니다.

## 최대 총알 증가

최종 최대량은 **Inspector 기본 최대량 + 이번 게임의 유물 보너스 + 이번 전투의 카드 보너스**입니다. 최대량 증가만으로 총알을 장전하지 않습니다. 잔탄이 그대로여도 UI의 최대량이 즉시 갱신됩니다.

### 이번 전투 동안 증가하는 카드

Create > Card > Effect > Increase Max Ammo에서 `IncreaseMaxAmmoEffect`를 생성하고 Amount를 설정합니다. 기존 `CardData.effects`에 연결하면 현재 플레이어의 최대량을 해당 수만큼 늘립니다. 여러 번 사용하면 누적되며 턴 전환 시 유지됩니다. 기존 장전 효과와 함께 사용하면 Effects 배열 순서가 적용됩니다.

`AmmoSystem.ResetForBattle()`은 전투 한정 보너스를 제거하고 잔탄을 Starting Ammo로 초기화하며 유물 보너스는 유지합니다. BattleManager의 Start에서 호출하므로 같은 Soldier를 다음 전투에서 재사용해도 카드 증가분이 넘어가지 않습니다. 기본 Player처럼 AmmoSystem이 없는 캐릭터에는 증가 효과가 적용되지 않습니다.

### 이번 게임 동안 증가하는 유물

Create > Relic > Ammo Capacity에서 `AmmoCapacityRelic`을 생성합니다. Relic Name, Description, Max Ammo Bonus를 Inspector에서 설정합니다. 유물 획득 처리에서는 아래 둘 중 하나를 호출합니다.

```csharp
bool acquired = relic.Acquire();
// 또는 RunRelicInventory.Acquire(relic);
```

다른 유물의 보너스는 누적되며 동일한 유물 에셋은 한 번만 획득됩니다. 중복/잘못된 획득은 false를 반환합니다. 보너스는 획득 시 기록되며 공유 ScriptableObject에 진행 상태를 쓰지 않습니다. 이미 있는 Soldier와 이후 생성되는 Soldier에 모두 반영되고 씬 변경에도 유지됩니다.

새 게임을 시작할 때 `RunRelicInventory.ResetForNewGame()`을 호출합니다. 유물 보유 상태와 보너스를 비우고 현재 Soldier의 전투 보너스와 잔탄도 초기화합니다. 비활성 Soldier도 다시 활성화할 때 새 게임 초기화를 적용합니다. Play Mode에 새로 진입하면 상태가 자동 초기화됩니다.

여기서 게임 내내 유지는 **이번 플레이 진행 중**을 의미합니다. 프로젝트에는 유물 획득 화면 및 게임 저장/불러오기 구조가 없으므로 획득 API를 제공하며, 앱 종료 후 유지되는 파일 저장은 포함하지 않습니다.

`Examples/IncreaseMaxAmmo.asset`은 전투 최대량 +1 카드, `Examples/AmmoCapacityRelic.asset`은 게임 최대량 +1 유물 예시입니다. 기본 덱과 BattleScene에는 자동 적용하지 않습니다.
