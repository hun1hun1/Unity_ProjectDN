# 꿈·악몽 카드 컨테이너 v4 — 영역 경계 기준

이전 버전은 16:5 PNG 안에 실제 철제 프레임이 작게 들어 있어 카드 뒤에 가려졌습니다. 이번 버전은 RightCardArea 및 LeftCardArea의 외곽에 철제 테두리가 붙도록 제작했습니다. 아래쪽을 연결하는 막대는 계속 없으며, 중앙과 아래쪽 입구는 투명합니다.

## 파일

- dream-container-1728x540.png / nightmare-container-1728x540.png: 고해상도 적용 소재.
- 각각의 864x270.png: 현재 영역 기준 크기.
- 각각의 432x135.png: 축소 소재.
- 각각의 container-original.png: 생성 원본. 적용에는 규격화된 크기별 PNG를 사용합니다.
- edge-fit-preview.png: 점선으로 표시한 영역 경계와 테두리의 위치 비교.
- padding-layout-example.png: 카드 배치에 안쪽 여백을 둔 도식. 회색 사각형은 예시이며 Unity 실행 화면이나 실제 카드 스크린샷이 아닙니다.
- dream-prompt.txt / nightmare-prompt.txt: 실제 생성 프롬프트.
- asset-info.json: 치수 및 알파 검사, 프레임 위치 측정 결과.

## Unity 적용

1. 꿈 PNG는 LeftCardArea, 악몽 PNG는 RightCardArea의 기존 Image에 연결합니다.
2. Import는 Sprite (2D and UI), Single, Mesh Full Rect, Alpha Is Transparency 켜기, Compression None, Max Size 2048을 사용합니다.
3. Image는 Simple, Color 흰색 Alpha 1, Preserve Aspect 끄기, Raycast Target 끄기로 설정합니다. 기존 Rect Transform은 유지하고 Set Native Size는 누르지 않습니다.
4. DreamCardContainer와 NightmareCardContainer의 Horizontal Layout Group Padding을 Left 16, Right 16, Top 40, Bottom 4로 설정하면 테두리와 달 장식을 위한 공간을 확보할 수 있습니다. 현재 기본 Padding은 좌우 0, 위아래 4이므로 이미지 교체만으로는 카드가 가장자리 테두리를 일부 가릴 수 있습니다.
5. CardHandController는 위아래 Padding을 사용해 카드 크기를 계산하므로 위의 설정은 카드 표시 높이에도 반영됩니다. 기준 해상도 1920x1080에서 컨테이너 높이 270, 세로 여백 합계 44, 카드 표시 높이 약 226을 기준으로 한 예시입니다.
6. 배경은 카드 정렬 컨테이너의 자식으로 추가하지 않습니다. Mask 또는 RectMask2D도 추가하지 않습니다.

## 제작 및 검증

기본 제공 image_gen으로 테두리가 캔버스 가장자리에 붙는 형태를 생성했습니다. 꿈 버전은 파란 기운이 과하게 밝아지지 않도록 같은 도구로 색감을 한 번 보정했습니다.

기존처럼 투명 캔버스 안에 원본을 작게 맞추는 방식은 사용하지 않았습니다. 높이를 맞춘 뒤 양옆 막대와 중앙 달 부분의 비율은 유지하고, 위쪽 가로 부분의 길이를 조정해 1728x540을 채웠습니다. 안개나 금속을 별도로 그려 넣지는 않았습니다. 축소 소재도 이 기준 PNG에서 내보냈습니다.

1728 소재에서 좌우 금속 막대 중심 간 폭은 전체 폭의 약 96.6~96.8%이며 나머지는 막대 두께와 모서리에 해당합니다. 위쪽 수평 금속 테두리의 밝은 픽셀 위치는 이미지 위에서 약 6~8픽셀입니다. 이전의 안쪽 외곽 여백이 제거되었음을 이미지 수준에서 확인했습니다. 모든 크기에서 중앙 및 바닥 중앙의 알파 0을 확인했습니다.

게임 코드, Assets, 씬, 프리팹은 수정하지 않았습니다. Unity 실행 및 실제 해상도 변경 검증은 수행하지 않았습니다. 기존 이미지 버전은 보존했습니다.
