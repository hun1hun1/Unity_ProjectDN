# 악몽 카드 컨테이너 v2

v1보다 왼쪽과 오른쪽의 세로 철제 막대를 길게 늘린 버전입니다. 아래쪽을 연결하는 막대는 없으며, 끝부분은 안개로 사라지고 카드가 들어오는 아래쪽과 중앙은 투명하게 열려 있습니다.

- nightmare-card-container-1728x540.png: 고해상도 소재.
- nightmare-card-container-864x270.png: 기준 표시 크기.
- nightmare-card-container-432x135.png: 축소 소재.
- nightmare-card-container-original.png: 이미지 생성 도구의 편집 원본.
- nightmare-card-container-preview.png: 어두운 배경과 체크무늬 위의 표시 예시.
- prompt.txt: 실제 편집 프롬프트.
- asset-info.json: 크기와 투명도 검사 결과.

최종 캔버스는 기존과 같은 16:5입니다. 기존 v1 소재 대신 같은 Image의 Source Image에 연결할 수 있습니다. Sprite Single, Mesh Full Rect, Image Simple, Color 흰색 Alpha 1, Raycast Target 끄기를 기준으로 합니다.

기본 제공 image_gen으로 v1을 편집했습니다. 크기별 내보내기와 미리보기 합성은 별도 처리했으며, 생성 원본의 비율과 알파를 보존했습니다. 생성 편집으로 장식과 안개 일부에도 차이가 있을 수 있습니다.

기존 v1 파일은 보존했습니다. Unity의 씬, 프리팹, 코드 및 Assets 폴더는 변경하지 않았으며, Unity 실행 검증은 수행하지 않았습니다.
