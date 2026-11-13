# UAnimGraph 사용 안내

대상 버전: Unity **7000.0.0a9**. Graph Toolkit으로 저작하고 Animation Playables로 실행합니다.

## 바로 실행하기

1. **UAnimGraph → Create Complete Example**으로 함수 전이·Mixer2D·상체 레이어 예제를 생성합니다.
2. **UAnimGraph → Open Complete Example Scene**을 열고 Play를 누릅니다.
3. **Window → UAnimGraph → Preview and Debug**에서 Live Debug Target을 선택해 상태·가중치·파라미터를 확인합니다.

기존 1D 예제는 **Create Root Layer Example / Open Root Layer Example Scene** 메뉴로 엽니다. 기존 예제나 사용자의 장면을 새 예제로 덮어쓰지 않습니다.

## 루트 → Layer → State

- **Assets → Create → UAnimGraph → UAnimAsset**: 기본 Layer와 Output Pose를 생성합니다.
- 루트에서 `Layer.Pose → 다음 Layer.PreviousPose → Output Pose`로 연결합니다.
- 첫 Layer는 입력 없이 Weight 1 / Override / 마스크 없음으로 사용합니다.
- Layer에는 State Machine을 지정하거나 **Create State Machine**으로 생성합니다.
- 각 Layer 노드에서 이름, AvatarMask, Override/Additive, Weight, Enabled를 편집합니다.
- Layer를 더블클릭하거나 Open 버튼으로 내부를 엽니다.
- 새 상태 머신에는 Entry와 State가 있습니다. Entry의 출력 연결이 시작 State를 정합니다.
- State의 **Create State Pose Graph** 버튼으로 내부 일반 그래프를 생성합니다. Animation Player에 클립을 지정하고 Output Pose에 연결합니다.
- 중첩 StateMachine은 지원 범위 밖입니다. State 안에서는 Pose를 조합합니다.

그래프 상단의 **Integrated Editor**로 같은 창에 탐색·선택 항목 설정·프리뷰 패널을 표시합니다. 다시 누르면 원래 Graph Toolkit 배치로 돌아갑니다. 작은 창에서는 그래프 탭을 최대화해 사용합니다. 별도 **Authoring Workspace**도 제공합니다.

## 전이 평가 함수

프로그래머가 플레이어에서 사용할 수 있는 런타임 `.cs` 파일에 작성합니다.

```csharp
using UAnimGraph;

[TransitionLibrary]
public static class CharacterTransitions
{
    [TransitionEvaluation]
    public static bool CanMove(in TransitionContext ctx)
    {
        return ctx.SharedBlackboard.GetFloat("speed") > 0.1f;
    }
}
```

- `public static class`와 `public static bool Method(in TransitionContext ctx)` 형식을 사용합니다.
- 해당 클래스·메소드에 두 어트리뷰트가 모두 필요합니다. 제네릭·오버로드·인스턴스 메소드는 선택할 수 없습니다.
- 전이 Inspector의 **Evaluation Function** 조건에서 함수를 검색·선택합니다. 선택 목록에는 클래스명·메소드명만 표시합니다.
- 코드 원문은 `.cs`에 남고 그래프에는 함수 식별 정보만 저장됩니다. **Open source**로 원본을 엽니다.
- 함수는 파라미터 스냅샷을 읽고 bool을 반환합니다. 파라미터 변경·State 변경·프레임별 할당은 피합니다.
- 컨텍스트는 `GraphOwner`, `GraphInstance`, `SharedBlackboard`, `From`, `TransitionStartNormalizedTimeAtFromState`, `TransitionStartTimeAtFromState`, `To`, `TransitionDuration` 필드로 구성됩니다. 파라미터는 `ctx.SharedBlackboard.GetFloat(...)` 등으로 조회하고, 상태 이름과 누적 시간은 `ctx.From.Name`, `ctx.From.TotalNormalizedTime`으로 읽습니다.
- 파라미터 ID 사용을 권장합니다. 이름으로도 조회할 수 있지만 이름을 코드에 사용했다면 이름 변경 때 코드를 함께 수정해야 합니다.
- 전이 시간·우선순위는 별도 설정입니다. 코드 조건과 필드 조건·별도 Exit Time을 혼합하면 오류입니다.
- 새 상태 머신의 전이는 함수 선택이 필수입니다. 기존 에셋의 Legacy 필드 조건은 호환용으로 유지합니다.

### 실행 규칙

