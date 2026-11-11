# 최대 총알 증가 구현 계획

- 기본 최대량 10, 시작량 0 및 기존 Soldier/기본 Player 구성을 유지합니다.
- 최대량은 기본값 + 이번 게임의 매개체 보너스 + 이번 전투의 카드 보너스입니다. 증가만으로 장전하지 않습니다.
- `AmmoSystem.IncreaseMaxForBattle` 및 `IncreaseMaxAmmoEffect`를 추가합니다. `ResetForBattle`과 BattleManager의 전투 시작 시 전투 보너스를 제거합니다.
- `AmmoCapacityRelic`은 Inspector에서 보너스를 설정하는 데이터입니다. `RunRelicInventory.Acquire`로 획득하며 같은 에셋의 중복 획득은 무효입니다. 다른 매개체는 누적됩니다.
- 매개체 상태는 씬/캐릭터 교체를 넘어 이번 게임 동안 유지됩니다. `RunRelicInventory.ResetForNewGame`은 새 게임 경계이며 매개체와 기존 전투 총알 상태를 초기화합니다. 파일 저장 기능은 이번 범위에 포함하지 않습니다.
- UI는 잔탄이 같아도 최대량 변경 시 갱신합니다. 음수 증가 및 정수 오버플로를 방어합니다.
- Unity 테스트를 먼저 추가하여 미구현을 확인하고, 소스와 예시 카드/매개체 에셋을 구현합니다. 기존 테스트와 새 상태 수명/UI/전투 시작 테스트를 실행하고 직렬화 참조 및 인코딩을 확인합니다.

## 완료 및 검증

- Unity 6000.0.77f1에서 새 기능 테스트 3개의 실패와 기존 41개 통과를 먼저 확인했습니다.
- 구현 및 테스트 수명 주기 보완 후 전체 60개가 통과했습니다. 이 중 5개는 Play Mode 진입을 포함하며 실제 카드 사용/턴 전환, Inspector UI 갱신, 매개체의 씬 재로드 유지, 재사용 Soldier의 전투 초기화, 비활성 캐릭터 동기화를 확인합니다.
- Unity에서 증가 카드/효과/매개체 예시 에셋을 생성했습니다. 기존 BattleScene, 시작 덱 및 Soldier 기본 최대량 10/시작량 0을 유지합니다.
- 로그: `Logs/ammo-capacity-assets.log`, `Logs/ammo-capacity-final.log`, `Logs/ammo-capacity-final.xml`.
