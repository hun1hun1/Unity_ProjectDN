# 꿈·악몽 카드 컨테이너 v7 — 양쪽 막대 하단 연장

왼쪽과 오른쪽 세로 막대 모두 사라지는 기운까지 이미지 최하단에 이어지도록 맞춘 수정본입니다. v6에서 짧게 남아 있던 오른쪽 막대를 왼쪽과 같은 방향으로 연장했습니다. 안쪽의 짙은 남색/버건디색 바탕은 하단까지 채웠으며 아래를 연결하는 가로 테두리는 없습니다.

## 파일

- dream-container-1728x540.png / nightmare-container-1728x540.png: 적용용 PNG.
- 각 864x270.png 및 432x135.png: 축소 소재.
- 각 container-original.png: 최종 생성 원본.
- both-sides-extended-preview.png: 영역 경계 위에 표시한 비교 예시.
- padding-layout-example.png: 회색 카드의 배치 도식. Unity 실행 화면이 아닙니다.
- dream-prompt.txt / nightmare-prompt.txt: 양쪽 막대 길이 수정 프롬프트.
- dream-refine-prompt.txt / nightmare-refine-prompt.txt: 하단 안쪽 바탕의 불투명도 보정 프롬프트.
- asset-info.json: 크기, 알파 검사와 원본 재단 정보.

## 적용

전체 캔버스는 기존과 같은 16:5입니다. 꿈은 LeftCardArea, 악몽은 RightCardArea Image의 Source Image에 해당 PNG를 연결합니다. Sprite Single, Full Rect, Compression None, Max Size 2048로 가져오고 Image Simple, 흰색 Alpha 1, Preserve Aspect 끄기, Raycast Target 끄기를 사용합니다.

이미지의 마지막 행이 화면 하단에 대응하도록 기존 영역의 아래쪽 앵커와 오프셋을 유지합니다. 별도 아래 여백은 추가하지 않습니다. 안쪽 카드 여백은 기존 안내의 Left/Right 16, Top 40, Bottom 4를 배치 예시 기준으로 사용했습니다.

## 제작 및 검증

기본 제공 image_gen으로 두 이미지를 편집하고, 같은 도구로 하단 안쪽 바탕이 계속 채워지도록 한 번 더 보정했습니다. 생성 결과의 상단 투명 여백과 하단의 얇은 반투명 끝부분을 내보낼 때 소량 재단했습니다. 좌우 막대와 중앙 장식의 비율을 유지하며 가로 부분 길이를 맞춰 1728x540을 채웠고, 그 PNG에서 축소 소재를 만들었습니다. 안개나 내부 바탕을 코드로 덧칠하지 않았습니다.

최종 출력의 마지막 행 가운데 70% 구간은 모든 크기에서 최소 알파 245/255 이상이며, 중앙 바탕도 거의 불투명함을 확인했습니다. 양쪽 막대의 사라지는 부분이 이미지 하단까지 이어지는 모습은 합성 미리보기에서 확인했습니다. 금속 질감과 기운은 손그림 형태이므로 픽셀 단위의 좌우 복제 대칭은 아닙니다.

기존 버전은 보존했습니다. Assets, .meta, 씬, 프리팹, C# 코드에는 적용하지 않았으며 Unity 실행 검증도 수행하지 않았습니다.
