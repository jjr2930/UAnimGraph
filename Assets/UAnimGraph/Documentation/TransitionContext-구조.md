# TransitionContext

TransitionContext는 다음 8개의 public readonly 필드로 구성한다.

| 필드 | 타입 | 의미 |
|---|---|---|
| GraphOwner | GameObject | 그래프를 실행하는 오브젝트. UAnimGraphPlayer는 자신의 gameObject를 전달한다. 직접 만든 UAnimInstance는 graphOwner를 지정할 수 있으며 생략하면 Animator의 gameObject를 사용한다. |
| GraphInstance | UAnimInstance | 전이를 평가하는 현재 인스턴스 |
| SharedBlackboard | SharedBlackboard | 해당 인스턴스의 공통 파라미터 접근 객체 |
| From | State | 출발 상태의 정의·ID·이름·시간을 제공하는 값 스냅샷 |
| TransitionStartNormalizedTimeAtFromState | float | 시작 시 출발 상태의 정규화 시간. 반복 클립은 현재 사이클의 0 이상 1 미만 값, 비반복은 0~1 범위 |
| TransitionStartTimeAtFromState | float | 시작 시 출발 상태의 누적 재생 시간(초). State Speed가 적용된 시간 |
| To | State | 목적지 상태의 스냅샷. 진입 시각은 0초 |
| TransitionDuration | float | 설정된 블렌딩 지속시간(초) |

평가 함수는 전이가 시작될 경우 사용할 후보 컨텍스트를 받는다. 함수가 true를 반환하면 그 값을 확정해 Machine.ActiveTransitionContext에 저장하며, 블렌딩 중 시작 시간은 바뀌지 않는다. 전이가 끝나거나 머신이 Reset되면 ActiveTransitionContext는 null이 된다. 0초 전이는 즉시 완료한다.

블렌딩 시간은 전이가 결정된 시점에 0부터 시작한다. 전이가 시작되기 전 프레임의 deltaTime을 이미 경과한 블렌딩 시간으로 더하지 않는다.

State는 런타임 정의(UAnimState)와 시간을 담는 readonly struct다. From.Name, From.Id, From.Time, From.NormalizedTime, From.TotalNormalizedTime 및 From.Definition에 접근할 수 있다. 누적 사이클이 필요한 조건은 From.TotalNormalizedTime을 사용한다.

SharedBlackboard는 GetFloat, GetInt, GetBool, GetTrigger, GetVector2와 SetFloat, SetInt, SetBool, SetTrigger, ResetTrigger, SetVector2를 제공한다. 키에는 ID 또는 이름을 사용할 수 있다. 평가 중 읽기는 프레임 시작의 공통 스냅샷을 사용하고, 쓰기는 기존 UAnimInstance 파라미터 규칙을 따른다. 다른 캐릭터의 인스턴스와 실행 값은 공유하지 않는다.

이전 호환 프로퍼티·메서드는 제거했다. 모든 전이 함수는 위 8개 필드 및 From/To 상태 정보와 SharedBlackboard를 직접 사용한다.

```csharp
[TransitionEvaluation]
public static bool CanRun(in TransitionContext context)
{
    return context.SharedBlackboard.GetFloat("Speed") > 0.1f;
}
```

검증: 컨텍스트 23개 검사, 기존 전체 기능 41개 검사, 전이 UI 검사 통과. 정상 상태에서 전이 평가 1,000회 관리 힙 할당 0바이트. 로그: Logs/TransitionContext.log.
