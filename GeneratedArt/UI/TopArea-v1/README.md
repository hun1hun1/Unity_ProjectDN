# TopArea 배경 v1

짙은 올리브빛 금속 바탕, 얇은 황동 테두리와 작은 리벳으로 구성한 불투명 상단 패널입니다. 글자, 아이콘, 버튼은 포함하지 않았습니다.

- `top-area-1920x108.png`: 현재 기준 해상도 1920×1080에서 TopArea의 실제 비율과 크기에 맞는 파일
- `top-area-3840x216.png`: 같은 비율의 2배 크기 출력본
- `top-area-original.png`: 생성 원본
- `top-area-cropped.png`: 하단 흰 여백을 제외한 패널 원본
- `prompt.txt`: 기본 제공 image_gen 도구로 생성·비율 보정할 때 사용한 실제 프롬프트
- `asset-info.json`: 크기, 여백 제외 영역, 불투명도 검사 결과

생성 원본의 하단 흰 여백만 제외하고 패널을 Lanczos 방식으로 리샘플링했습니다. 최종 두 PNG는 알파 채널이 없는 완전 불투명 RGB 이미지입니다. 큰 출력본은 생성 원본에서 확대되었습니다.

Unity에서 사용할 파일은 `Assets/Images/UI/TopArea/`에도 복사되어 있습니다. Sprite (2D and UI), Single, Full Rect, Mipmap 끄기, Bilinear, Compression None으로 설정했습니다. 이 작업에서 씬이나 버튼 배치는 변경하지 않았습니다.

## 적용 위치

`Assets/Scenes/BattleScene.unity`의 `Canvas/TopArea`에서 배경용 Image의 Source Image에 위 PNG를 지정합니다. Color는 흰색, Alpha는 1, Image Type은 Simple, Preserve Aspect는 끄고 TopArea 영역 전체에 맞춥니다. 현재 TopArea에는 Image가 없으므로 적용할 때 Image 컴포넌트를 추가해야 합니다. 배경 Image의 Raycast Target은 꺼 두고 버튼과 텍스트는 자식 UI로 배치합니다.

창 비율을 바꾸면서 양 끝 장식 비율을 유지하려면 Sprite Editor에서 Border를 지정하고 Sliced로 사용할 수 있습니다. 이 출력본에는 9-slice Border를 미리 지정하지 않았습니다.
