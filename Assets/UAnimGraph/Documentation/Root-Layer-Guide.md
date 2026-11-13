# 루트 Layer 그래프 사용법

대상 Unity: 7000.0.0a9. 이번 기능은 루트의 Layer 직렬 연결과 실행 예제를 추가합니다.

## 예제를 바로 열기

1. Unity 메뉴에서 **UAnimGraph → Create Root Layer Example**을 실행합니다.
2. `Assets/UAnimGraph/RootExample/CharacterLocomotion.uanimasset`이 만들어지고 열립니다.
3. 루트의 `Base Locomotion → Upper Aim → LayerOutput` 연결을 확인합니다. `LayerOutput`은 기획서의 Output Pose에 해당합니다.
4. Layer 노드의 **Open Layer / State Machine** 버튼으로 내부 상태 머신을 엽니다.
5. **UAnimGraph → Open Root Layer Example Scene**을 실행하고 Play를 누릅니다.
6. **Window → UAnimGraph → Preview and Debug**에서 실행 중인 캐릭터를 Live Debug Target으로 선택하고 Speed를 조절합니다.

예제는 추가 다운로드 없이 기존 데모의 큐브 캐릭터와 생성된 클립을 사용합니다. 사용자가 편집한 다른 장면을 자동 교체하지 않습니다. 장면을 여는 메뉴에서는 먼저 저장 여부를 확인합니다.

## 새 루트 만들기

**Assets → Create → UAnimGraph → UAnimAsset**으로 생성하면 기본 Layer 노드와 LayerOutput이 연결된 루트가 만들어집니다.

- 기본 Layer에는 입력 연결이 없습니다. Weight=1, Additive=false, AvatarMask=None으로 둡니다.
- Layer 노드의 Machine에 `.uamachine` 상태 머신을 지정합니다.
- 추가 Layer는 앞 Layer의 Pose를 자신의 PreviousPose에 연결합니다.
- 마지막 Layer의 Pose를 LayerOutput의 Pose에 연결합니다.
- 각 노드의 Name, Weight, Additive, AvatarMask, Enabled 옵션을 설정합니다.
- 기존 Pose 그래프를 재사용할 때만 Machine 대신 PoseGraph를 지정합니다. 둘을 동시에 지정할 수 없습니다.
- 연결 순서가 Layer 합성 순서입니다. 화면의 좌우 위치나 Inspector 배열 순서는 영향을 주지 않습니다.
- Layer의 분기·순환·출력과 끊어진 Layer는 오류로 처리합니다.

그래프 옵션은 선택한 노드의 Graph Toolkit Inspector에서 편집합니다. 루트 에셋 Inspector에는 Parameters와 Open Root Graph 버튼, 내부 Layer 열기와 Validate/Compile을 제공합니다. 컴파일된 Layer 배열은 루트 Inspector에서 직접 편집하지 않습니다.

## 예제 내용

| Layer | 내부 | 설정 |
| --- | --- | --- |
| Base Locomotion | Idle ↔ Move. Move는 Walk/Run Blend1D | Weight 1, Override, 전체 바디 |
| Upper Aim | Aim State 하나 | Weight 0.45, Override, 상체 AvatarMask |

Speed가 0이면 Idle, 0.65이면 Walk 70%/Run 30%, 1이면 Run입니다. 상체에는 Aim이 45% 합성됩니다. 애니메이션 재생 예제이며 캐릭터 위치 이동을 구현하는 예제는 아닙니다.

## 호환성과 이번 변경의 범위

기존 `.asset` 루트와 첫 Layer의 Pose를 직접 작성하던 구형 `.uanimasset`은 기존 경로로 처리합니다. 자동으로 새 형식으로 변환하거나 사용자 그래프를 덮어쓰지 않습니다. 새 루트는 Layer 노드 연결 방식을 사용합니다.

이번 변경은 루트 Layer 저작·컴파일·실행·예제 기능입니다. 평가 함수 어트리뷰트 선택 UI, Mixer2D, 기획서의 전체 편집 창 통합은 별도 구현 대상이며, 이 예제의 전이는 기존 ParameterCondition 실행을 사용합니다.

검증 메뉴: **UAnimGraph → Verify Root Layer Chain**. 결과 파일은 `Logs/UAnimGraph-root-chain-verification.txt`입니다.
