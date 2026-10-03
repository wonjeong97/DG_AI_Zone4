# TODO

<!-- 이 프로젝트에서 해야 할 일. 사용자가 적고, Claude가 작업하면서 갱신한다(Antigravity는 읽기만 한다). -->
<!-- 형식: - [ ] 할 일 — 담당: Claude / 검증: Antigravity -->
<!-- 끝나면 [x]로 바꾸고 완료일을 붙여 "완료"로 옮긴다. 오래된 완료 항목은 지워도 된다(git 기록에 남는다). -->

## 진행 중

- [ ] T19 T16 반영 3: IngredientSelectionController 분할(화살표 힌트·경고 연출·설계창 분리, 레벨 1 값·레벨 4 분기 상태로 이동), 테스트 리플렉션 정리(완료 기준: 씬 참조 재연결, PlayMode 테스트 통과, 동작 변화 없음) — 담당: Claude / 검증: Antigravity

## 할 일

## 완료

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
