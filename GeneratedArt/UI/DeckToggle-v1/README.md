# 덱 토글 버튼 이미지 v1

기존 꿈·악몽 뽑을 카드 더미를 참조해 만든 투명 배경 버튼 소재입니다. 한 더미의 가장 윗장에 꿈의 파란 초승달과 악몽의 붉은 보름달을 좌우 절반씩 배치했습니다.

- `deck-toggle-512x512.png`: 고해상도 소재
- `deck-toggle-108x108.png`: 현재 BattleScene의 `Canvas/TopArea/Decks` 크기에 맞는 소재
- `deck-toggle-original.png`: 생성 원본
- `prompt.txt`: 기본 제공 image_gen 도구에 사용한 실제 편집 프롬프트
- `asset-info.json`: 크기·투명도·가시 영역 검사 결과

참고 이미지는 `Assets/Images/UI/CardPiles/dream-draw-pile-512x512.png`와 `nightmare-draw-pile-512x512.png`입니다. 꿈 카드 더미를 편집 대상으로, 악몽 카드 더미를 색·문양 참고로 사용했습니다. 원본의 사각형 캔버스 전체를 Lanczos 방식으로 축소해 투명도와 가장자리 여백을 보존했습니다.

Unity용 파일은 `Assets/Images/UI/DeckToggle/`에 있습니다. Sprite (2D and UI), Single, Full Rect, Mipmap 끄기, Bilinear, Compression None으로 설정했습니다. 씬과 버튼 동작은 수정하지 않았습니다.

적용할 때는 `Canvas/TopArea/Decks`의 Image → Source Image에 소재를 지정하고, Color를 흰색·Alpha 1, Image Type을 Simple, Preserve Aspect를 켜면 됩니다. 버튼의 108×108 영역은 유지할 수 있습니다.
