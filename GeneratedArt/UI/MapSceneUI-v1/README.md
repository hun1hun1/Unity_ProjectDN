# 1층 MapScene UI 리소스 v1

안개 낀 1층 맵 배경에 맞춰 은빛 테두리와 남청색 바탕을 사용했다. 카드는 중앙의 주 행동으로, 자원 배지와 경로 표시는 보조 정보로 쓰는 구성이다.

| 구분 | 파일 | 픽셀 크기 | 용도 |
|---|---|---:|---|
| 선택 카드 | [choice-card-frame.png](ChoiceCard/choice-card-frame.png) | 679×880 | 카드 바깥 프레임. 중앙 투명 |
| 선택 카드 | [choice-card-nameplate.png](ChoiceCard/choice-card-nameplate.png) | 748×152 | 카드 제목을 올리는 남청색 이름판 |
| 선택 카드 | [choice-combat-icon.png](ChoiceCard/choice-combat-icon.png) | 207×213 | 일반 전투를 나타내는 교차 검 |
| 자원 | [resource-badge.png](Resources/resource-badge.png) | 1298×274 | HP·골드가 공유하는 빈 배지 바탕 |
| 자원 | [hp-heart.png](Resources/hp-heart.png) | 300×279 | HP 하트 |
| 자원 | [gold-coin.png](Resources/gold-coin.png) | 287×290 | 골드 동전 |
| 진행 경로 | [node-completed.png](Route/node-completed.png) | 329×336 | 완료 노드. 녹색 바탕과 은빛 체크 |
| 진행 경로 | [node-current.png](Route/node-current.png) | 290×353 | 현재 노드. 밝은 마름모 |
| 진행 경로 | [node-pending.png](Route/node-pending.png) | 350×356 | 대기 노드. 속이 투명한 청회색 원 |
| 진행 경로 | [route-connector.png](Route/route-connector.png) | 469×36 | 노드 사이에 배치하는 가는 수평 연결선 |

마우스를 올렸을 때의 강조는 기본 프레임 스프라이트에 색을 적용하는 구성을 기준으로 했다. 카드 제목과 설명, HP·골드 수치, 노드 번호는 기존 UI 텍스트를 겹쳐 표시할 수 있도록 비워 두었다.

기본 이미지 생성 도구로 만든 원본 시트 3종과 [생성 프롬프트](Source/prompts.json)는 `Source`에 보관했다. 각 PNG는 시트에서 필요한 영역을 분리해 내보냈다. 카드 프레임과 대기 노드의 중앙 알파값은 0이다. 현재 단계는 이미지 리소스 제작과 확인이다.
