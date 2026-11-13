# Inspector의 Script None 원인과 수정

## 원인

상단 Failed To Load Script Object는 런타임 UAnimAsset의 Script가 아니라 해당 파일을 처리하는 ScriptedImporter의 누락된 MonoScript 참조였다. 실제 프로젝트의 `.uanimasset.meta`에서 `script: {instanceID: 0}`을 확인했다.

UAnimRootImporter가 UAnimRootGraph.cs에 들어 있었고, 나머지 Layer/State/Machine Importer도 UAnimImporters.cs에 함께 들어 있었다. Unity가 각 Importer에 대응하는 MonoScript를 연결하지 못하는 구조였다. 아래에 표시되던 UAnimAsset과 그 Custom Inspector는 정상적으로 로드된 상태였다.

## 수정

Importer를 각각 일치하는 파일명으로 분리했다.

- UAnimRootImporter.cs
- UAnimLayerImporter.cs
- UAnimStateImporter.cs
- UAnimMachineImporter.cs

중복 등록은 제거하고 Importer 버전을 올려 재임포트를 유도했다. 런타임 클래스와 에셋 GUID는 유지한다. 재임포트 과정에서 구형 Mixer1D의 Threshold 옵션이 새 기본값으로 대체되는 호환 문제도 수정했다. 기본 두 입력은 포트 정의 시 생성하고 기존 임계값은 먼저 읽어 보존한다.

## 구분할 오류

화면 아래의 `Assign exactly one Machine or PoseGraph on Layer Base Layer`는 별개의 저작 오류다. 확인 시점의 사용자 루트에는 embeddedSources가 비어 있고 Base Layer의 Machine/PoseGraph 참조도 없었다. Open Root Graph로 열고 Base Layer에서 Create State Machine을 누르거나 사용할 그래프를 지정해야 한다. 스크립트를 임의로 할당하거나 임의의 상태 머신으로 연결하지 않았다.

## 검증

Importer 14개 검사 통과: 각 MonoScript.GetClass 일치, 네 확장자의 AssetImporter.GetAtPath 로드, 직렬화된 m_Script 참조, 기존 루트·서브에셋 로드. 수정 후 메타데이터는 유효한 MonoScript GUID 참조로 바뀌었다.

내부 저장 14개, 통합 41개·기본 회귀 33개·루트 연결 검증도 통과했다. 로그: `Logs/ImporterScriptsFinal.log`, `Logs/UAnimGraph-importer-script-verification.txt`.

Mixer 입력 21개와 기존 통합 검증도 최종 재실행하여 통과했다. 로그: `Logs/ImporterMixerCompatibilityFinal.log`.
