# Mixer1D 입력 포트 편집

노드에서 Count와 Threshold0~7 목록을 숨기고, 실제 GTK Pose 입력 포트마다 임계값과 X 버튼을 배치했다. 연결 소켓은 기존 GTK 포트 그대로 사용한다.

- Pose1·Pose2: 임계값을 편집할 수 있고 X 버튼은 비활성화된다.
- Add Input Pose: 새 Pose 입력 포트와 임계값 행을 추가한다.
- Pose3부터: X 버튼으로 해당 포트와 연결을 제거한다. 다른 포트의 연결과 임계값은 유지한다.
- 입력 추가·삭제·임계값 변경은 Undo/Redo와 저장·재열기를 지원한다. 상세 Mixer Inspector에도 같은 삭제 제한을 적용했다.
- 상세 Inspector는 노드의 우클릭 메뉴 **Mixer1D Inspector**, 또는 **Window → UAnimGraph → Mixer Inspector**에서 연다.

기존 Count/Threshold 설정은 동적 입력으로 처음 편집할 때 값을 보존하여 전환한다. 내부 포트 ID는 유지하고 화면의 이름만 Pose1부터 순서대로 표시한다. Value 입력과 Pose 출력은 기존 연결 방식을 유지한다.

## 검증

Unity 7000.0.0a9의 별도 검증 프로젝트에서 신규 20개 및 기존 통합 41개·기본 회귀 33개·루트 연결 검증을 통과했다.

첫 두 입력의 삭제 거부, 세 번째 입력 삭제, 기존 임계값·와이어 보존, 실제 연결된 포트의 Undo 복구와 Redo, 저장·재열기를 검사했다. 네이티브 GTK 포트 UI 트리에서 각 포트의 임계값 필드, X 버튼 상태, Add Input Pose 버튼과 구형 옵션 숨김을 검사했다. 배치 모드 검사이므로 최종 화면 픽셀 배치는 직접 확인하지 않았다.

로그: `Logs/MixerInputsFinal.log`, `Logs/UAnimGraph-mixer-input-verification.txt`.
