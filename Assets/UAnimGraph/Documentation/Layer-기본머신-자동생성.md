# Layer 기본 StateMachine 자동 생성

새 UAnimAsset의 Base Layer와 그래프에서 새로 추가한 Layer에 기본 StateMachine을 하나씩 자동 생성·연결한다. 머신에는 Entry와 초기 Pose State가 있으며 Entry가 해당 State에 연결되어 있다. 저작 원본과 실행용 서브에셋은 루트 내부에 저장된다.

자동 생성은 편집 창을 열지 않는다. Layer를 더블클릭하면 기존 방식으로 내부 머신을 연다. 실제 애니메이션은 Pose State에서 State Pose Graph를 만들고 클립을 연결한다.

같은 Layer를 다시 정의하거나 에셋을 재임포트해도 머신을 중복 생성하지 않는다. 이미 Machine/PoseGraph를 지정했다면 해당 연결을 유지한다. 초기화 전에 삭제한 Layer에는 고아 머신을 만들지 않는다. 생성 여부 메타데이터로 Undo 후 임의 재생성을 피한다.

신규 10개, 기존 내부 저장 14개 및 통합 41개·기본 회귀·루트 연결 검증을 통과했다. 로그: `Logs/DefaultMachine1.log`, `Logs/UAnimGraph-default-machine-verification.txt`.
