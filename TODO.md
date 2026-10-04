# TODO

<!-- 이 프로젝트에서 해야 할 일. 사용자가 적고, Claude가 작업하면서 갱신한다(Antigravity는 읽기만 한다). -->
<!-- 형식: - [ ] 할 일 — 담당: Claude / 검증: Antigravity -->
<!-- 끝나면 [x]로 바꾸고 완료일을 붙여 "완료"로 옮긴다. 오래된 완료 항목은 지워도 된다(git 기록에 남는다). -->

## 진행 중


## 할 일

- [ ] T23 설계창 배치 방식 확정: 기획 확인 뒤 DesignPanel의 배치 방식 드롭다운('줄여서 한 화면에'/'크게 두고 자동 스크롤')에서 고른 하나만 남기고 나머지 코드·설정값 제거 — 담당: Claude / 검증: Antigravity

## 완료

- [x] MCP for Unity 패키지 10.2.0 → 10.3.0 업데이트(packages-lock.json 고정 커밋 갱신) — 담당: Claude / 검증: Antigravity (2026-10-04)
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
