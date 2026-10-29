# 군인 카드 아트워크 프레임 v1

군인의 카드에 공통으로 사용할 수 있는 얇은 철제 아트워크 테두리입니다. 낡은 어두운 철과 은은한 황동색 안쪽 선, 짧게 깎은 모서리로 구성했습니다. 그림이 들어가는 중앙과 모서리 바깥은 투명합니다.

희귀도나 꿈·악몽 색을 포함하지 않아 이름 패널의 희귀도 표시 및 카드 앞면 색과 함께 사용할 수 있습니다. 캐릭터나 무기 장식, 글자는 넣지 않았습니다.

## 파일

| 파일 | 용도 |
|---|---|
| soldier-artwork-frame-900x520.png | 고해상도 적용용 소재 |
| soldier-artwork-frame-360x208.png | 2배 소재 |
| soldier-artwork-frame-180x104.png | 기본 표시 크기 |
| soldier-artwork-frame-original.png | 생성 원본 |
| soldier-artwork-frame-preview.png | 투명도 및 기존 공격 아트워크 합성 예시 |
| prompt.txt | 생성 및 비율 보정 프롬프트 |
| asset-info.json | 치수와 내보내기 정보 |

## 배치 기준

- 프레임 외곽: 기존 아트워크 영역과 같은 180x104.
- 그림 표시 영역: 약 174x101, 프레임 중앙, Preserve Aspect 켜기.
- 기존 900x520 아트워크 PNG는 그대로 사용하며, Artwork Rect Transform의 표시 크기만 조절합니다.
- ArtworkFrame은 그림보다 뒤에 그려지지 않도록 같은 부모 아래에서 Artwork보다 나중에 배치합니다.
- 프레임에는 그림이 합쳐져 있지 않습니다. 미리보기에만 기존 공격 이미지를 사용했습니다.
- 파일에 9-slice Border는 설정되어 있지 않으며, Image Simple을 기준으로 합니다.

예시 구조:

```text
ArtworkArea                  기존 아트워크 영역 180x104
├─ Artwork                   174x101, 중앙, 기존 카드 데이터 연결
└─ ArtworkFrame              전체 영역에 Stretch
```

## 임포트 기준

Sprite (2D and UI), Single, Mesh Full Rect, Alpha Is Transparency 켜기, Filter Mode Bilinear, Compression None, Max Size 1024 이상을 사용합니다.

ArtworkFrame Image는 Simple, 흰색 Alpha 1, Raycast Target 끄기, Preserve Aspect 끄기를 기준으로 합니다. Artwork는 Preserve Aspect를 켜서 기존 그림의 비율을 유지합니다.

## 제작 및 검증

기본 제공 image_gen으로 생성하고 같은 도구로 실제 프레임의 외곽 비율을 보정했습니다. 생성된 금속 질감과 알파를 유지한 채 내보내기에서 위·아래 가장자리 부분의 높이를 조절해 얇은 상하 테두리로 정리했습니다. 900x520 출력에서 위·아래 가장자리 구간은 각각 8픽셀이며, 기본 크기에서 약 1.6픽셀에 해당합니다.

모든 출력의 중앙 알파가 0이고 크기가 정확함을 검사했습니다. 기존 공격 그림을 약 174x101 비율의 영역에 놓고 프레임을 올린 합성 미리보기에서 큰 영역 가림 없이 표시되는 것을 확인했습니다. 미리보기 배경과 체크무늬는 적용용 PNG에 포함되어 있지 않습니다.

Unity 실제 카드 표시, 확대 보기 및 해상도 변경 검증은 수행하지 않았습니다. Assets, 씬, 프리팹, 카드 데이터 및 C# 코드는 변경하지 않았습니다.
