# BattleScene NightmareCost HUD v1

DreamCost HUD와 짝을 이루는 원형 철제 악몽 자원 계기판입니다. 상단의 꿈 초승달을 붉은 보름달로 바꾸고, 테두리는 붉은색 장식, 중앙은 짙고 채도가 낮은 버건디색으로 구성했습니다. 현재/최대 코스트 텍스트를 별도로 올리는 소재입니다.

| 파일 | 크기 | 용도 |
|---|---|---|
| nightmare-cost-hud-1152x972.png | 1152×972 | 6배 고해상도 소재 |
| nightmare-cost-hud-384x324.png | 384×324 | 2배 소재 |
| nightmare-cost-hud-192x162.png | 192×162 | 기준 해상도에서의 기본 패널 크기 |
| nightmare-cost-hud-original.png | 생성 원본 크기 | 원본 이미지 |
| nightmare-cost-hud-readability-preview.png | 1216×1036 | 빈 패널, 3/3, 0/3, 10/10 미리보기 |
| prompt.txt | — | 실제 생성 프롬프트 |
| asset-info.json | — | 치수, 색상, 투명도 및 미리보기 설정 |

소재 PNG에는 숫자와 글자가 없으며 바깥은 투명하고 중앙은 색으로 채워져 있습니다. DreamCost와 같은 192×162 캔버스, 약 150×150 본체 영역으로 내보냈습니다. 이는 BattleScene의 기준 해상도 1920×1080을 기준으로 한 크기이며 실제 화면 비율에 따라 패널 표시 크기가 달라질 수 있습니다.

가독성 미리보기는 Noto Sans KR Bold 원본 OTF, 논리 크기 36, 아이보리색 #F2ECD8, Image 알파 1을 기준으로 만든 이미지 조합입니다. 미리보기 숫자는 소재에 포함되지 않으며 Unity 실행 화면은 아닙니다. 왼쪽 위 빈 패널, 오른쪽 위 3/3, 왼쪽 아래 0/3, 오른쪽 아래 10/10입니다.

기본 제공 image_gen으로 DreamCost HUD 생성 원본을 참조하여 제작했습니다. 생성 후 투명 여백을 추출하고 같은 규격으로 내보냈습니다. 소재는 밝은 아이보리색 글자용으로 제작했으며 BattleScene과 Assets 내 파일은 변경하지 않았습니다.
