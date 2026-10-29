# 턴 종료 버튼 v1

군용 장비를 닮은 낡은 철 테두리, 얇은 황동 장식, 네 개의 리벳과 어두운 올리브색 중앙판으로 구성한 기본 상태 버튼입니다. 외곽은 투명합니다. 중앙은 글자를 배치할 수 있도록 장식을 줄였습니다.

| 파일 | 용도 |
|---|---|
| turn-end-button-1008x518.png | 고해상도 UI 소재 |
| turn-end-button-672x346.png | 중간 크기 |
| turn-end-button-336x173.png | 현재 버튼 크기에 가까운 소재 |
| turn-end-button-original.png | 생성 원본 |
| turn-end-button-preview.png | 소재와 텍스트 배치 예시 |
| prompt.txt | 최초 생성 및 비율 보정 프롬프트 |
| asset-info.json | 크기, 알파 검사 및 표시 정보 |

소재에는 글자가 포함되어 있지 않습니다. 미리보기의 '턴 종료'는 Noto Sans KR Bold 32, 아이보리 #F2ECD8을 별도로 합성한 예시입니다.

## Unity 적용 기준

- Texture Type: Sprite (2D and UI), Sprite Mode: Single, Mesh Type: Full Rect.
- Alpha Is Transparency 켜기, Compression None, Filter Mode Bilinear.
- TurnEnd Image의 Source Image에 위 PNG를 지정하고 Color를 흰색, Alpha 1로 설정합니다.
- Image Type은 Simple을 기준으로 제작했습니다. 9-slice Border는 지정하지 않았습니다.
- 현재 씬의 TurnEnd는 기준 해상도 1920x1080에서 약 336x172.8 크기입니다. 기존 배치와 크기를 유지할 수 있습니다.
- 일반 상태 소재 한 장입니다. 상호작용 상태는 Button의 Color Tint로 표현할 수 있습니다: Normal #FFFFFF, Highlighted #FFF3D8, Pressed #B5B8A6, Disabled #7E8178 (모두 Alpha 1).
- Color Tint의 Highlighted는 표면을 약간 따뜻하게 물들이는 방식입니다. 별도의 눌림 형태나 안쪽 그림자 이미지가 포함된 것은 아닙니다.

기본 제공 image_gen으로 제작하고, 비율 보정도 같은 도구로 진행했습니다. PNG 축소 및 미리보기 합성만 별도로 처리했습니다. 생성 원본의 알파는 그대로 보존했으며 중앙은 약 98~99% 불투명합니다.

프로젝트의 코드, 씬, 프리팹 및 Assets 폴더에는 적용하지 않았습니다. Unity 실행 화면에서의 검증은 수행하지 않았습니다.
