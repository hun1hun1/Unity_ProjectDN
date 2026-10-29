# 꿈·악몽 카드 컨테이너 v5 — 내부 바탕

v4의 비어 있던 안쪽을 꿈은 짙은 남색, 악몽은 어두운 버건디색으로 채웠습니다. 카드 그림과 글자가 먼저 보이도록 중앙 무늬는 절제했습니다. 아래쪽 가로 테두리는 없으며, 하단 바탕이 점차 투명해져 카드 진입 방향이 열려 보이도록 제작했습니다.

## 파일

- dream-container-1728x540.png / nightmare-container-1728x540.png: 적용용 고해상도 PNG.
- 각 864x270.png 및 432x135.png: 축소 소재.
- 각 container-original.png: 생성 원본.
- filled-panels-preview.png: 어두운 배경 위의 꿈·악몽 비교.
- padding-layout-example.png: 안쪽 여백을 둔 카드 배치 도식. 실제 Unity 화면은 아닙니다.
- dream-prompt.txt / nightmare-prompt.txt: 실제 편집 프롬프트.
- asset-info.json: 치수, 중앙 바탕 및 하단 투명도 검사 결과.

## 적용

꿈 이미지는 LeftCardArea, 악몽 이미지는 RightCardArea Image의 Source Image에 연결합니다. 기존 v4와 같은 16:5 규격이므로 영역 크기는 유지할 수 있습니다.

Sprite Single, Mesh Full Rect, Alpha Is Transparency 켜기, Compression None, Max Size 2048을 기준으로 가져옵니다. Image는 Simple, Color 흰색 Alpha 1, Preserve Aspect 끄기, Raycast Target 끄기를 사용합니다.

DreamCardContainer 및 NightmareCardContainer의 Horizontal Layout Group Padding은 이전 안내와 같은 Left 16, Right 16, Top 40, Bottom 4가 배치 예시의 기준입니다. 배경을 카드 정렬 컨테이너의 자식으로 추가하거나 Mask를 추가하지 않습니다.

## 제작 및 검증

기본 제공 image_gen으로 내부 바탕을 추가했습니다. 악몽의 하단은 같은 도구로 한 번 더 보정해 아래쪽 경계를 흐리게 처리했습니다. 안쪽 바탕과 안개를 별도 코드로 그리거나 덧칠하지 않았습니다.

내보내기는 바깥 투명 여백을 추가하지 않는 v4 방식과 같습니다. 양옆 막대와 중앙 달의 비율을 유지하면서 가로 레일 길이를 조정해 1728x540을 채우고, 이 PNG에서 축소 소재를 내보냈습니다.

모든 크기에서 중앙은 알파 251~252/255로 거의 불투명하며, 바닥 중앙은 알파 0~1/255로 거의 투명함을 확인했습니다. 생성 편집 특성상 일부 금속 질감과 세부 위치는 v4와 차이가 있을 수 있습니다.

기존 버전은 보존했습니다. 게임 코드, Assets, 씬 및 프리팹은 변경하지 않았고 Unity 실행 검증은 수행하지 않았습니다.
