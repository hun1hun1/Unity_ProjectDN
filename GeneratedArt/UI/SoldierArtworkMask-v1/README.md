# 군인 아트워크용 Mask 소재 v1

기존 SoldierArtworkFrame-v1의 투명한 안쪽 창을 기준으로 만든 기능용 마스크입니다. 흰색 불투명 영역에서만 자식 아트워크가 표시되며, 프레임 바깥과 깎인 모서리는 투명합니다.

정확한 프레임 윤곽이 필요하므로 AI로 별도의 모양을 그리지 않고 기존 프레임의 알파 데이터에서 창 영역을 직접 추출했습니다. 원래 프레임과 아트워크 파일은 변경하지 않았습니다.

## 파일

| 파일 | 용도 |
|---|---|
| soldier-artwork-mask-900x520.png | 권장 적용용 고해상도 마스크 |
| soldier-artwork-mask-360x208.png | 같은 크기 프레임에 대응하는 축소 마스크 |
| soldier-artwork-mask-180x104.png | 기본 표시 크기 마스크 |
| soldier-artwork-mask-preview.png | 마스크 및 적용 전후 모서리 비교 |
| asset-info.json | 추출 방식, 영역 치수와 검증 정보 |

각 크기는 같은 크기의 프레임 PNG에서 각각 추출했습니다. 가능하면 Frame과 Mask 모두 900x520 파일을 사용하고 Unity에서 180x104로 표시합니다. 이미지 전체 크기와 좌표가 맞아야 하므로 Sprite Editor에서 따로 Tight Crop하지 않습니다.

## Unity 구조와 배치

```text
ArtworkArea                        기존 아트워크 영역 180x104
├─ ArtworkMask                     Image + Mask, 전체 영역 Stretch
│  └─ Artwork                      기존 그림, 174x101 중앙
└─ ArtworkFrame                    기존 철제 프레임, 전체 영역 Stretch
```

1. ArtworkMask를 만들고 Image의 Source Image에 soldier-artwork-mask-900x520을 연결합니다.
2. Image는 Simple, Color 흰색 Alpha 1, Preserve Aspect 끄기, Raycast Target 끄기로 설정합니다.
3. Mask 컴포넌트를 추가하고 Show Mask Graphic을 끕니다. 마스크를 그리는 Image 컴포넌트 자체는 활성 상태로 유지합니다.
4. 기존 Artwork를 ArtworkMask의 자식으로 옮깁니다. Artwork의 Maskable은 켜고, Preserve Aspect도 켭니다.
5. ArtworkMask는 ArtworkArea에 Anchor Min (0,0), Max (1,1), 네 방향 Offset 0으로 배치합니다.
6. Artwork는 부모 중심에 Anchor Min/Max (0.5,0.5), Pivot (0.5,0.5), Pos X/Y 0, Width 174, Height 101로 배치합니다. 기존 900x520 아트워크 파일은 그대로 사용합니다.
7. ArtworkFrame은 ArtworkMask의 형제로 두고, 전체 영역 Stretch와 Offset 0을 적용합니다. Hierarchy에서 Mask보다 뒤에 배치해 마지막에 그립니다.
8. 기존 CardUI와 DeckCardUI의 artwork 참조는 실제 그림 Image에 유지합니다.

별도의 어두운 아트워크 바탕 Image를 사용한다면 그것도 ArtworkMask 아래에 두어야 함께 잘립니다. ArtworkArea 같은 마스크 바깥 부모의 사각형 Image는 자식 Mask의 영향을 받지 않습니다. 이전 프레임 미리보기에서 모서리 바깥으로 보인 부분에는 전체 영역에 깔린 어두운 사각형 바탕도 포함되어 있었습니다.

이 마스크는 RectMask2D가 아닌 일반 Mask용입니다. 중앙이 투명한 철제 프레임 PNG를 Mask의 Source Image로 사용하지 않습니다.

## 임포트

- Texture Type: Sprite (2D and UI).
- Sprite Mode: Single.
- Mesh Type: Full Rect.
- Alpha Source: Input Texture Alpha.
- Alpha Is Transparency: 켜기.
- Compression: None.
- Filter Mode: Bilinear.
- Generate Mip Maps: 끄기.
- Max Size: 1024 이상.

## 제작과 검증

프레임 중앙에서 시작해 알파 8/255 미만인 연결 영역만 찾고, 그 영역을 흰색 Alpha 255, 나머지를 Alpha 0으로 출력했습니다. 모든 크기에서 안쪽 창이 외부 가장자리와 연결되지 않음을 확인했습니다.

900x520 기준 마스크의 창 경계는 Left 15, Top 8, Width 870, Height 504이며, 모서리에는 프레임에 맞는 잘린 부분이 포함됩니다. 반투명 회색 얼룩을 넣지 않아 실제 마스크 경계를 명확하게 유지했습니다.

기존 미리보기와 같은 그림 및 사각형 바탕을 프레임에 합성했을 때 프레임 바깥에 남던 982픽셀이, 마스크 적용 뒤 0픽셀이 되는 것을 이미지 데이터로 확인했습니다. 비교는 프레임 자체가 거의 투명한 바깥 영역을 기준으로 했습니다. 적용 전후 확대 이미지는 동일한 그림과 동일한 프레임을 사용합니다.

이는 PNG 합성 검증이며 Unity 실행 결과는 아닙니다. Assets, 씬, 프리팹, 카드 데이터, C# 코드는 변경하지 않았습니다.
