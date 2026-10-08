# TODO

<!-- 이 프로젝트에서 해야 할 일. 사용자가 적고, Claude가 작업하면서 갱신한다(Antigravity는 읽기만 한다). -->
<!-- 형식: - [ ] 할 일 — 담당: Claude / 검증: Antigravity -->
<!-- 끝나면 [x]로 바꾸고 완료일을 붙여 "완료"로 옮긴다. 오래된 완료 항목은 지워도 된다(git 기록에 남는다). -->

## 진행 중


## 할 일


## 완료

- [x] T59 씬·프리팹·텍스처 UI 설정 점검(스킬 19번): 클릭 받지 않는 Raycast Target 6개 끔, 3_Game 페이드 UI 2곳 하위 Canvas 분리, 씬에 남은 지운 직렬화 필드 값 정리, 관리자 이름 입력란을 직접 누르면 선택되던 버그 수정(밉맵·투명 차단 이미지는 문제 없음, HANDOFF.md 참고) — 담당: Claude / 검증: Claude(agy 한도 초과로 대신) (2026-10-09)
- [x] T58 타이틀·인트로 성능·버그 점검(T54에서 시간 제한으로 끊긴 부분): 인트로 스토리를 화면 전환이 끝난 뒤 시작, 튜토리얼 7페이지 미리 로드·실패 핸들 재시도, QR을 다시 받은 직후 잘려 들어오는 글자 버림, 관리자 레벨 이동 중 QR·서버 응답·대기 초과가 해금 레벨을 바꾸지 않게 함(HANDOFF.md 참고) — 담당: Claude / 검증: Claude(agy 한도 초과로 대신) (2026-10-09)
- [x] T57 미룬 리팩터링(T54에서 범위가 커서 뺀 것 중 필요한 것): IngredientSelectionController의 InitializeWorkflowAsync·OnRfidTagReceived를 단계별 메서드로 나눔(스킬 10번), 쓰이지 않는 스테이지 인덱스 정리. 주입 실패 Debug.LogError 일괄 추가(테스트가 로거 없이 만들어 오류 로그로 실패함)·상태 인터페이스의 안 쓰는 매개변수(다섯 상태 공통 모양, 레벨 5 검토 중)는 하지 않음 — 담당: Claude / 검증: Claude(agy 한도 초과로 대신) (2026-10-09)
- [x] T23 설계창 배치 방식 확정: 기획 확인 뒤 DesignPanel의 배치 방식 드롭다운('줄여서 한 화면에'/'크게 두고 자동 스크롤')에서 고른 하나만 남기고 나머지 코드·설정값 제거 → 사용자 결정으로 '크게 두고 자동 스크롤'만 남기고 DesignLayoutMode·maxFitScale·결과 저장소의 배치 방식·전용 테스트 2개 삭제(실행 동작은 이미 자동 스크롤이라 그대로) — 담당: Claude / 검증: Claude(agy 한도 초과로 대신) (2026-10-09)
- [x] T56 남은 조용한 대체값에 경고 추가: `_selectedLevelStore`가 null이면 말없이 레벨 1을 쓰는 곳 7곳(GameFlowController·MissionBoardController 2·ResultAiPanel·ResultFlowController 2·ResultVideoPanel). 필수 주입이라 실제로는 테스트에서만 null(PR #56 리뷰에서 찾음) — 담당: Claude / 검증: Claude(agy 한도 초과로 대신) (2026-10-09)
- [x] T54 전체 코드 재점검: 스킬(unity-stack-scaffold·unity-network-protocol) 위반, 성능 최적화, 리팩터링 후보를 찾아 확인된 것만 적용(사용자 요청, HANDOFF.md 참고) — 담당: Claude / 검증: Claude(agy 한도 초과로 대신, 사용자 확인) (2026-10-09)
- [x] T55 HuliacDev Template 패키지 26.9.25-3 → 26.10.9-1 업데이트(packages-lock.json 고정 커밋 b4547f3 → 640d05e, VideoManager 영상 RenderTexture 깊이 버퍼 제거 — 이 프로젝트는 해당 API를 쓰지 않음, 버전은 이미 26.10.9)(HANDOFF.md 참고) — 담당: Claude / 검증: Claude(Antigravity 한도 초과로 대신 검증) (2026-10-09)
- [x] T53 로그 정리: 운영 진단에 필요한 정보 로그(리더기 접속·끊김, 서버 응답, 관리자 조작, QR 진단, 레벨 판정 근거)만 남기고 연출·내부 단계 로그(게이지 시퀀스, 깜빡임, 미리보기, 레벨 4 도착 연출, 초기화·설계 기록, RFID 원시 태그·발행) 삭제. 체험자 행동(QR·시작하기·레벨 고름·결과 다음·처음으로, 카드 올림·뗌과 처리 결과, 설정하기·취소하기·코딩 완료·건너뛰기)은 '{name}이 ~를 함' 형식으로(조사 이/가 자동)(사용자 요청, HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-09)
- [x] T52 RFID 리더기 연속 읽기 모드 대응: 읽기 명령(폴링)을 없애고 리더기가 계속 보내는 원시 7바이트 UID를 받아 같은 UID 반복은 한 번만 발행, UID가 cardRemovedDebounceMs 동안 없으면 카드 떨어짐으로 판정, 접속 때 올려져 있던 카드는 떼었다 다시 올릴 때까지 무시. RfidMappings.json 카드 uid를 1회 읽기 값(A1G0+14자리+2자리)의 가운데 14자리로 변환하고, 팀원이 공유한 카드 목록에서 새 카드 43장 등록(제어는 Condition·Repeat 구분 없이 '제어'로)(사용자 요청, HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-09)
- [x] T51 타이틀에서 비활동 타이머 멈춤: 1존처럼 GameManager가 0_Title에서는 InactivityTimer를 Pause, 다른 씬에서는 Resume해 타이틀 대기 중 타임아웃 이벤트 발행 로그와 APIManager의 '보내지 않음' 로그가 반복해 남지 않게 함(사용자 요청, HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-08)
- [x] T50 TitleFlowController.CancelConfirmTimeout 잠재 예외: 시작하기 대기 작업이 아직 설정을 읽는 중(시작 버튼이 뜬 뒤 약 1프레임)에 취소하면 Cancel()이 그 자리에서 finally를 실행해 필드를 null로 만들고 바로 다음 Dispose()에서 NullReferenceException. 필드를 지역 변수로 옮겨 먼저 비운 뒤 Cancel·Dispose하도록 고침(T49 Play 모드 확인 중 발견, 실사용에서 겪기는 거의 어려움, HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-08)
- [x] T49 타이틀 비활동 로그: 타이틀은 이미 대기 화면이라 입력 없이 기다리기만 할 때는 move_idle_timeout을 보내지 않고(로컬 모드 포함), 서버 모드에서 QR로 확인한 체험자가 시작하기를 누르지 않아 QR 대기로 돌아갈 때만 보냄(사용자 요청, HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-08)

- [x] T48 체험자 서버 JSON 응답 앞뒤 군더더기 무시(1존 현장에서 getUser가 JSON 끝 } 뒤에 ``` 줄을 붙여 보내 모든 체험자가 타이틀에서 막힘, 4존도 같은 서버·같은 코드) — getUser·updateValue는 첫 { ~ 마지막 }만 읽고, getUser 실패 사유에 JsonUtility 오류 문구 포함(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-08)

- [x] T47 레벨 1 핵 추진 엔진 +12 → +15(외계 행성 정답 조합 1가지뿐이라 너무 어려움, 사용자 요청). 달 2·화성 6·외계 행성 3가지(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-08)

- [x] T46 레벨 5: '그리고'(논리 카드)를 없애고 동작 블록 '연결 통로 코드'를 추가(함수 1·동작 4장, 스토리 문구 네 가지 시스템), 현재 상황 화면 그림(Image_Dome·Rover·Tower·Corridor)을 처음에 숨기고 동작 블록이 함수 정의 블록 안에 들어가 다 붙으면 맞는 그림을 보여 줌. 모든 레벨: 마지막으로 확정한 단계의 카드를 떼면 취소하기로 처리, 리더기 '카드 없음' 오응답 디바운스(RfidMappings.json cardRemovedDebounceMs 1000)(사용자 요청, HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-08)

- [x] T45 타이틀 QR 입력: 글자 사이가 0.5초(0_Title.json scanCharGapSeconds — PC 렉 대비 사용자 요청으로 JSON 분리)보다 벌어지면 앞에 모은 글자를 버리고 새로 모음(실제 리더기 테스트에서 찍기 전에 눌린 키 한 글자가 uid 앞에 붙어 13자로 들어온 경우가 있었음, 사용자 요청. 1존은 사용자가 따로 반영)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] T44 QR·체험자 서버 API(checkActive·getUser·updateValue, 재시도), 타이틀 QR 확인 흐름, 결과 업로드, getUser 해금 변환(1존 규칙: 기록 있는 마지막 D 레벨 + 1, 최대 5, 없으면 1), 결과 문구 '미션 성공!'(사용자 확인), 연출 값 1존과 맞춤(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] T43 관리자 페이지(타이틀 왼쪽 위 10회 터치 → 비밀번호 → 비밀번호 변경·운영 모드·체험자 이름·레벨 1~5 이동) + 체험자 설정 이전(Visitor.json → VisitorSettings SO, PlayerPrefs 우선). 1존과 같은 동작·값, 레벨 이동은 2_LevelSelect에서 그 레벨 스토리를 자동으로 띄움(사용자 확인)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] T42 QR 스캐너 키 입력 충돌: 템플릿 디버그 단축키 D·I·M을 런타임 바인딩 오버라이드로 Ctrl+D·Ctrl+I·Ctrl+M으로(4존 디버그 키 숫자 1~4·Space는 에디터·개발 빌드 전용이고 스캐너를 읽는 타이틀에서는 꺼져 있어 그대로 둠, 사용자 확인) — 담당: Claude / 검증: Antigravity (2026-10-07)
- [x] T41 카드가 떨어졌을 때 설계창 처리 변경: 확정된 단계의 카드가 떨어지면 그 단계부터 뒤 블록을 흐리게 하는 대신 임시로 떨어뜨리고(같은 분류 카드가 돌아오면 천천히 다시 붙음, 다른 분류면 그 단계부터 다시 시작), 떨어져 있는 동안 설정하기·코딩 완료를 막음. 값을 고르던 단계의 카드가 떨어지면 고르던 값만 비움, 리뷰에서 찾은 경계 상황(게임 화면이 비활성일 때 돌아온 카드, 다시 붙는 도중에 또 떼기)와 실기에서 찾은 스크롤 문제(설계창이 스크롤된 상태에서 카드를 떼거나 다시 올릴 때)도 수정(HANDOFF.md 참고) — 담당: Claude / 검증: Claude(사용자 요청으로 agy 대신) (2026-10-06)
- [x] T40 RFID 카드 등록: 동작 카드 4장(A1G08061AF3BA7F20472, A1G0807369226313040D, A1G081736922B958047D, A1G0817369226642040D), 제어 카드 1장(A1G081736922C12B0409), 논리 카드 1장(A1G080736922BCA1047B)을 `RfidMappings.json` mappings에 추가(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-06)
- [x] T39 RFID 리더기 추가 등록: `RfidMappings.json` readers에 Reader_2(34-46-63-D4-38-92)·Reader_3(34-46-63-D4-33-EA)·Reader_4(192.168.0.183, 34-46-63-D4-33-8F)·Reader_5(192.168.0.184, 34-46-63-D4-35-35) 추가, 1~3번 IP는 사용자가 192.168.0.180~182로 변경(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-06)
- [x] T38 사운드: 1존과 같은 효과음 8종(파일·Settings.json sounds·루트 프리팹 SoundManager)을 넣고 1존 규칙대로 대응 지점에서 재생(전용 효과음이 없는 버튼·터치는 buttonClick, 잘못된 카드 경고는 codingAlert, 결과 씬 두 설계창 블록도 blockAssembled)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T37 MCP for Unity 패키지 업데이트(10.2.0 → 10.3.0, git #main 재해석, 로컬 MCP 서버도 10.3.0으로 재시작)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T36 레벨 5 설계창: 함수 카드 다음에 놓은 블록(동작·논리)을 모두 오른쪽 함수 정의(함수 바디) 블록 안쪽에 쌓고 함수 정의 블록이 안쪽 블록 수만큼 늘어나게(함수 카드보다 먼저 놓은 블록과 완성하기는 시작하기 아래 그대로, 결과 화면 두 설계창도 같음)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T35 디버그용 인스펙터 값(GameFlowController.debugStartLevel, LevelSelectFlowController.debugUnlockedLevelCount)을 전처리기(#if UNITY_EDITOR || DEVELOPMENT_BUILD)로 에디터·개발 빌드에서만 적용, 릴리스 빌드는 값이 남아 있으면 경고만(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T34 PR #39 머지 전 리뷰(Antigravity 6묶음 + 다중 에이전트 탐색·반박 검증)와 수정: 결과 화면 AI 패널 설계창 드래그 스크롤(GraphicRaycaster를 중첩 Canvas인 DesignWindow로), '나의 코딩 결과' 블록 쌓기를 씬 전환 페이드인 뒤로, CHANGELOG 정리(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T33 디버그 입력을 InputAction으로: LevelSelect 스페이스바 전체 해금 추가(에디터·개발 빌드, 레벨을 고르기 전까지), 숫자키 카드 시뮬레이션·레벨 4 스페이스바도 DebugInputActions 에셋(Debug 맵)으로 전환(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T32 결과 화면: 왼쪽 아래 '나의 코딩 결과' 패널 추가, 플레이어·AI 설계창을 3_Game 블록 이미지로(배치 방식은 3_Game 설계창을 따름, 블록 1초 간격), 레벨 3 실패 원인별 영상(4-3-Fail-Electricity/O2, 논리 오류·둘 다 부족은 4-3-Fail), 마지막 레벨 5(4-5 영상)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T31 설계창 블록 묶음(시작하기)을 모든 레벨에서 더 왼쪽으로, 레벨 5 함수 정의 블록도 같은 거리만큼 왼쪽으로(DesignPanel 인스펙터 stackShiftLeft, 기본 30)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T30 레벨 5 함수 블록(Zone1 함수 사용·함수 정의 ㄷ자) + 임시 레벨 5 진행(함수1·동작3·논리1, 순서 자유, 다 쓴 분류 카드는 경고, 5장 다 놓으면 성공): 함수 사용은 시작하기 아래 줄, 함수 정의는 설계창 오른쪽. 동작·논리 위치는 기획 확인 중이라 우선 시작하기 아래 줄에 순서대로(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T29 레벨 5 현재 상황 화면: Image_CurrentSituation 아래 Panel_Level5에 레퍼런스(level5_ref.png)처럼 로버·통신탑·돔 기지·연결 통로를 원본 크기로 배치하고 GameFlowController 패널 목록 5번째에 연결(게임 진행은 기획 확정 뒤)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T28 설계창 값 블록 글자를 Zone1처럼 블록 폭 가운데에(왼쪽 홈 폭만큼 10px 오른쪽으로 치우치던 것)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T27 레벨 4 설계창: '반복하기'를 ㄷ자 블록(만약과 같은 If.png, 횟수 값 블록 포함)으로, 바로 뒤 '이동하기'를 그 안쪽에 표시. ㄷ자 관련 이름을 만약·반복하기 공용(FlowControl)으로 바꾸고 배율 계산용 모양을 레벨 상태가 정함(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T26 설계창 스크롤: 터치·마우스 드래그로 위로 올려 볼 수 있게(DesignScrollView에 GraphicRaycaster, Viewport에 투명 입력 영역), 올린 상태에서 코딩 완료 시 맨 아래로 부드럽게 내린 뒤 완성하기 블록 연결(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T25 레벨 3 설계창: '만약' 단계를 Zone1 ㄷ자 블록(If)으로, 뒤따르는 동작 블록을 그 안쪽에, '그리고'·'또는'을 초록 논리 블록(Logic.png 새로 만듦)으로 표시(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-04)
- [x] T24 설계창 블록 묶음을 모든 레벨에서 레벨 1처럼 왼쪽에 배치(단계가 많거나 값 블록이 없는 레벨이 가운데로 몰리던 것, 레벨 1 위치는 그대로)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-03)
- [x] T22 설계창 블록 코딩 연출: Zone1 블록 이미지로 상단 '시작하기', 설정하기 시 명령+값 블록이 내려와 맞물리며 쌓임, 취소 시 빠짐, 코딩 완료 시 하단 '완성하기' 연결. 배치 방식 두 가지를 인스펙터 드롭다운으로 비교 가능(T23에서 하나로 정리)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-03)
- [x] T21 MissionBoardController 레벨별 미션 문구 메서드 5개를 공용 ApplyMissionText로 통합, 씬을 떠날 때 재료 선택 초기화가 파괴된 오브젝트에서 계속되던 문제 수정(템플릿 JsonLoader 취소 처리는 Template TODO로)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-03)
- [x] T19 T16 반영 3: IngredientSelectionController 분할(화살표 안내·잘못된 카드 경고·설계창을 컴포넌트로 분리, 레벨 1 값·레벨 4 규칙을 상태로 이동), 테스트 리플렉션 제거 — 씬 참조 재연결, PlayMode 56/56, Play 모드 레벨 1·4 확인(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-03)
- [x] T20 프로젝트 재점검(T16 이후): 스킬 준수·버그·최적화·리팩터링 재확인 후 확인된 버그와 규칙 위반 수정(RFID 카드 떨어짐 이벤트 메인 스레드 전환, 결과 영상 종료 대기 상한, MonoBehaviour `?.`, 조용한 반환, 터치 판정 중복, 튜토리얼 완료 이벤트 R3 전환)(HANDOFF.md 참고) — 담당: Claude / 검증: Antigravity (2026-10-03)
- [x] T18 T16 반영 2: Level4BoardController 규칙/연출 분리, 이동 방향·판정 규칙 중복 제거, 배치 풀 캐시, 그리기 순서 할당 제거 — PlayMode 56/56 통과 — 담당: Claude / 검증: Antigravity (2026-10-03)
- [x] 프로젝트 설정 정리: Player Settings Version을 수정일 26.10.1로 — 담당: Claude / 검증: Antigravity (2026-10-01)
- [x] UI 전용 프로젝트 렌더링 경량화: Quality 기본을 Performant로, Graphics 기본 RP를 URP-Performant로, 빌드 씬 카메라 후처리 끔 — 담당: Claude / 검증: Antigravity (2026-10-01)
- [x] T17 T16 반영 1: 로봇 영상 준비 실패 처리, 디버그 입력 에디터·개발 빌드 한정, 마지막 레벨 상수 통합, 작은 규칙 위반, 설정 폴백 이중 정의 제거(HANDOFF.md 참고) — 담당: Claude, Antigravity (2026-10-01)
- [x] T16 프로젝트 전체 점검: 스킬 준수 여부, 버그 가능성, 최적화·리팩터링 후보 정리(코드 변경 없음, HANDOFF.md 참고) — 담당: Claude, Antigravity (2026-10-01)
- [x] T14(PR #34) 교차 리뷰: Claude 자체 리뷰 + Antigravity 코드·설계 리뷰 — 결과 연출 중 비활동 타이머 정지, 영상 준비 실패 시 무한 대기 방지 반영(HANDOFF.md 참고) — 담당: Claude, Antigravity (2026-10-01)
- [x] 결과 화면 AI 연출: 결과 영상 후 "AI가 코딩중입니다..." → 우측 상단 AI 패널(이번 판 정답 설계창 → 성공 영상) → 컴플리트 패널 — 담당: Claude (2026-10-01)
- [x] 결과 화면 Text_MissionComplete를 미션 결과에 따라 "미션 완료!"/"미션 실패!"로 표시 — 담당: Claude (2026-10-01)
- [x] T11(PR #32 레벨 4 반복 필수화) 교차 검토: Claude 자체 리뷰 + Antigravity 코드·설계 리뷰 — 검증기 제어 카드 단계 검사 반영(HANDOFF.md 참고) — 담당: Claude, Antigravity (2026-10-01)
- [x] 레벨 4: 이동 블록만으로는 5장 안에 못 풀고 반복 블록을 써야 풀리는 배치만 나오게 변경(PDF "제어 블록을 사용하지 않음" 지적) — 담당: Claude (2026-10-01)
- [x] T9(PR #31 리뷰 지적 반영) 검증: 콘솔 에러, PlayMode 테스트, 변경 파일 정적 점검·코드 리뷰 — 전 항목 통과(HANDOFF.md 참고) — 담당: Antigravity (2026-10-01)
- [x] PR #31 코드 리뷰 지적 14건 반영(데이터 검증, 이름 후반영, 레벨 3 id 분기, 고아 코드·중복 제거, 블록 목록 공유 구조, 패널 헬퍼, 조사 자리표시자, 테스트 보강) — 담당: Claude (2026-10-01)
- [x] 레벨 2 선택지 섞기 + 취소 시 진행바가 줄지 않던 버그 수정 — 담당: Claude (2026-10-01)
- [x] 빌드 씬 6개에서 효과 없는 Directional Light·Global Volume 제거, 카메라 후처리 끔 — 담당: Claude (2026-10-01)
- [x] UI 리배치 최적화: 반복 연출·스토리 텍스트·패널에 중첩 Canvas, 숨긴 패널 Canvas 끔, Viewport RectMask2D, 빈 레이아웃 정리 — 담당: Claude (2026-10-01)
- [x] 레벨 1: 추진력 = 엔진(7/10/12) + 연료 − 탑재, 목표 거리와 정확히 같아야 성공, 초과 시 게이지 감소 — 담당: Claude (2026-10-01)
- [x] 레벨 3: Group_O2/Group_Electric 위치 교환, 전기 조건→전기 게이지·산소 조건→산소 게이지 — 담당: Claude (2026-10-01)
- [x] 인트로·아웃트로 `{name}` 상수를 Constants.VisitorPlaceholder로 통일 — 담당: Claude (2026-10-01)
- [x] 레벨 1 목적지·레벨 3 기준값 범위를 LevelData로 이동 — 담당: Claude (2026-10-01)
- [x] RFID 블록에 id/label/value 분리, 코드는 id로 판정 + PDF 블록 이름 반영 — 담당: Claude (2026-10-01)
