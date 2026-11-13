# State 기본 Pose 그래프 자동 생성

Pose State 생성 시 해당 State 전용 Pose 그래프를 자동 생성·연결한다. 루트 내부 StateMachine의 그래프는 루트 내부 저작 원본과 실행용 서브에셋으로 저장한다. 일반 외부 `.uamachine`의 State는 기존 외부 저작 경로를 사용한다.

기본 그래프는 Animation Player → State Output으로 연결되어 있다. 생성 시 창을 열지 않으며, 사용자는 **Open State Pose Graph**로 열어 클립을 지정한다. State의 Create State Pose Graph 버튼은 제거하고 Name 입력과 Open 버튼만 유지한다.

기존 연결을 보존하고, 같은 State의 초기화를 반복해도 그래프를 중복 생성하지 않는다. 초기화 전에 삭제한 State에는 고아 그래프를 만들지 않는다. State 이름과 ID, 연결은 저장·재임포트 후 유지된다.

신규 14개, 기본 Layer/Machine 10개, 내부 저장 14개, 전이 UI 12개와 기존 통합 41개·기본 회귀·루트 연결 검증을 통과했다. GTK 네이티브 State UI 트리에서 각 State의 버튼이 Open 하나뿐임을 검사했다. 최종 화면의 픽셀 배치는 직접 검사하지 않았다.

로그: `Logs/DefaultStateFinal.log`, `Logs/UAnimGraph-default-state-verification.txt`.
