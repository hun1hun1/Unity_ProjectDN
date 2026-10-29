# 꿈·악몽 카드 컨테이너 v6 — 왼쪽 막대와 내부 바탕 하단 연장

현재 씬에 연결된 v5 소재를 기준으로 왼쪽 세로 막대의 사라지는 끝부분을 이미지 최하단까지 이어지게 수정했습니다. 연장된 영역의 안쪽도 꿈은 짙은 남색, 악몽은 버건디색으로 채웠습니다. 아래쪽을 연결하는 가로 막대나 금속 테두리는 추가하지 않았습니다.

오른쪽 막대의 기존 짧아지는 구성을 유지하도록 편집했습니다. 생성 편집 특성상 일부 표면 질감과 장식의 위치에는 차이가 있을 수 있습니다.

## 파일

- dream-container-1728x540.png / nightmare-container-1728x540.png: 적용용 고해상도 PNG.
- 각 864x270.png 및 432x135.png: 축소 소재.
- 각 container-original.png: 최종 생성 원본.
- bottom-extended-preview.png: 점선 영역 경계 위의 꿈·악몽 비교.
- padding-layout-example.png: 회색 예시 카드를 올린 배치 도식. 실제 Unity 실행 화면은 아닙니다.
- dream-prompt.txt / nightmare-prompt.txt: 실제 편집 및 추가 보정 프롬프트.
- asset-info.json: 치수와 하단 바탕 알파 검사 결과.

## 적용

전체 캔버스는 기존과 같은 16:5입니다. 꿈은 LeftCardArea, 악몽은 RightCardArea Image의 Source Image를 해당 PNG로 교체합니다. Sprite Single, Mesh Full Rect, Compression None, Max Size 2048을 기준으로 가져오고, Image는 Simple, 흰색 Alpha 1, Preserve Aspect 끄기, Raycast Target 끄기를 사용합니다.

현재 저장된 BattleScene에서 CardZone과 그 안의 LeftCardArea 및 RightCardArea의 아래쪽 앵커가 0이므로 영역이 화면 아래에 닿는 구조입니다. 각 영역의 기존 Rect Transform을 유지하면 이미지의 마지막 행이 영역 최하단에 대응합니다. 별도 아래 여백을 추가하거나 이미지 비율 보존으로 여백을 만들지 않습니다.

Background의 하단 바탕은 이제 거의 불투명하게 이어집니다. 왼쪽 막대 주변 기운의 투명한 부분과 모서리의 투명 영역은 유지됩니다.

## 제작 및 검증

기본 제공 image_gen으로 두 이미지를 편집했습니다. 꿈 버전의 하단 안쪽 바탕은 같은 도구로 한 번 더 보정해 불투명도를 높였습니다. 이후 내보내기에서는 기존처럼 양옆 막대와 중앙 달 부분의 비율을 유지하며 가로 레일 길이를 맞춰 1728x540을 채웠습니다. 별도 바깥 여백은 추가하지 않았습니다.

최종 PNG의 맨 아래 행 중 가운데 70% 구간에서 알파 최솟값은 꿈 247/255 이상, 악몽 249/255 이상으로 확인했습니다. 각 크기에서 하단 바탕이 비어 있지 않음을 검사하고, 막대 끝이 하단까지 이어지는 모습을 미리보기에서 확인했습니다. 이는 이미지 수준 검사이며 Unity 실행 검증은 아닙니다.

현재 씬에 연결된 Assets/Images/UI/CardContainer의 두 파일이 기존 GeneratedArt/UI/CardContainers-v5 출력과 동일함을 SHA256으로 확인했습니다. 해당 Assets 파일과 .meta, 씬 및 프리팹은 변경하지 않았습니다. 새 결과는 GeneratedArt에만 저장했으며 이전 버전을 보존했습니다.
