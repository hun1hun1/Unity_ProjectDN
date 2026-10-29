# 악몽 카드 컨테이너 v1

꿈 카드 컨테이너와 짝을 이루는 악몽 손패 영역 배경입니다. 아래쪽이 열린 철제 프레임과 황동 장식은 유지하고, 위쪽 중앙은 붉은 보름달, 테두리와 안개는 진홍색과 짙은 버건디색으로 표현했습니다.

| 파일 | 용도 |
|---|---|
| nightmare-card-container-1728x540.png | 고해상도 소재, 16:5 |
| nightmare-card-container-864x270.png | 기준 표시 크기 |
| nightmare-card-container-432x135.png | 축소 소재 |
| nightmare-card-container-original.png | 생성 원본 |
| nightmare-card-container-preview.png | 어두운 배경 및 체크무늬 위 투명도 예시 |
| prompt.txt | 실제 생성 프롬프트 |
| asset-info.json | 치수와 알파 검사 결과 |

중앙과 아래쪽 입구는 투명하며, 아래쪽을 연결하는 테두리는 없습니다. 소재에는 카드, 글자, 체크무늬가 포함되어 있지 않습니다. 꿈 컨테이너와 최종 캔버스 규격을 맞췄으며, 별도 생성된 변형이므로 세부 장식과 윤곽이 픽셀 단위로 동일하지는 않습니다.

생성 원본의 비율을 보존해 투명한 16:5 캔버스 안에 배치했습니다. 외곽 투명 여백을 포함한 전체 PNG가 배치 기준입니다.

## Unity 적용 기준

- Sprite (2D and UI), Single, Mesh Type Full Rect, Alpha Is Transparency 켜기.
- Filter Mode Bilinear, Compression None, Max Size 2048 이상.
- Image Type Simple, Color 흰색 Alpha 1, Raycast Target 끄기.
- 카드 뒤에 별도 배경 Image로 표시합니다. NightmareCardContainer의 카드 정렬용 Layout Group 대상에 포함하지 않습니다.
- 배경에 Mask나 RectMask2D를 추가하지 않습니다.

기본 제공 image_gen으로 꿈 컨테이너를 참조해 편집했으며, 악몽 코스트 HUD의 붉은 보름달 색감을 함께 참조했습니다. 원본 알파를 유지한 크기별 내보내기와 미리보기 합성만 별도로 처리했습니다.

바닥 가장자리와 중앙 투명도를 PNG 수준에서 확인했습니다. Unity 실행 검증이나 씬, 프리팹, 코드, Assets 폴더에 대한 적용은 수행하지 않았습니다.
