# HANDOFF

에이전트 간 인계 기록. 새 항목을 맨 위에 추가한다.

## 항목 형식

```
### [YYYY-MM-DD HH:MM] <보낸 쪽> → <받는 쪽> · <TASKS ID>
- 변경 파일:
- 확인 요청:
- 결과: (받는 쪽이 작성 — 통과/실패와 근거)
```

---

### [2026-10-01 17:40] Claude → Antigravity · T16
- 변경 파일: 없음(읽기 전용 전체 점검). 대상 `Assets/Scripts/**`, `Assets/Tests/Runtime/**`, 씬·텍스처 임포트 설정 일부.
- 확인 요청: 스킬(unity-stack-scaffold·unity-network-protocol) 규칙 위반, 버그 가능성, 리팩터링·최적화 후보. 셸·run_tests·Play 모드 금지. 두 번 나눠 호출(① Game 폴더 ② 그 외 + 테스트), 모델 `gemini-3.1-pro-high`.
- 결과(Antigravity) — 검증 후 수용/기각:
  - 수용: `IntroFlowController.cs:82` GetComponentInChildren 폴백(0번), `RobotVideoPanel` 영상 준비 무한 대기(VideoReadyGate.PrepareAsync 미사용), `Level4BoardController.ApplyRowBasedDrawOrder` 매 스텝 List·Sort 할당(18번), IngredientSelectionController 분할 제안, `TutorialImageSlider.cs:55` 로거가 주입되는데도 Debug.LogError 사용(낮음).
  - 기각: TryGetComponent 금지(스킬 0번은 오히려 권장), ChildComponentFinder 위반(0번이 권장하는 직계 자식 순회 방식), ReadLoop 예외 미처리로 스레드 소멸(`RfidReaderService.cs:434` catch 있음), 전용 Thread·Thread.Sleep '높음'(블로킹 폴링이라 스레드 풀 대신 전용 스레드가 맞음, 형태 차이일 뿐), StoryLineAnimator·CommonSettingsProvider·GameLifetimeScope·TextHorizontalGradient의 Debug.Log(스킬 6번이 허용한 예외), 테스트 `WaitForSeconds(0.6f)`(고정 지연이라 무한 대기 아님), BaseFlowController 상속(씬마다 흐름이 달라 상속보다 작은 헬퍼 추출이 맞음).
  - 참고: 인용한 근거 코드 중 실제 파일과 다른 문구가 여러 건(TutorialImageSlider·TextHorizontalGradient·GameLifetimeScope)이라 줄 번호만 믿고 내용은 직접 확인함.
- 결과(Claude 자체 점검) — Antigravity가 놓친 것:
  - 버그 가능성: `KeyboardRfidSimulator`(숫자 1~4)·레벨 4 스페이스바가 빌드에서도 켜져 있음(3_Game에서 enabled, 빌드 분기 없음). 타이틀은 QR 스캐너를 키보드로 받으므로 게임 중 스캐너·키보드 입력이 가짜 카드 인식이 될 수 있음. `LevelSelectFlowController.cs:274` 두 참조가 모두 null이면 NRE(낮음). 결과 영상 종료 감지(WaitUntil/WaitWhile isPlaying)에 상한 없음(추정, 낮음).
  - 규칙: 최대 레벨 4가 `UnlockedLevelStore.MaxLevel`·`ResultFlowController.LastLevel`·`ResultVideoPanel.MaxLevel` 세 곳(12번, 주석은 "이 값만 올리면 됨"), `APIManager.cs:29` 영어 로그, `Tutorial5~7.png` 밉맵 켜짐(1~4는 꺼짐, 19번), `StoryLineAnimator` 수동 Lerp 루프(8번, 기존 코드), 테스트 4개 파일이 private 필드 리플렉션을 각자 작성(9·13번), `StoryLineAnimator.IsPointerPressedThisFrame` 미사용인데 같은 코드 3벌.
  - 리팩터링: 설정 폴백 값 이중 정의(설정 클래스 기본값과 컨트롤러 readonly 필드·`Constants.StoryLine`이 같은 값을 따로 가짐, 약 45개 필드·60곳 `?.??`. 현재 값은 모두 일치), IngredientSelectionController 분리(화살표 힌트·경고 연출·설계창, 레벨 1 추진력 값은 상태로, `_selectedLevel == 4` 분기 상태로), Level4BoardController 규칙/연출 분리·이동 방향 정의 2벌·판정 규칙 3벌·배치 풀 정적 캐시, MissionBoardController 레벨별 미션 문구 메서드 5벌, 설정 제공자 2개 동일 구조, Addressables 스프라이트 로드 중복.
