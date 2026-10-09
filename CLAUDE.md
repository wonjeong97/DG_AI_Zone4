# CLAUDE.md

<!-- 이 프로젝트에만 해당하는 내용만 적는다. 코딩 지침, CHANGELOG 규칙, 멀티 에이전트 규칙, 스킬은 구글 드라이브 AgentSync의 전체 메모리에서 공통으로 불러온다. -->

## 프로젝트 개요

**이동형 AI 4존** — AI 코딩 체험 전시 콘텐츠. 관람객이 RFID 코딩 카드(동작·제어·논리·함수)를 리더기에 올려 설계창에 블록을 쌓고, 우주 미션을 코딩으로 푼다.

- 설치 장소: 고정되어 있지 않은 이동형 전시. 장소를 옮길 때마다 네트워크가 바뀌므로 Unity PC IP, 리더기의 Target IP, `RfidMappings.json`의 `readers`, `Server.json`의 `baseUrl`을 현장에 맞춘다.
- Unity 2022.3.62f3, C# 10(`Assets/csc.rsp`), UI 캔버스·영상만 써서 URP-Performant·카메라 후처리 꺼짐.
- 스택: VContainer, UniTask, MessagePipe, R3, ZLogger, ZString, DOTween, Addressables, Input System, 공용 템플릿 패키지 `com.huliacdev.template`(`HuliacDev.*`).
- 어셈블리: 런타임 `DGAIZone` 하나와 테스트 `DGAIZone.Tests`(`InternalsVisibleTo`). 테스트는 PlayMode만 있음.
- VContainer 1.19는 `Construct`의 `= null` 기본값을 선택 주입으로 보지 않는다(등록이 없으면 예외). 기본값은 테스트에서 직접 부를 때만 쓰이므로 루트 프리팹(GameLifetimeScope)의 InactivityTimer·SoundManager·APIManager 등을 빼지 않는다.

### 씬 흐름

`0_Title`(QR 확인·관리자 화면) → `1_Intro`(스토리·튜토리얼) → `2_LevelSelect`(레벨 고름·레벨 스토리) → `3_Game`(카드 코딩) → `4_Result`(나의 코딩 결과·AI 정답·결과 영상) → 레벨 1~4면 `2_LevelSelect`, 레벨 5면 `5_Outro` → `0_Title`.
관리자 레벨 이동 판은 `0_Title` → `2_LevelSelect`(그 레벨 스토리) → … → `4_Result` → `0_Title`(관리자 화면)으로 돌아간다.
비활동 타임아웃이 나면 타이틀로 돌아간다(타이틀에서는 타이머를 멈춤).

### 레벨

| 레벨 | 미션 | 카드 | 단계 | 성공 조건 |
|---|---|---|---|---|
| 1 | 로켓 추진력 맞추기 | 동작 | 3(추진체·탑재 장비·연료량) | 추진력(엔진 출력 + 연료 − 탑재 중량)이 목적지 거리와 정확히 같음 |
| 2 | 로켓 발사 순서 | 동작 | 5 | 발사 순서 블록 5개를 정해진 순서로 |
| 3 | 우주정거장 조건 | 제어(만약)·논리(그리고/또는)·동작(올리기/낮추기) | 5 | '만약 전기량이' = 이번 판 상한 + '낮추기', '만약 산소량이' = 이번 판 하한 + '올리기', 논리는 '그리고'('또는'이면 시스템 불안정). 정답 블록마다 게이지가 차고 두 게이지가 다 차야 성공 |
| 4 | 탐사 로봇 경로 | 동작·제어(반복) | 최대 5(1단계부터 코딩 완료 가능) | 자원을 모으고 함정을 피해 기지로 돌아옴 |
| 5 | 우주 도시 함수 | 함수 1·동작 4 | 5(5장 모두 놓아야 코딩 완료) | 동작 블록 4개가 모두 함수 정의 블록 안(함수 카드 뒤)에 있음 |

레벨별 블록 값과 단계 정의는 `RfidMappings.json`의 `levelMappings`, 스토리·미션 문구와 레벨 1·3 목적지·기준값은 `Assets/Data/Level{1~5}.asset`(LevelData)에 있다.