- 전이 중에는 새 전이를 평가하지 않습니다.
- 큰 우선순위가 먼저이며, 동률이면 안정적인 전이 ID 순입니다.
- Exit Time은 함수에서 누적 정규화 시간을 비교합니다. 예: `ctx.From.TotalNormalizedTime >= 0.8`.
- 명시적인 Self Transition은 새 재생 인스턴스를 시간 0에서 시작해 기존 Pose와 블렌드합니다.
- 모든 Layer가 같은 trigger 스냅샷을 읽고, 평가 tick 종료 시 사용 여부와 관계없이 해제합니다. 블렌드 도중 들어온 trigger를 자동 보관하지 않습니다.
- 함수 예외는 해당 평가를 false로 처리하고 진단합니다. 다른 후보는 계속 평가합니다.

## Mixer1D / Mixer2D Inspector

노드의 **Mixer Inspector** 버튼으로 열고 Parameter Root를 지정합니다.

- Mixer1D: 그래프 Value 포트, float 파라미터 또는 상수. 입력별 임계값을 편집합니다. 인접 두 샘플을 선형 보간하며 범위 밖은 끝점으로 고정합니다.
- Mixer2D: Vector2 파라미터, float X/Y 파라미터 또는 상수. 입력별 X/Y를 표 또는 2D 공간에서 편집합니다. 방향·속도 기반 gradient-band 보간을 사용합니다.
- 입력은 클립 슬롯이 아니라 Pose 포트입니다. 다른 Mixer 결과도 연결할 수 있습니다.
- 입력을 추가하면 새 포트와 행을 생성합니다. 임계값순 보기는 연결을 변경하지 않습니다.
- 행을 선택하면 대응 샘플이 강조됩니다. 연결 노드 버튼으로 원본 그래프를 열고 노드를 표시합니다.
- 드래그로 샘플 위치를 변경하고 Shift로 0.1 단위 스냅합니다. 확대·축소, 중간 버튼 팬, Fit samples와 Origin을 제공합니다.
- **Test input** 모드에서는 샘플 대신 임시 입력을 조절합니다. 프리뷰 인스턴스에만 적용하며 에셋이나 게임 캐릭터의 파라미터를 변경하지 않습니다.
- 입력 가중치는 읽기 전용입니다. 라이브 디버그 대상 또는 프리뷰의 현재 입력도 표시합니다.
- State 위상 동기화와 독립 재생을 선택할 수 있습니다. 같은 Pose가 여러 경로에서 쓰이면 실행 인스턴스를 분리해 시간 충돌을 피합니다.
- 활성 Mixer에서는 가중치 0인 입력도 시간을 진행하고, 비활성 State는 시간을 멈춥니다.
- 중복 임계값·좌표, 미연결 포트, 잘못된 파라미터와 비유한 설정 값은 오류로 표시합니다. 편집은 Undo/Redo를 지원합니다.

## 프리뷰와 실행

프리뷰는 별도 Scene의 복제 모델을 사용합니다. Preview Model을 지정하고 Start Preview를 누르세요. 재생·일시정지·반복·시간 이동을 제공합니다. 시간 이동은 현재 파라미터로 처음부터 재평가하는 방식이며 과거 입력 이벤트를 재생하는 기능은 아닙니다.

실제 캐릭터에는 Animator와 UAnimGraphPlayer를 추가하고 루트 Asset을 지정합니다. Animator Controller는 비워 둡니다. API는 `SetFloat`, `SetInt`, `SetBool`, `SetTrigger`, `ResetTrigger`, `SetVector2`입니다. 한 Animator는 한 Player가 소유합니다.

저장 또는 노드 의존성이 바뀌면 새 정의를 임포트합니다. 실행 중 정의가 바뀌면 재시작 안내를 표시합니다. 루트 Inspector의 Validate/Compile과 빌드 전 검증으로 누락 참조·순환·타입 불일치를 검사합니다.

## 검증 메뉴

- **UAnimGraph → Verify Complete Feature Set**: 함수 조건·Mixer·저장·Undo·기존 기능 검사
- **UAnimGraph → Verify Root Layer Chain**: Layer 연결·실제 Animator 출력 검사
- **UAnimGraph → Run Verification**: 기존 기본 회귀 검사

결과는 프로젝트 `Logs/UAnimGraph-*-verification.txt`에 기록됩니다. 빌드 전 평가 함수 라이브러리 보존용 `Assets/UAnimGraph/Generated/link.xml`을 갱신하여 플레이어 스트리핑 후에도 선택 함수를 호출할 수 있게 합니다.

## 루트 내부 저장
Layer와 State의 생성 버튼은 이제 저작 원본과 실행용 Machine/Pose를 루트 내부에 저장합니다. 네이티브 편집 캐시는 Assets/Editor/UAnimGraphCache에 재생성하며 버전 관리에서 제외합니다. 기존 그래프의 내부화 메뉴와 검증 결과는 서브에셋-저장-검증.md를 참고하세요.
