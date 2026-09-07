# 진행 상태

## 현재 단계

- 확인일: 2026-09-07 (Asia/Seoul)
- **1단계 프로젝트 조사와 문서 정리 완료. 사용자 확인 대기.**
- 승인 범위: 프로젝트·HTML·CLI 확인 및 문서 작성까지. 게임 구현은 시작하지 않았다.
- 기존 루트 AGENTS.md 및 docs 문서는 없었으므로 새로 작성했다.
- 기획은 [GAME_DESIGN.md](GAME_DESIGN.md), 작업 규칙은 [AGENTS.md](../AGENTS.md)를 참조한다.

## 프로젝트 조사 결과

| 항목 | 확인 결과 |
| --- | --- |
| Unity | ProjectSettings/ProjectVersion.txt: **6000.6.0f1**, revision f7f8ed4d1e24 |
| 렌더링 관련 구성 | URP 17.6.0 패키지와 PC/Mobile 렌더 파이프라인·렌더러 설정 에셋 존재. 플랫폼 결정으로 해석하지 않음 |
| 주요 패키지 | Input System 1.20.0, AI Navigation 2.0.14, Test Framework 1.8.0 |
| 씬 | Assets/Scenes/SampleScene.unity. Main Camera, Directional Light, Global Volume 존재. 빌드 씬 목록에도 등록됨 |
| 스크립트 | Assets/TutorialInfo 아래 Readme.cs 및 Editor/ReadmeEditor.cs만 확인. 게임 전용 코드 없음 |
| 기타 에셋 | Assets/Settings, InputSystem_Actions.inputactions, 템플릿 Readme |
| 참고 자료 | References/prototype.html, 522줄 전체 읽음. 내용 분석은 GAME_DESIGN.md 참조 |
| Git | 현재 루트는 Git 저장소가 아님. git status로 변경 비교 불가 |

## Unity CLI와 에디터

- 실행 파일: `/Users/minchichi/.unity/bin/unity`
- `unity --version`: **1.0.0-beta.6**
- `unity --help`, `unity list --help`, `unity pipeline --help`에서 사용 가능한 명령과 옵션 확인.
- 주요 명령: `status`(연결 상태), `pipeline list`(에디터/패키지 상태), `list`(연결된 에디터 도구 목록), `command`(에디터 명령), `open`, `run`, `build`, `test`, `editors`, `projects`, `doctor`, `mcp`. 도움말 확인은 각 명령의 실행 성공을 의미하지 않는다.
- `unity pipeline list --json`: MonsterBattle 실행 중, `hasPipelinePackage: false`, `isReachable: false`.
- `unity status --json`: 연결 0개, `STATUS_NO_INSTANCES`.
- 주 에디터 프로세스의 실행 경로도 Unity 6000.6.0f1로 확인했다. 에디터 실행과 CLI 자동화 연결은 별개다.
- 이번에는 패키지를 설치하지 않았다. 연결된 에디터의 실제 도구 목록은 아직 조회할 수 없다.

### 다음 단계에 사용할 연결 설정 안내

CLI가 안내한 설치 명령은 아래와 같다. **아직 실행하지 않은 설정 절차**이며, 게임 구현 단계에서 연결을 준비할 때 사용한다.

1. Unity Hub의 **Projects**에서 **MonsterBattle**을 열어 둔다. 현재 조사 시점에는 이미 실행 중이다.
2. macOS **터미널** 앱에서 다음 명령을 실행한다. 이 명령은 프로젝트에 Pipeline 패키지를 설치하므로 프로젝트 파일이 변경된다.

   ```sh
   unity pipeline install --project-path /Users/minchichi/Developer/MonsterBattle
   ```

3. Unity의 패키지 처리와 스크립트 컴파일이 끝날 때까지 기다린다.
4. 터미널에서 `unity pipeline list --json`과 `unity status --json`을 실행해 패키지 설치 및 연결 여부를 확인한다.
5. 연결되면 `unity list --project-path /Users/minchichi/Developer/MonsterBattle`로 실제 제공되는 도구를 확인한다. 실패하면 오류 내용을 먼저 조사한다.

## 개발 순서와 단계별 확인 결과물

현재는 요청한 순서를 유지한다. 소수 병사로 이동·접촉을 먼저 검증하고 병종 차이를 확장하는 순서가 적절하다. 병력 규모가 정해지면 이동·성능 구조를 다시 검토한다.

| 단계 | 범위 | 사용자가 확인할 작은 결과물 | 상태 |
| --- | --- | --- | --- |
| 1 | 프로젝트·HTML 조사, 문서 정리 | 기획과 진행 문서 | 완료, 확인 대기 |
| 2 | 작은 3D 전장, 카메라, 임시 아군·적 | Play에서 양측 도형과 전장을 볼 수 있음 | 대기 |
| 3 | 소수 병사의 자동 이동·공격·체력·사망 | 병사들이 접근해 싸우고 체력이 소진되면 사망 | 대기 |
| 4 | 승패 판정과 재시작 | 단일 전투 결과를 확인하고 초기 상태로 다시 시작 | 대기 |
| 5 | 전투 전 배치 조작 | 배치를 바꾸고 전투를 시작 | 대기 |
| 6 | 창병·기마병 역할 차이 | 병종별 전투 행동 차이를 비교 | 대기 |
| 7 | 모집·성장·보상 등 진행 규칙 협의와 추가 | 합의한 규칙으로 전투 전후 흐름을 실행 | 대기 |
| 8 | 3D 모델·애니메이션·효과음, 완성도 개선 | 전투 동작과 소리, 시각적 피드백 확인 | 대기 |

7단계처럼 큰 단계는 시작할 때 작은 실행 단위로 나누어 사용자와 진행 범위를 맞춘다.

## 이번 검증 및 확인 방법

- 파일 조사와 CLI 읽기 전용 진단을 수행했다. HTML 브라우저 실행, Unity Play, 새 컴파일, 빌드 및 테스트는 수행하지 않았다.
- 씬·C#·에셋·패키지·프로젝트 설정은 변경하지 않았다. 새 문서 3개만 작성했다.
- 사용자 확인: VS Code에서 루트 `AGENTS.md`, `docs/GAME_DESIGN.md`, `docs/PROGRESS.md`를 열고 **Shift+Command+V**로 Markdown 미리보기를 확인한다.
- Unity 기본 씬을 보려면 **Project** 창에서 **Assets → Scenes → SampleScene**을 더블 클릭하고 **Hierarchy**의 Main Camera, Directional Light, Global Volume을 확인한다. 아직 전장이나 병사는 없다.

## 다음 작업

사용자가 확인 후 “다음 단계”라고 하면 **2단계만** 진행한다. CLI 연결을 준비하고 작은 바닥 전장, 전체가 보이는 카메라, 구분 가능한 임시 아군·적 도형을 만든다. 자동 이동·공격은 3단계에서 진행한다. 2단계 종료 시 가능한 컴파일·Play 검증 결과와 사용자의 정확한 확인 절차를 기록한다.
