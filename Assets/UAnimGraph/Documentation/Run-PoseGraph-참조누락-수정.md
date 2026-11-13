# Run Pose Graph 참조 누락 복구

원인: StateMachine이 하위 Pose Graph의 임포트 결과보다 먼저 읽히면 null 참조가 런타임 Machine과 루트 서브에셋에 남았다. 기존 코드는 로드에 성공한 Pose에만 의존 관계를 등록했고, 캐시 파일이 존재하는 경우 Run 참조만 누락된 상태는 복구 대상으로 감지하지 못했다.

수정:
- 임베디드 Pose 경로는 루트 에셋을 역으로 조회하지 않고 Machine 캐시의 형제 파일에서 구한다.
- Pose를 아직 로드할 수 없는 경우에도 소스와 임포트 결과 의존 관계를 등록한다.
- 캐시 임포트 후 소유 루트도 지연 처리 큐에서 확인한다.
- 실제로 내부 소스가 있는 State의 런타임 Pose 참조가 비어 있으면 Pose → Machine → Root 순서로 재읽는다.
- Play 진입 전에 남은 큐와 복구 작업을 완료한다.

기존 사용자 에셋의 노드·연결·ID·MovingSpeed/Direction 설정은 수정하지 않는다. 원본 소스가 없는 외부 그래프까지 임의로 생성하지 않는다.

검증: 참조 누락 재현·자동 복구·원본 해시 유지·캐시 전체 재생성·실제 Animator 평가 10개 검사, 기존 전체 기능 41개 검사 통과. TestScene 사본에서 Run 참조를 일부러 누락한 후 실제 Play Mode로 진입해 오류 없는 시작, Slider의 Idle↔Run 전이, 원래 재생 속도를 확인했다.

로그: Logs/MissingRunPoseFinal.log, Logs/MissingRunPosePlayFinal.log.
