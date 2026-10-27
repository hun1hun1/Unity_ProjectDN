# 탄창·탄약 UI 소재 v1

현재 탄약 UI의 세로 형태에 맞춘 빈 탄창 배경과, 5.56×45mm NATO 탄약을 연상시키는 단일 탄약 소재입니다. 탄약은 황동색 탄피, 잘록한 어깨와 목, 오른쪽을 향하는 뾰족한 구리색 탄두로 구분했습니다. 전체는 기존 UI와 어울리는 낡은 철제·손으로 그린 금속 질감입니다.

## 파일

| 파일 | 크기 | 용도 |
|---|---|---|
| magazine-background-280x1080.png | 280×1080 | 빈 탄창 고해상도 배경 |
| magazine-background-140x540.png | 140×540 | 축소 배경 |
| magazine-background-70x270.png | 70×270 | 기본 비율 배경 |
| cartridge-556-1000x170.png | 1000×170 | 단일 탄약 고해상도 소재 |
| cartridge-556-200x34.png | 200×34 | 단일 탄약 소재 |
| cartridge-556-100x17.png | 100×17 | 축소 단일 탄약 |
| ammo-cell-176x90.png | 176×90 | 한 발과 투명 간격을 포함한 반복 셀 |
| ammo-fill-10-176x900.png | 176×900 | 10발 기본 장탄 수용 Fill 소재 |
| ammo-fill-10-88x450.png | 88×450 | 축소 Fill 소재 |
| ammo-magazine-preview.png | 1240×1190 | 0/10·1/10·5/10·10/10 조합 미리보기 |
| magazine-original.png / cartridge-original.png | 생성 원본 크기 | 각각의 생성 원본 |
| magazine-prompt.txt / cartridge-prompt.txt | — | 실제 생성 프롬프트 |
| asset-info.json | — | 크기, 배치, Fill 검증 결과 |

탄창 바깥과 단일 탄약 바깥은 투명합니다. 탄창의 중앙은 탄약을 올릴 수 있는 짙은 바탕이며, 탄약 Fill 소재에는 탄창 배경이 포함되지 않습니다. 미리보기 위 숫자는 확인용이고 소재에 들어 있지 않습니다.

## 배치 기준

현재 BattleScene AmmoBarBackground는 기준 해상도 1920×1080에서 약 67.2×259.2입니다. 소재를 70:270, 즉 7:27 비율로 내보내 기존 비율에 맞췄습니다.

280×1080 배경 기준 Fill 배치 영역은 왼쪽 52, 위쪽 96, 너비 176, 높이 900입니다. 내부 탄약은 한 셀마다 한 발씩, 90픽셀 간격으로 배치되어 있습니다. 0부터 10까지 정수 탄약 수에서 Vertical/Bottom Fill 경계가 탄약을 자르지 않고 정확히 같은 수의 탄약을 표시하는 것을 이미지 수준에서 확인했습니다.

Unity에 적용할 때의 기준 설정은 다음과 같습니다. 이번 작업에서는 실제 씬과 코드를 변경하지 않았습니다.

- 탄창: Sprite (2D and UI), Single, Image Type Simple, Color 흰색·알파 1.
- Fill: Source Image에 ammo-fill-10-176x900.png, Image Type Filled, Fill Method Vertical, Fill Origin Bottom, Color 흰색·알파 1.
- Fill RectTransform: Anchor Min (0.1857143, 0.0777778), Anchor Max (0.8142857, 0.9111111), 네 방향 Offset 0.
- Fill Amount는 current / 10입니다. 현재 AmmoUI 코드의 current / maximum과 같은 결과가 나오는 조건은 maximum이 10일 때입니다.
- 최대 장탄 수가 10 이외의 값으로 바뀌면 단일 탄약 소재를 사용해 해당 개수로 셀 또는 Fill 이미지를 재구성해야 한 발당 한 개를 유지할 수 있습니다. 현재 게임의 최대 탄약 증가 기능과 밸런스는 변경하지 않았습니다.

원본은 기본 제공 image_gen으로 각각 생성했습니다. 생성 후 투명 여백 추출·크기 조정·반복 배치를 통해 Fill 소재를 만들었습니다. 형태 참고: [GGG 공식 5.56×45mm NATO 제품 페이지](https://www.ggg-ammo.lt/en/military-ammunition/ggg-5-56-45-mm-nato-ball-cartridges-en).

이는 이미지 조합 미리보기와 이미지 검증 결과이며, 실제 Unity 적용 또는 런타임 검증 결과는 아닙니다.
