# 옵션 버튼 톱니바퀴 v1

기존 TopArea의 금속·황동 프레임과 어울리는 단일 톱니바퀴 아이콘입니다. 외곽과 중앙 구멍은 투명하며 글자·버튼 바탕·그림자는 포함하지 않았습니다.

- `options-gear-512x512.png`: 고해상도 UI 소재
- `options-gear-108x108.png`: TopArea 높이 108px 기준 UI 소재
- `options-gear-original.png`: 생성 원본
- `prompt.txt`: 기본 제공 image_gen 도구로 생성할 때 사용한 실제 프롬프트
- `asset-info.json`: 크기·모서리·중앙 구멍 투명도 검사 결과

전체 정사각형 캔버스를 Lanczos 방식으로 축소하고 원본 알파와 여백을 보존했습니다. Unity용 PNG와 Sprite 설정은 `Assets/Images/UI/OptionsButton/`에 저장했습니다. 씬이나 버튼 동작은 수정하지 않았습니다.

## Unity 적용

옵션 버튼의 Image → Source Image에 소재를 지정하고, Color는 흰색·Alpha 1, Image Type은 Simple, Preserve Aspect는 켜기로 설정합니다. Button의 Target Graphic은 해당 Image로 연결합니다. 버튼의 Raycast Target은 켜 둡니다.

저장된 BattleScene의 Settings 영역은 기준 해상도 1920×1080에서 약 105.6×108입니다. 정사각형 이미지의 비율을 유지해 이 영역 안에 맞추면 됩니다. PNG는 Sprite (2D and UI), Single, Full Rect, Bilinear, Mipmap 끄기, Compression None으로 설정했습니다.