### 설정 파일 (StreamingAssets, 재빌드 없이 현장 조정)

- `Json/00_Common.json`, `0_Title.json` ~ `4_Result.json`: 씬별 연출 타이밍(페이드, QR 확인 시간, 스캐너 글자 간격 등). `00_Common.json`의 `robotVideoPath`는 로봇 영상 경로(없는 파일이면 기본 `Videos/robot_0811.webm`), `0_Title.json`에는 하단 안내 문구 7개(이름 자리 `{name}`)도 있다.
- `Json/Admin.json`: 관리자 비밀번호(4~6자리, 기본 `0000`), 관리자 화면·이름 입력 창 자동 닫기 `idleCloseSeconds`(60초), 비밀번호 창 `passwordIdleCloseSeconds`(10초), 진입 클릭 `entryClickCount`(10회)·`entryClickWindowSeconds`(3초). 시간·횟수가 1보다 작으면 기본값을 쓰고, 비밀번호를 바꿀 때 다른 값은 유지한다.
- `Json/Server.json`: 체험자 서버 주소·시간 제한·재시도 횟수.
- `RfidMappings.json`: 리더기, 카드(uid → 분류), 레벨별 블록 목록·단계.
- `Settings.json`(템플릿): 비활동 타이머(`useInactivityTimer`, `resetTime`), 프레임, 소리 등. `ShutdownSettings.json`(템플릿): 요일별 자동 종료.
- `Videos/4-{레벨}-Success.mp4`, `4-{레벨}-Fail[-원인].mp4`: 결과 영상(레벨 3만 원인별 실패 영상 `Electricity`·`O2`가 있음).

## 하드웨어·외부 연동

### RFID 리더기 (KA-LAN-754 × 5)

- `Reader_1` ~ `Reader_5`가 단계 1 ~ 5에 대응한다.
- 리더기가 TCP 클라이언트로 Unity PC(서버, 포트 `listenPort` 10123)에 접속한다. 리더기의 Target IP는 Unity PC의 IP여야 한다.
- 리더기 식별: 접속한 IP를 `readers[].ipAddress`와, ARP로 조회한 MAC을 `macAddress`와 대조한다. 둘이 서로 다른 리더기를 가리키면 MAC을 따르고(현장 이동으로 IP가 엇갈린 경우, 경고 로그), 한쪽만 맞으면 그쪽으로 식별한다.
- **연속 읽기 모드로 설정해야 한다.** 카드가 올라가 있는 동안 구분자 없는 원시 7바이트 UID를 계속 보낸다(0x0D가 섞일 수 있음).
  - `mappings[].uid`는 공백 없는 16진수 14자리(예: `81736922E51D04`)다. 1회 읽기 모드 응답 `A1G0` + 14자리 + 2자리의 가운데 14자리와 같다.
  - 같은 UID가 반복되면 한 번만 처리한다. UID가 `cardRemovedDebounceMs`(기본 1000ms) 동안 오지 않으면 카드를 뗀 것으로 본다.
  - 접속 직후 이미 올려져 있던 카드는 떼었다 다시 올릴 때까지 무시한다.
  - 등록되지 않은 UID(교통카드·겹친 카드 등)는 카드 판정에 넣지 않는다(올려 둔 카드가 새 카드로 다시 발행되지 않게).
  - 게임 화면은 리더기마다 올려져 있는 카드를 기억한다. 차례가 아닌 리더기에 미리 올린 카드나 되돌린 뒤에도 놓인 카드는 그 단계가 되면 이어서 쓰고, 카드를 떼면 잊는다.
- 카드 53장: 동작 28·제어 16·논리 5·함수 4. 카드는 분류만 알려 주고, 블록 값은 화면의 좌우 버튼으로 고른다.

### QR 스캐너 (타이틀)