- 반영: 없음. 사용자에게 보고 후 적용 범위를 정하기로 함.

### [2026-10-01 17:05] Claude → Antigravity · T15
- 변경 파일: T14(PR #34 결과 화면 AI 연출, 커밋 876849f·f7e7256) — `Game/UI/States/*LevelState.cs`(BuildSolution, 레벨 3 IsCorrectBlock), `Game/UI/Level4BoardController.cs`(경로 기록·FindShortestSolution), `Game/UI/IngredientSelectionController.cs`(StoreSolutionDesign), `Game/UI/MissionBoardController.cs`, `App/GameResultStore.cs`, `Result/ResultFlowController.cs`·`ResultAiPanel.cs`(신규)·`ResultVideoPanel.cs`·`ResultLifetimeScope.cs`, `Data/ResultSceneSettings.cs`, `StreamingAssets/Json/4_Result.json`, `Tests/Runtime/SolutionDesignTests.cs`(신규)
- 확인 요청: 코드·설계 리뷰만(사용자가 에디터를 쓰는 중이라 run_tests·Play 모드 금지, 셸 금지). (1) 정답이 실제 판정과 같은 규칙이고 입력 가능한지 (2) 레벨 4 솔버 경로 기록·최단성 (3) 정답 기록 시점 (4) 결과 씬 비동기 흐름(취소, 영상 준비 실패) (5) 로그 규칙 (6) 테스트 (7) 기획 의견. 모델 `gemini-3.1-pro-high`.
- 결과(Antigravity): 1·3·5·6 문제 없음, 2 낮음, 4 높음, 7 의견.
  - 2(낮음): DFS라 첫 경로가 최단이 아닐 수 있다고 지적 → **오해**. `FindShortestSolution`은 카드 1장부터 늘려 가는 반복 심화 탐색이라 처음 찾은 경로가 최소 카드임. 미반영.
  - 4(높음): 영상 파일이 없거나 준비에 실패하면 `WaitUntil(isPrepared)`가 끝나지 않음 → 수용.
  - 7 의견: 레벨 1 정답을 무작위로, 연출 시간 단축(코딩 3초·설계 4초), 설계창 제목 추가 → 제목은 씬에 이미 있음("AI 설계 완료", 씬 YAML을 읽지 않아 놓친 것). 나머지는 기획 판단이라 사용자에게 보고만 함.
- 결과(Claude 자체 리뷰): Antigravity 4와 같은 문제를 독립적으로 찾고 에디터에서 재현함(없는 파일로 Prepare 시 "WindowsMediaFoundation received empty file" 경고만 남고 `errorReceived` 없이 `isPrepared`가 계속 false). 추가로 Antigravity가 놓친 문제: `Settings.json` `resetTime`이 20초인데 결과 화면의 입력 없는 구간이 약 10초 → 약 32초로 늘어, 비활동 타이머를 켜면 완료 패널 전에 타이틀로 돌아감(높음).
- 반영(사용자 지시 포함):
  - 비활동 타이머: 결과 씬 입장부터 미션 결과 문구(컴플리트 패널)가 나올 때까지 `InactivityTimer.Pause()`, 이후·씬 파괴 시 `Resume()`.
  - 영상 준비: `VideoReadyGate.PrepareAsync`(오류 콜백 + 5초 타임아웃) 추가. 플레이어 결과 영상은 실패하면 바로 AI 연출로, AI 패널 영상은 실패하면 설계창을 둔 채 완료 패널로 넘어감. 두 수정이 함께 있어야 영상 실패 시 타이머가 멈춘 채 화면이 영구히 멈추는 일이 없음.
  - 컴파일 에러 0건. PlayMode 테스트는 사용자가 에디터를 쓰는 중이라 이번에는 돌리지 않음.
- 후속(에디터 사용 허락 후):
  - 사용자 지시로 Antigravity 7번 의견 중 "레벨 1 정답 무작위" 반영 — 정답 조합 전부를 모아 매번 하나를 무작위로 고름(달 2가지, 화성 8가지, 외계 행성 1가지). 테스트는 시드를 고정해 목적지마다 30번 뽑아 모두 성공하는지와 조합이 2가지 이상인 목적지에서 실제로 다른 조합이 나오는지 확인.
  - Play 모드 확인 중 `PrepareAsync`의 `CancelAfterSlim` 타이머를 해제하지 않아, 준비가 일찍 끝나면 5초 뒤 해제된 CTS를 Cancel해 `ObjectDisposedException`이 나는 것을 발견하고 고침(타이머 등록을 CTS보다 먼저 해제). Play 모드에서는 없는 파일에 `errorReceived`가 바로 와 0.0초에 false 반환함을 확인.
  - Play 모드로 결과 씬 확인: 설계가 한 줄씩 올라옴, 결과 씬 입장 시 타이머 정지 → 컴플리트 패널이 뜬 직후 재개. PlayMode 56/56 통과.

### [2026-10-01 16:06] Claude → Antigravity · T12
- 변경 파일: T11(PR #32 레벨 4 반복 필수화, 커밋 795985a·bc24bbd) — `App/Constants.cs`(Level4Board), `Game/UI/Level4BoardController.cs`(BuildPlacementPool·CanClearWithin·TryRun·Level4Layout), `Game/Data/RfidMappingValidator.cs`(레벨 4 단계 수·반복 3회 검사), `Tests/Runtime/Level4OutcomeEvaluationTests.cs`, `Tests/Runtime/RfidMappingDataTests.cs`
- 확인 요청: 코드·설계 리뷰만(사용자가 에디터를 쓰는 중이라 run_tests·Play 모드 금지, 셸 금지). (1) 솔버가 `EvaluateOutcome`·`BuildMoveSteps`와 같은 규칙인지 (2) 솔버의 풀이를 실제 게임에서 입력할 수 있는지(5번째 단계는 동작만, 반복하기 다음은 동작만) (3) 풀의 배치를 반복 없이 푸는 우회로 (4) 견고성·비용 (5) 검증기 누락 조건 (6) 테스트의 공허한 단언 여부 (7) 기획·체험 의견. 모델 `gemini-3.1-pro-high`(설계 판단).
- 결과(Antigravity): 1·2·3·6 문제 없음, 4 낮음, 5 중간, 7 의견.
  - 1 규칙 일치: `TryRun`이 그리드 밖·함정 즉시 실패, 반복 도중 칸마다 자원 수집, 기지 도착 시 즉시 결과로 `EvaluateOutcome`과 같음.
  - 2 입력 가능성: 솔버는 반복하기를 항상 이동하기와 2장 묶음으로 놓아 5번째 칸에 반복하기가 오지 않으므로 실제 입력 가능.
  - 3 우회로: 이동만으로 5장 안에 풀리는 배치는 풀에서 빠지고, 2장으로 3칸을 가는 것은 반복 3회뿐이라 우회로 없음.
  - 4 견고성(낮음): 풀이 비면 로그만 남기고 반환함 → 폴백 배치 권장. 탐색 비용은 수 ms.
  - 5 검증기(중간): 단계별 categories에 '제어'가 있는지 검사하지 않아, JSON에서 제어를 빼면 반복이 필요한 배치를 못 푸는 상태가 됨.
  - 6 테스트: 실제 `EvaluateOutcome`으로 모든 조합을 따지므로 공허한 단언 없음.
  - 7 의견: 이동 카드만으로는 항상 실패하므로 관람객 안내(힌트 문구)가 충분한지 기획 확인 필요.
- 결과(Claude 자체 리뷰): 1·2·3·6에 동의. 같은 5번 누락을 독립적으로 찾음. 추가로 풀 66가지 중 54가지는 반복 3회 1장, 12가지는 반복 3회 2장(카드 5장 모두)이 필요해 난이도가 꽤 오름(독립 구현한 Python 탐색으로 66가지·54/12 교차 확인).
- 반영: 5번 수용 — 검증기에 "모든 단계가 동작 카드를, 마지막을 뺀 모든 단계가 제어 카드를 받아야 함" 검사와 테스트 추가, PlayMode 49/49 통과. 4번 미반영 — 풀은 상수(4x4, 5장, 반복 3회)로만 정해지고 테스트가 비어 있지 않음(66가지)을 보장하므로 일어날 수 없는 경우의 폴백은 두지 않음(AGENTS.md "No error handling for impossible scenarios"). 7번과 반복 2장 배치 비율은 기획 판단이라 사용자에게 보고만 함.

- 변경 파일: T9(PR #31 리뷰 지적 14건 반영, 커밋 1659467~6b7e1b7)
  - 코드: `App/Constants.cs`, `App/PanelFader.cs`(신규), `App/PlaceholderFormatter.cs`(신규), `App/VisitorInfoProvider.cs`, `Data/LevelSelectSceneSettings.cs`, `Game/Data/RfidMappingData.cs`, `Game/Data/RfidMappingValidator.cs`(신규), `Game/GameFlowController.cs`, `Game/UI/IngredientSelectionController.cs`, `Game/UI/MissionBoardController.cs`, `Game/UI/States/IngredientLevel3State.cs`, `Intro/IntroFlowController.cs`, `Outro/OutroStoryController.cs`, `LevelSelect/LevelSelectFlowController.cs`, `Result/ResultFlowController.cs`
  - 데이터·씬: `StreamingAssets/RfidMappings.json`, `StreamingAssets/Json/2_LevelSelect.json`, `Data/Level1.asset`, `Scenes/3_Game.unity`
  - 테스트: `IngredientFsmStateTests.cs`, `RfidMappingDataTests.cs`(신규), `PlaceholderFormatterTests.cs`(신규)
- 확인 요청: (1) 콘솔 컴파일 에러 0건 (2) PlayMode `DGAIZone.Tests` 46개 전부 통과 (3) 주요 스크립트 7개 `validate_script` (4) 정적 점검 — "체험자" 리터럴이 `Constants.DefaultVisitorName` 한 곳뿐인지, 패널 헬퍼 3종이 인트로에만 남고 나머지는 `PanelFader`를 쓰는지, 난이도 고아 코드 잔여 여부, `RfidMappings.json`의 `matterSetId` 참조·블록 id 중복·레벨 2 순서·레벨 1/3 value, 레벨 3이 `ingredientId`로 분기하는지, 3_Game `debugStartLevel`=0·미션 보드 `levelDataList` 제거, Level1 `{distance|이에요}` (5) 이름 후반영·`matterSetId` 해석·검증기·`PanelFader` 로그 태그 코드 리뷰. 모델 `gemini-3.8-flash-medium`.
- 결과: **전 항목 통과**
  - 1 콘솔: `error CS` 0건.
  - 2 테스트: PlayMode 46/46 통과(1.80초).
  - 3 `validate_script`: 7개 파일 모두 에러·경고 0건.
  - 4a~4g 정적 점검: 모두 통과. "체험자"는 `Constants.cs:31`에만 리터럴로 있고 나머지는 주석, 헬퍼 정의는 `IntroFlowController.cs:283/299`에만 있음, 난이도 관련 잔여 0건, `matterSetId` 10종 모두 같은 레벨 `matterSets`에 존재·중복 0건·레벨 2 순서 일치·value 모두 > 0, `IngredientLevel3State.cs`가 `ingredientId`로 분기, `3_Game.unity`의 `debugStartLevel: 0`이고 미션 보드에 `levelDataList` 없음(GameFlowController 5개는 유지), `Level1.asset:23`에 옛 `{이에요}` 없음.
  - 5 코드 리뷰: 발견 없음(이름 후반영은 원본 템플릿 기준, 레벨 2는 같은 `ingredientId`라 확정 블록 제외 후 섞기 정상, 로그 태그는 `typeof(T).Name`).
  - 참고: 첫 호출은 Antigravity가 셸 명령(command 권한, 허용 목록에 없음)을 쓰려다 헤드리스 모드에서 자동 거부되어 결과 없이 끝남. "셸 명령 금지, 파일 읽기·`find_in_file`만 사용"을 요청에 명시해 다시 호출함. 권한 설정은 바꾸지 않음.
  - 참고: 플레이 모드·테스트 실행 중 TMP 동적 폰트 아틀라스(`GamtanRoadTantan SDF.asset`)에 글리프가 추가돼 작업 폴더에 변경으로 남았으며, 런타임 부산물이라 커밋하지 않음.
