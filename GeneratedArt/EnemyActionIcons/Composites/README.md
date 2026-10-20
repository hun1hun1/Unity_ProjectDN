# 적 행동 복합 아이콘 원본

기존 검·방패·디버프 이미지를 참고해 built-in imagegen으로 제작한 투명 PNG입니다. 각 조합은 분리된 나열 대신 중앙에서 겹치는 하나의 표식으로 구성했습니다.

| 조합 | 원본 | 최종 프롬프트 |
|---|---|---|
| 공격 + 방어 | [attackBlock-v1.png](attackBlock-v1.png) | [prompt](attackBlock-v1-prompt.txt) |
| 공격 + 디버프 | [attackDebuff-v1.png](attackDebuff-v1.png) | [prompt](attackDebuff-v1-prompt.txt) |
| 방어 + 디버프 | [blockDebuff-v1.png](blockDebuff-v1.png) | [prompt](blockDebuff-v1-prompt.txt) |
| 공격 + 방어 + 디버프 | [attackBlockDebuff-v1.png](attackBlockDebuff-v1.png) | [prompt](attackBlockDebuff-v1-prompt.txt) |

검은 좌상단을 향합니다. 방패는 뒤, 검은 중간, 디버프 표식은 앞에 배치하며 각 구성 요소의 실루엣을 남겼습니다. 기존 단일 아이콘 3종은 상위 폴더의 `*-v1-256x256.png`를 그대로 사용합니다.

게임용 파일은 `Assets/Images/UI/EnemyActions`에 같은 이름의 PNG로 복사되며, 기존 `.meta`의 GUID를 유지합니다. 고해상도 원본도 Unity에서는 최대 256 해상도로 임포트합니다. 원본 재적용 메뉴는 `ProjectDN > Import Generated Enemy Action Icons`입니다.
