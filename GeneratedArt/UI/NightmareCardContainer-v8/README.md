# 악몽 카드 컨테이너 v8 — 하단 모서리 밀도 보강

꿈 v7과 비교해 악몽 v7의 좌하단과 우하단이 더 비어 보이던 부분을 보강했습니다. 두 모서리에 버건디색 바탕과 붉은 기운을 더 채우고, 양옆 막대가 하단까지 이어지는 구성을 유지했습니다. 아래쪽을 연결하는 가로 테두리는 없습니다.

꿈 컨테이너는 변경하지 않았습니다. 비교 미리보기의 위쪽은 기존 꿈 v7, 아래쪽은 새 악몽 v8입니다.

## 파일

- nightmare-container-1728x540.png: 적용용 고해상도 PNG.
- nightmare-container-864x270.png / nightmare-container-432x135.png: 축소 소재.
- nightmare-container-original.png: 최종 생성 원본.
- lower-corners-comparison-preview.png: 꿈 v7과 악몽 v8 비교.
- prompt.txt, refine-prompt.txt, opaque-prompt.txt, cleanup-prompt.txt: 실제 생성과 보정에 사용한 프롬프트.
- asset-info.json: 치수, 내보내기 정보와 하단 모서리 알파 비교.

## 적용

기존 RightCardArea Image의 Source Image에 새 악몽 PNG를 연결합니다. 기존과 같은 16:5 캔버스이므로 영역 크기는 유지할 수 있습니다.

Sprite Single, Mesh Full Rect, Compression None, Max Size 2048을 기준으로 가져옵니다. Image Simple, Color 흰색 Alpha 1, Preserve Aspect 끄기, Raycast Target 끄기를 사용합니다.

## 제작 및 검증

기본 제공 image_gen으로 악몽 v7을 편집하고 꿈 v7의 하단 채움 정도를 참조했습니다. 모서리 바탕이 계속 비치던 부분은 불투명 바탕으로 보강한 뒤, 마지막 편집에서 상단 프레임 바깥 여백의 투명도를 다시 정리했습니다. 일부 금속 질감과 붉은 기운의 세부 표현에도 차이가 있습니다.

크기별 내보내기는 v7과 같은 방식으로 양옆 막대와 중앙 달 부분을 유지하면서 가로 부분 길이를 맞췄습니다. 상단 바깥 여백 13픽셀만 원본에서 재단했고 하단은 재단하지 않았습니다. 생성된 그림이나 알파를 코드로 덧칠하지 않았습니다.

이미지 하단 15% 높이, 좌우 각각 바깥 8% 너비 구간의 평균 알파를 측정했습니다. 1728x540 기준 값은 다음과 같습니다.

| 이미지 | 좌하단 평균 알파 /255 | 우하단 평균 알파 /255 |
|---|---:|---:|
| 꿈 v7 | 219.5 | 224.3 |
| 이전 악몽 v7 | 111.0 | 162.0 |
| 수정 악몽 v8 | 214.3 | 218.4 |

막대 끝의 부드러운 투명 부분까지 포함한 값이며, 완전히 불투명한 직사각형으로 만든 것은 아닙니다. 최종 합성 미리보기와 크기별 출력 치수를 확인했습니다. Unity 실행 검증은 수행하지 않았습니다.

기존 버전과 Assets, .meta, 씬, 프리팹, C# 코드는 변경하지 않았습니다. 새 결과는 GeneratedArt에만 저장했습니다.
