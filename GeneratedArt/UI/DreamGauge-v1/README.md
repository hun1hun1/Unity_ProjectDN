# 드림 게이지 v1 — 분리형 UI 소재

빈 남색 트랙 Background, 푸른 꿈빛 Fill, 초승달·철제 테두리·빈 숫자판을 포함한 Frame을 각각 분리했습니다. 숫자는 이미지에 포함하지 않으며 기존 GaugeText를 사용합니다.

## 적용할 파일

| 용도 | 고해상도 파일 | 기준 표시 크기 |
|---|---|---|
| Frame | dream-gauge-frame-384x1080.png | 96x270 |
| Background | dream-gauge-background-176x824.png | 44x206 |
| Fill | dream-gauge-fill-176x824.png | 44x206 |

Frame은 외곽과 안쪽 창이 투명하고, 하단 숫자판은 어두운 남색입니다. Background와 Fill은 안쪽 창 전용의 불투명 직사각형입니다. Background와 Fill을 전체 96x270 영역으로 늘리지 않습니다.

각각 1/2, 1/4 크기의 PNG도 포함했습니다. frame-original.png, background-original.png, fill-original.png는 생성 원본입니다. 프롬프트 3개와 asset-info.json에 생성·배치 정보를 기록했습니다.

## 구조

```text
DreamGauge                    CostGaugeUI, 기존 영역 96x270
├─ Background                 빈 트랙 Image
│  └─ Fill                    채움 Image
├─ Frame                      테두리 Image
└─ GaugeText                  TMP 수치
```

위 순서로 Background와 Fill 뒤에 Frame을 그리고, 마지막에 숫자를 표시합니다. 프레임의 실제 창에 맞춰 배치값을 정했으므로 최초 디자인의 대략적인 창 크기 대신 아래 값을 사용합니다.

## Rect Transform

모든 Pivot은 (0.5,0.5), Scale은 (1,1,1)을 기준으로 합니다.

### Frame

- 부모: DreamGauge.
- Anchor Min (0,0), Max (1,1).
- Left, Right, Top, Bottom 모두 0.

### Background

- 부모: DreamGauge.
- Anchor Min (0.2708333, 0.1537037).
- Anchor Max (0.7291667, 0.9166667).
- Left, Right, Top, Bottom 모두 0.
- 384x1080 프레임 기준 창 배치: Left 104, Top 90, Width 176, Height 824.
- 96x270 표시 기준: Left 26, Top 22.5, Width 44, Height 206.

### Fill

- 부모: Background.
- Anchor Min (0,0), Max (1,1).
- Left, Right, Top, Bottom 모두 0.
- 별도의 여백을 추가하지 않습니다.

### GaugeText

- 부모: DreamGauge.
- Anchor Min (0.1354167, 0.0370370).
- Anchor Max (0.8645833, 0.1111111).
- Left, Right, Top, Bottom 모두 0.
- Noto Sans KR Bold SDF, Font Size 20, Color #F2ECD8, 가로·세로 중앙, Auto Size 끄기를 미리보기 기준으로 사용했습니다.

## Image 및 임포트

- 임포트: Sprite (2D and UI), Single, Mesh Full Rect, Compression None, Filter Mode Bilinear, Max Size 2048.
- Frame은 Alpha Is Transparency를 켭니다.
- Frame과 Background의 Image Type은 Simple, Preserve Aspect는 끕니다.
- Fill은 Image Type Filled, Fill Method Vertical, Fill Origin Bottom, Preserve Aspect 끄기를 사용합니다.
- 세 Image의 Color는 흰색 Alpha 1, Raycast Target은 끕니다.
- CostGaugeUI의 Gauge Fill에는 자식 Fill Image, Gauge Text에는 GaugeText를 연결합니다. 기존 amount / maxAmount 계산을 사용하며 게임 규칙이나 최대치는 변경할 필요가 없습니다.

## 미리보기 및 검증

dream-gauge-preview.png는 0/10, 1/10, 5/10, 10/10의 합성 예시입니다. dream-gauge-parts-preview.png는 분리된 소재를 보여줍니다. 미리보기의 숫자와 어두운 바탕은 적용용 PNG에 포함되어 있지 않습니다.

Frame의 외곽과 창 중앙 알파가 0임을 확인하고, 크기별 출력 치수와 숫자판의 텍스트 배치를 검사했습니다. 채움 예시는 Unity Vertical Bottom과 같은 아래쪽 잘라내기 방식으로 합성해 시각적으로 확인했습니다. 이는 실제 Unity 실행 검증 결과가 아닙니다.

기본 제공 image_gen으로 Frame, Background, Fill을 각각 제작했습니다. 크기 조정, 배치 치수 측정 및 미리보기 합성만 별도로 처리했습니다. 이미지의 창이나 안개를 코드로 다시 그리지 않았습니다.

소재는 GeneratedArt에만 저장했습니다. Assets, 씬, 프리팹, C# 코드에는 적용하지 않았습니다.
