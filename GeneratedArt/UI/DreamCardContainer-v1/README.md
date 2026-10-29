# 꿈 카드 컨테이너 v1

아래쪽이 열린 역 U자 형태의 꿈 손패 영역 배경입니다. 낡은 철 테두리, 얇은 푸른 장식, 작은 황동 장식과 위쪽 중앙의 푸른 초승달을 사용했습니다. 양옆은 아래로 내려가며 푸른 안개로 사라집니다. 아래쪽을 연결하는 테두리는 없습니다.

| 파일 | 용도 |
|---|---|
| dream-card-container-1728x540.png | 고해상도 소재, 16:5 |
| dream-card-container-864x270.png | 현재 컨테이너 기준 크기 |
| dream-card-container-432x135.png | 축소 소재 |
| dream-card-container-original.png | 생성 원본 |
| dream-card-container-preview.png | 어두운 배경 및 체크무늬 위 투명도 예시 |
| prompt.txt | 생성 및 옆 테두리 길이 보정 프롬프트 |
| asset-info.json | 치수와 알파 검사 결과 |

## 표시와 투명도

소재에는 카드, 글자, 체크무늬 배경이 포함되어 있지 않습니다. 중앙은 투명하며, 안개는 위쪽과 양옆 가장자리에 집중되어 있습니다. 각 크기에서 바닥 가장자리 알파가 0임을 확인했습니다. 이는 이미지 검사이며 Unity 실행 검증은 아닙니다.

생성 원본의 비율을 유지해 투명한 16:5 캔버스에 맞췄습니다. 원본의 알파를 보존했으며, 바깥쪽 투명 여백도 최종 배치의 일부입니다.

## Unity 적용 기준

- Sprite (2D and UI), Single, Mesh Type Full Rect, Alpha Is Transparency 켜기.
- Filter Mode Bilinear, Compression None, Max Size 2048 이상.
- Image Type Simple, Color 흰색 Alpha 1, Raycast Target 끄기.
- 카드 뒤에 표시하는 별도의 배경 Image로 사용합니다.
- DreamCardContainer에는 카드 정렬용 Layout Group이 있으므로, 배경은 카드 정렬 대상에 포함하지 않습니다. 같은 부모 아래의 형제 배경으로 두고 카드 컨테이너보다 먼저 그려지도록 배치할 수 있습니다.
- 카드가 아래에서 들어오는 연출을 유지하도록 배경에 Mask나 RectMask2D를 추가하지 않습니다.
- 부모 영역은 16:5 비율을 기준으로 합니다. 현재 씬 앵커 계산상 기준 해상도 1920x1080에서 약 864x270 영역입니다.

기본 제공 image_gen으로 생성하고 옆 테두리 길이를 보정했습니다. 크기별 내보내기와 미리보기 합성만 별도로 처리했습니다. 게임 코드, 씬, 프리팹, Assets 폴더에는 적용하지 않았습니다.
