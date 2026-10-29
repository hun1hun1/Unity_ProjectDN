# 악몽 게이지 v1 — 분리형 UI 소재

드림 게이지와 같은 Background → Fill, Frame, GaugeText 구조에 사용하는 악몽 소재입니다. 어두운 버건디색 빈 트랙, 붉은 꿈빛 Fill, 붉은 보름달과 철제 테두리를 각각 분리했습니다. 숫자는 적용용 이미지에 포함하지 않습니다.

## 파일

| 용도 | 고해상도 파일 | 기준 표시 크기 |
|---|---|---|
| Frame | nightmare-gauge-frame-384x1080.png | 96x270 |
| Background | nightmare-gauge-background-176x824.png | 44x206 |
| Fill | nightmare-gauge-fill-176x824.png | 44x206 |

각각 1/2, 1/4 크기 PNG와 생성 원본, 실제 프롬프트도 포함했습니다. Frame의 외곽과 안쪽 창은 투명합니다. Background와 Fill은 창에 넣는 불투명 직사각형이므로 전체 96x270 영역으로 늘리지 않습니다.

## 계층

```text
NightmareGauge                기존 CostGaugeUI
├─ Background                 빈 트랙 Image
│  └─ Fill                    채움 Image
├─ Frame                      보름달·테두리·빈 숫자판
└─ GaugeText                  TMP 수치
```

## Rect Transform

모든 Pivot (0.5,0.5), Scale (1,1,1), 각 Rect Transform의 Left/Right/Top/Bottom은 모두 0을 기준으로 합니다.

| 대상 | 부모 | Anchor Min | Anchor Max |
|---|---|---|---|
| Frame | NightmareGauge | (0,0) | (1,1) |
| Background | NightmareGauge | (0.2708333, 0.1537037) | (0.7291667, 0.9166667) |
| Fill | Background | (0,0) | (1,1) |
| GaugeText | NightmareGauge | (0.1354167, 0.0518519) | (0.8645833, 0.1259259) |

전체 영역과 Background/Fill의 배치는 드림 게이지와 같습니다. 생성된 악몽 숫자판 위치에 맞춰 GaugeText는 드림 소재보다 4 논리 픽셀 위로 조정했습니다. 생성 편집 특성상 프레임 세부 윤곽과 재질이 픽셀 단위로 같지는 않습니다.

Background 및 Fill의 기준 배치는 384x1080에서 Left 104, Top 90, Width 176, Height 824입니다. GaugeText는 Left 52, Top 944, Width 280, Height 80입니다.

GaugeText 미리보기는 Noto Sans KR Bold, Font Size 20, 아이보리 #F2ECD8, 가로·세로 중앙, Auto Size 끄기를 사용했습니다.

## Image 및 임포트

- Sprite (2D and UI), Single, Mesh Full Rect, Filter Mode Bilinear, Compression None, Max Size 2048.
- Frame은 Alpha Is Transparency를 켭니다.
- Background와 Frame은 Image Simple, Preserve Aspect 끄기.
- Fill은 Image Filled, Fill Method Vertical, Fill Origin Bottom, Preserve Aspect 끄기.
- 세 Image의 Color는 흰색 Alpha 1, Raycast Target 끄기.
- CostGaugeUI의 Gauge Fill에 Fill Image, Gauge Text에 GaugeText를 연결합니다. 기존 amount / maxAmount 계산을 사용합니다.

현재 Soldier.prefab의 maxNightmareGauge가 20임을 확인하고 미리보기는 0/20, 2/20, 10/20, 20/20으로 구성했습니다. 최대치는 이미지에 새겨져 있지 않으며 게임 규칙을 변경하지 않았습니다.

## 제작 및 검증

기본 제공 image_gen으로 드림 소재를 참조해 세 이미지를 각각 편집했습니다. 크기별 리사이즈와 미리보기 합성만 별도로 처리했습니다.

nightmare-gauge-preview.png는 0%, 10%, 50%, 100% 합성 예시이며, nightmare-gauge-parts-preview.png는 세 소재를 분리해 보여줍니다. 미리보기의 숫자와 바탕은 적용용 PNG에 포함되지 않습니다.

프레임 외곽 및 창 중앙 알파 0, 출력 치수, 숫자판 텍스트 배치와 합성 결과를 확인했습니다. Unity 실행 화면의 검증은 수행하지 않았습니다. Assets, 씬, 프리팹, C# 코드는 변경하지 않았습니다.
