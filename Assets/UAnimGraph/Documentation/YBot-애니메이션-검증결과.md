# Y Bot 애니메이션 제작 및 오류 수정 결과

실제 프로젝트: `C:\Homework\UAnimGraph`

## 생성한 애니메이션

- 루트: `Assets/UAnimGraph/Tests/UAnimAsset.asset`
- Layer Graph: `Assets/UAnimGraph/Tests/UAnimAsset.uapose`
- Idle: `Assets/UAnimGraph/Tests/Locomotion/Idle.uastate`
- Walk/Run Blend1D: `Assets/UAnimGraph/Tests/Locomotion/Move.uastate`
- 상태 머신: `Assets/UAnimGraph/Tests/Locomotion/Locomotion.uamachine`
- 실행 장면: `Assets/UAnimGraph/Tests/Locomotion/YBotLocomotion.unity`

기존 Y Bot Humanoid 모델과 Rifle Idle, Walking, Running FBX의 실제 클립을 사용했다. 기본 상태는 Idle이며, float 파라미터 `speed`(표시 이름 Speed)가 0.1보다 크면 이동 상태로, 0.1 이하면 Idle로 전환한다. 전이 시간은 0.2초이고 Walk/Run 블렌드 임계점은 0.5와 2다.

장면을 열어 Play를 누른 뒤 **Window > UAnimGraph > Preview and Debug**의 Live Debug Target에 Y Bot의 UAnimGraphPlayer를 지정하면 Speed를 조절할 수 있다. 기본값 0은 Idle이며 0.5는 Walk, 2는 Run이다.

## 수정한 오류

1. 새 그래프를 작성하는 도중의 출력 미연결·참조 미지정을 실패한 임포트로 반복 기록하던 처리를 수정했다. 제작 중에는 그래프 경고로 안내하고, 실행·명시적 검증·빌드에서는 미완성 정의를 계속 거부한다.
2. 이전 UAnimGraphComponent가 삭제되거나 누락된 에셋을 참조할 때 발생하는 null 접근을 막았다. 해당 컴포넌트를 비활성화하고 현재 UAnimGraphPlayer 사용을 안내한다.
3. 이전 경로의 잘못된 믹서 연결, 그래프 재생성 시 누수, 유효하지 않은 그래프의 중복 해제를 방어했다. 혼동을 유발하던 이전 에셋 생성 메뉴를 제거했다.
4. 에디터에서 런타임 SceneManager.CreateScene을 호출하던 작업 코드를 EditorSceneManager.NewScene으로 변경했다. 장면 전환으로 언로드될 수 있는 루트 에셋도 다시 로드한다.
5. 정의 해시에서 Unity 세션별 instance ID를 제거하고 GUID/local file ID를 사용하여, 단순 에셋 재로딩을 정의 변경으로 잘못 표시하지 않게 했다.
6. 제작 작업을 반복해도 기존 기본 레이어 ID를 유지한다.

## 최종 검증

- 기존 자동 검증 **33개 통과**.
- 실제 Humanoid 클립의 Idle → Walk/Run → Idle 전이 및 다리 뼈 회전 확인.
- 전이 전후 Playable 개수 유지와 종료 시 그래프 해제 확인.
- 빈 그래프 생성 → 출력 추가 → 실제 클립 연결 → 완료된 정의 검증까지 **Console 오류 0건**.
- 이전 플레이어의 누락 참조 Awake/Start, 이전 그래프 재생성·재생·반복 해제 검사 통과.
- 그래프 에셋 언로드·재로딩 후 정의 해시 유지 검사 통과.
- 실제 Play 모드 **6,235프레임**, 두 상태 진입 및 뼈대 움직임 확인, **오류 0건**.

검증은 열린 사용자 Unity를 조작하지 않고 별도 프로젝트 사본에서 숨김 실행했다. 생성 결과는 원본 파일의 해시를 확인하여 사용자 변경이 없을 때만 반영했다. 사용자가 열어 둔 장면을 전환하거나 Play 모드로 바꾸지 않았다.

검증 로그는 실제 프로젝트의 `Logs/UAnimGraph-YBot-background-final.log`, `Logs/UAnimGraph-YBot-play-verification.txt`, `Logs/UAnimGraph-draft-regression.txt`에 저장했다.