- USB 키보드 방식(uid를 입력한 뒤 Enter). 연결된 키보드를 모두 구독한다.
- 글자 사이가 `scanCharGapSeconds`(`0_Title.json`, 기본 0.5초)보다 벌어지면 앞 글자를 버린다. QR 입력을 다시 받기 시작한 직후 이어서 들어오는 글자도 앞 스캔의 뒷부분으로 보고 버린다.
- uid에는 생년월일이 들어 있어 로그에 남기지 않는다.
- 스캐너는 모든 씬에서 꽂혀 있으므로 빌드 씬 6개의 EventSystem은 Send Navigation Events를 끈다(`m_sendNavigationEvents: 0`, `ScannerKeyboardInputTests`가 확인). 켜 두면 터치로 누른 버튼(Navigation Automatic)이 선택으로 남아 스캐너의 Enter에 한 번 더 눌리고, 글자 W·A·S·D에 선택이 옮겨 간다(T65). 새 씬을 만들 때도 끈다.

### 체험자 서버 (`Server.json`의 `baseUrl`)

- 타이틀 QR 확인: `checkActive`(체험 가능 여부)와 `getUser`(진행도 → 해금 레벨). 결과 업로드: `updateValue`. 이 존의 코드는 `D`.
- JSON 응답(`getUser`·`updateValue`) 앞뒤에 붙은 글자는 무시한다(첫 `{` ~ 마지막 `}`만 읽음). `checkActive` 응답은 평문이다.
- 서버 로그(`APIManager`, 템플릿 `ApiManagerBase`, 주소는 `Settings.json`의 `apiUrl`): 시작·종료·비활동. 비활동은 `move_idle_timeout`이고, `5_Outro`에서는 타임아웃·처음으로 모두 정상 이탈이라 `move_idle`을 보낸다. 타이틀에서는 QR로 확인한 체험자가 `resetTime` 동안 시작하기를 누르지 않을 때만 `TitleFlowController`가 `move_idle_timeout`을 보낸다.

### 관리자 화면 (타이틀)

- 타이틀 왼쪽 위의 보이지 않는 버튼을 3초 안에 10번 누른 뒤 비밀번호를 입력한다(횟수·시간은 `Admin.json`).
- 화면을 누르지 않고 두면 비밀번호 창은 10초, 관리자 화면·이름 입력 창은 60초 뒤 닫힌다(`IdleCloseTimer`, 타이틀은 비활동 타이머가 멈춰 있어 따로 잰다). 이미 바꾼 운영 모드는 반영되고, 입력 중이던 이름·비밀번호만 저장되지 않는다. QR 스캐너 키 입력으로는 시간을 다시 재지 않는다.
- 로컬·서버 모드 전환, 로컬 모드 체험자 이름(화면 키보드로만 입력), 비밀번호 변경, 레벨 이동(그 판의 결과는 서버에 올리지 않음).

### 입력·디버그

- 터치스크린. 화면 아무 곳이나 누르는 판정은 `Pointer.current`로 한다.
- 디버그 키(에디터·개발 빌드만, `App/DebugInputActions.inputactions`의 Debug 맵): `1`~`4`는 동작·제어·논리·함수 카드 흉내(`KeyboardRfidSimulator`), `Space`는 레벨 4 시뮬레이션·전체 레벨 해금.

## 공통 규칙의 예외

- 세션 상태는 GameSession(ScriptableObject) 대신 루트 스코프 싱글톤 저장소(`SelectedLevelStore`, `UnlockedLevelStore`, `GameResultStore`, `VisitorInfoProvider`, `AdminLevelJumpStore`)에 둔다(2026-10-07 결정).
- 템플릿 단축키(D 디버그 창·I 인스펙터·M 마우스 커서·F 창 포커스 복구)는 QR 스캐너 입력과 겹쳐 `Ctrl+D`·`Ctrl+I`·`Ctrl+M`·`Ctrl+F`로 바꿔 쓴다(`GameLifetimeScope.ConfigureInputBindings` → `App/DebugShortcutBindings.cs`).
- 레벨 상태(`IngredientLevel1~5State`)는 컨트롤러 로거를 빌려 쓰므로 로그 태그가 `[IngredientSelectionController]`다.
