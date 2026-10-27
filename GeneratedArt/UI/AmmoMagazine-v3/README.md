# 20발 고정 표시용 탄창 UI v3

v2의 촘촘한 탄약 배치를 유지하면서 수납부만 20발 길이로 늘린 버전입니다. 단일 탄약의 모양과 크기는 유지했습니다. 최대 장전 가능량이 10발인 경우에도 그림은 항상 20발을 기준으로 표시하며, 현재 탄약이 10발일 때 절반, 15발일 때 75%, 20발일 때 가득 차도록 구성했습니다.

## 파일

| 파일 | 크기 | 용도 |
|---|---|---|
| magazine-background-560x1360.png | 560×1360 | 20발용 빈 탄창 고해상도 |
| magazine-background-280x680.png | 280×680 | 탄창 배치 기준 |
| magazine-background-140x340.png / magazine-background-70x170.png | 파일명 기준 | 축소 소재 |
| magazine-panel-280x1080.png | 280×1080 | 현재 AmmoBarBackground와 같은 7:27 캔버스 비율에 탄창을 중앙 배치한 소재 |
| magazine-panel-140x540.png / magazine-panel-70x270.png | 파일명 기준 | 기존 패널 비율용 축소 소재 |
| ammo-fill-20-352x1040.png | 352×1040 | 고해상도 20발 Fill |
| ammo-fill-20-176x520.png | 176×520 | 배치 기준 20발 Fill |
| ammo-cell-176x26.png | 176×26 | 한 발 반복 셀 |
| cartridge-556-1000x170.png / cartridge-556-200x34.png | 파일명 기준 | 기존과 동일한 개별 탄약 |
| ammo-magazine-preview.png | 1240×790 | 0/10·10/10·15/15·20/20 표시 예시 |
| magazine-original.png | 생성 원본 크기 | 탄창 생성 원본 |
| magazine-prompt.txt / cartridge-prompt.txt | — | 생성 프롬프트 |
| asset-info.json | — | 치수, 배치, 0~20발 Fill 검증 결과 |

숫자와 미리보기 배경은 소재에 포함되지 않습니다. 탄창의 외곽과 탄약의 외곽은 투명하며, 탄창 안쪽은 짙은 바탕으로 채웠습니다. 미리보기의 분모는 실제 장전 가능량을 나타냅니다. 10/10이라도 전체 표시 공간은 20발분이므로 절반만 채워집니다.

## Fill 배치와 계산

Image Type Filled, Fill Method Vertical, Fill Origin Bottom, Color 흰색·알파 1을 기준으로 합니다. 채움 비율은 반드시 currentAmmo / 20f입니다. 현재 AmmoUI의 current / maximum 계산은 이 표시 규칙으로 별도로 바꿔야 합니다. 이번 작업에서는 게임 코드나 씬을 변경하지 않았습니다.

### 잘린 탄창 소재 280×680 기준

- Fill 영역: 왼쪽 52, 위쪽 84, 너비 176, 높이 520.
- Anchor Min (0.1857143, 0.1117647), Anchor Max (0.8142857, 0.8764706), Offset 네 방향 0.
- 탄창과 Fill의 공통 부모를 7:17 비율로 표시해야 합니다.

### 현재 패널 비율용 소재 280×1080 기준

- magazine-panel-280x1080.png는 위 탄창 주위에 투명 여백을 두어 현재 AmmoBarBackground와 같은 7:27 비율로 만든 이미지입니다.
- Fill 영역: 왼쪽 52, 위쪽 284, 너비 176, 높이 520.
- Anchor Min (0.1857143, 0.2555556), Anchor Max (0.8142857, 0.7370370), Offset 네 방향 0.
- 두 종류의 배경은 같은 탄창이며 투명 캔버스 크기만 다릅니다. 사용하는 배경에 맞는 Fill 배치 정보를 사용해야 합니다.

0~20의 각 정수 탄약 수에서 한 발 단위 Fill 경계가 탄약 중간을 자르지 않고 정확한 개수를 표시하는 것을 이미지 수준에서 확인했습니다. 이는 Unity 실행 검증 결과는 아닙니다.

20발용 탄창은 기본 제공 image_gen으로 v2를 참조해 제작했습니다. 단일 탄약은 기존 PNG를 그대로 재사용했습니다. 투명 여백 추출, 크기 조정, 일정 간격 반복 배치로 Fill 소재를 내보냈습니다. v1·v2 파일과 기존 프로젝트 파일은 유지했습니다.
