# 전이 함수 Inspector와 샘플

## 선택 동작 수정

이전의 StateMachine 전체 함수 목록은 자동 Inspector에서 제거했다. 이제 GTK 전이의 조건 행이 선택 Inspector에 붙거나 떼어지는 공개 ConditionView 콜백을 따라 선택을 추적한다. **StateMachine/State 선택 시에는 숨기며, 일반 전이 화살표를 선택했을 때 해당 전이만 표시한다.** GTK의 Inspector 오버레이도 열어 네이티브 조건 행에서 직접 함수를 고를 수 있게 했다.

함수가 아직 없는 전이에는 편집 전용 선택 행을 초기화한다. 이 행은 런타임 조건으로 컴파일되지 않아 기존 무조건 전이나 필드 조건의 실행 의미를 바꾸지 않는다. 함수 선택 시 선택 행은 실제 EvaluationFunctionCondition으로 교체된다. Entry 연결에는 함수 선택 행을 추가하지 않는다. 별도 Transition Conditions 창은 명시적으로 연 경우에만 전체 관리 도구로 유지한다.

신규 선택 연동 10개·기존 전이 UI 12개·통합 41개와 기본 회귀·루트 연결 검증을 통과했다. 로그: `Logs/SelectedTransition1.log`, `Logs/UAnimGraph-selected-transition-verification.txt`. 자동 검사는 선택 콜백과 표시 대상 판정·직렬화·컴파일 경로를 확인했으며, 사용자의 실제 화면에서 화살표를 마우스로 클릭하는 검사는 수행하지 않았다. 아래의 StateMachine 전체 Inspector 표시 설명은 이전 기록이다.

스크린샷의 Inspector는 GTK의 내부 GraphObject를 선택한 상태였다. 기존 선택 패널은 별도 창/툴바에만 있었으므로 이 기본 Inspector에는 나오지 않았다. Unity의 공개 `Editor.finishedDefaultHeaderGUI`로 이 Inspector에 전이별 함수 선택 UI를 연결했다. GTK 비공개 타입이나 내부 필드는 수정하지 않는다. 루트의 Machine 서브에셋을 선택해도 같은 선택 항목을 제공한다.

## 사용

1. StateMachine을 선택하면 Inspector에 **UAnimGraph Transition Functions**가 표시된다.
2. State 간 전이의 **Choose function…**에서 클래스를 검색해 함수를 선택한다.
3. 샘플은 **UAnimGraph → Open Transition Function Sample** 또는 Inspector의 **Open sample with two States**로 연다.
4. 샘플 그래프는 `Assets/UAnimGraph/TransitionFunctionSample/SampleGraph.uanimasset`이며, Entry → Idle 및 Idle ↔ Move가 연결되어 있다. 사용자 그래프는 변경하지 않는다.

새 런타임 샘플 클래스: `SampleTransitionConditions`.

```csharp
[TransitionLibrary]
public static class SampleTransitionConditions
{
    [TransitionEvaluation]
    public static bool AlwaysTrue(in TransitionContext context)
    {
        return true;
    }

    [TransitionEvaluation]
    public static bool AfterOneCycle(in TransitionContext context)
    {
        return context.From.TotalNormalizedTime >= 1;
    }
}
```

두 함수는 루트 파라미터가 없어도 작동한다. 기존 `LocomotionTransitions`의 함수들도 계속 사용한다. Entry는 시작 State를 지정하므로 함수 선택 대상이 아니다. 일반 State 사이의 전이가 하나 이상 있어야 Choose function이 나타난다. 샘플에는 해당 연결과 함수 선택이 미리 설정되어 있다.

## 검증

별도 Unity 프로젝트에서 실제 GTK GraphObject의 Inspector 경로 해석, 루트 Machine 서브에셋 경로 해석, 샘플 클래스 2개 함수의 등록·유효성, 샘플의 양방향 전이와 런타임 두 State 방문을 검사한다. 이번에는 사용자 화면의 Inspector를 마우스로 직접 조작하지 않았다.

신규 9개와 기존 전이 UI 12개 검증을 통과했다. 로그: `Logs/InspectorFunctionsFinal.log`, `Logs/UAnimGraph-inspector-functions-verification.txt`.
