# Zone1 ↔ Zone4 연출/컨벤션 정리

DG_AI_Zone4는 DG_AI_Zone1보다 나중에 만들어진 프로젝트라, 연출과 아키텍처 컨벤션을 Zone1에
맞추는 작업을 진행 중임. 이 문서는 그 과정에서 무엇을 맞췄고, 어떤 차이/버그를 발견해
Zone4에서 고쳤는지 정리함. **Zone1을 다음에 작업할 때 여기 나온 항목들을 Zone1에도
동일하게 반영할 것.**

## 1. 씬 전환 (페이드아웃 → 씬 로드 → 페이드인)

완전히 동일함. 둘 다 템플릿의 `Wonjeong.UI.FadeManager`를 그대로 사용하고, 그 안에서
`Ease.Linear` + `SetUpdate(true)`(Time.timeScale 무관)로 이미 구현돼 있어 손댈 것 없음.

지속 시간은 양쪽 다 `00_Common.json`류의 공통 설정(`sceneTransitionFadeDuration`)에서
가져오도록 통일함(Zone4는 이번 세션에서 Title/Outro까지 포함해 전체 씬이 이 값 하나를
공유하도록 정리함).

## 2. 패널 전환 (같은 씬 안에서 패널끼리 전환)

**컨벤션: 항상 순차 페이드(먼저 것을 완전히 페이드아웃한 뒤 다음 것을 페이드인)로 통일.
동시 크로스페이드는 쓰지 않음.**

- Zone4: 모든 패널 전환(Intro→Tutorial, LevelSelect→Story, Game↔Story, Result→Complete)이
  순차 페이드. 이징은 `Ease.Linear` + `SetUpdate(true)`로 씬 전환과 동일하게 맞춤(기존엔
  `Ease.InOutQuad`였고 timeScale 영향을 받았음 — 이번에 수정).
- Zone1: 대부분 순차 페이드이지만 **`ResultSequence.cs`의 결과 패널→컴플리트 패널 전환
  한 곳만** `SceneFader.CrossFadeGroupsAsync`로 동시 크로스페이드를 씀. 나머지는 이미
  순차 페이드(`FadeCanvasGroupAsync`를 두 번 호출).
- **TODO (Zone1 작업 시)**: `ResultSequence.cs`의 그 한 곳도 순차 페이드로 바꿔서
  일관성을 맞출 것. `SceneFader.CrossFadeGroupsAsync`는 이후 사용처가 없어지면 제거 검토.

## 3. 타이틀 QR 블링크 + 서버 연동 표시

Zone1의 `TitleSceneManager.ApplyQrVisibilityAsync`를 Zone4의 `TitleFlowController`에
그대로 이식함. `Visitor.json`의 `isServerConnected`에 따라 QR을 표시/숨김하고,
표시할 때만 알파 1~`qrBlinkMinAlpha` 사이를 `InOutSine` 이징으로 무한 Yoyo 반복 페이드.
타이밍 값은 `0_Title.json`(`qrFadeDuration`, `qrBlinkMinAlpha`)에서 읽음.

이식하면서 원본(Zone1)에도 있던 문제 2가지를 Zone4에서 발견해 고침 — **Zone1에도
동일하게 적용할 것**:

1. **취소 예외 처리 누락**: `ApplyQrVisibilityAsync`가 `async UniTaskVoid`인데
   `try/catch(OperationCanceledException)`가 없어서, 타이틀 씬 진입 직후 바로 다음 씬으로
   전환되면(오브젝트 파괴로 토큰 취소) 처리되지 않은 예외가 콘솔에 에러로 찍힐 수 있음.
   → 전체를 try/catch로 감싸고 catch에서 조용히 종료하도록 수정.
2. **QR 초기 플리커**: QR 오브젝트가 씬에서 기본 활성 상태라, 서버 연동 여부를 비동기로
   확인하는 동안(첫 1~2프레임) QR이 잠깐 보였다 꺼지는 플리커가 있었음.
   → `Start()`에서 확인 시작 전에 먼저 `SetActive(false)`로 숨겨두고, 확인 결과에 따라
   다시 켜도록 수정.

## 4. TMP 폰트 / Addressables 컨벤션

폰트를 `Assets/AddressableAssets/Fonts/`에 두고 Addressables로 등록(`TMPFont` 라벨),
`GameLifetimeScope` 부팅 시 그 라벨로 전부 로드해 `MaterialReferenceManager`에 등록해
`<font="...">` 태그가 해석되도록 하는 구조는 동일하게 맞춤.

**주의**: Zone1은 TextMeshPro **4.0.0-pre.2**, Zone4는 **3.0.9**를 써서 폰트 에셋의
실제 직렬화 타입이 다름(`UnityEngine.TextCore.Text.FontAsset` vs `TMPro.TMP_FontAsset`).
**폰트 에셋 파일을 프로젝트 간에 그대로 복사하면 안 되고**, 원본 ttf에서 각 프로젝트의
TMP 버전으로 새로 구워야 함.

## 5. 씬 연출값 JSON 외부화 (Zone4 전용, 아직 Zone1엔 없음)

Zone4는 이번 세션에서 페이드 시간·딜레이·조작 감도 등을 `StreamingAssets/Json/`으로
분리해 재빌드 없이 현장에서 조정 가능하게 함(`CommonSettings` + 씬별 Settings 클래스 +
`CommonSettingsProvider`/`GameSceneSettingsProvider`로 중복 로드 방지). Zone1은 아직
값이 코드/인스펙터에 하드코딩되어 있음. 현장 운영 편의성을 위해 Zone1에도 같은 구조를
적용할지는 별도 논의 필요(이 세션 범위 밖).

## 6. 아웃트로 화면 종료 및 타임아웃 시 idle 로그 전송 정책 (Zone4 개선, Zone1 반영 필요)

전시/체험 공간 특성상 마지막 씬(아웃트로)에 도달한 체험자는 이미 모든 콘텐츠를 정상적으로 끝까지 관람한 상태임. 따라서 타이틀(대기 화면)로 복귀할 때 서버 통계에 정상 완료(`move_idle`)로 집계되어야 함.

- **기존 문제점 (기존 Zone1/Zone4 공통)**:
  1. 사용자가 아웃트로 화면에서 "처음으로" 버튼을 직접 눌러 복귀할 때, `MoveIdleEvent`가 발행되지 않아 서버에 `move_idle` 로그가 누락됨.
  2. 체험을 마친 사용자가 버튼을 누르지 않고 그냥 자리를 떠서 `InactivityTimer` 타임아웃으로 복귀할 때, `ApiManagerBase`의 기본 동작에 의해 "체험 중도 포기/이탈(`move_idle_timeout`)"로 잘못 전송됨.

- **Zone4 개선 적용 내용**:
  1. **홈 버튼 클릭 시 `move_idle` 전송**: [`OutroFlowController.cs`](file:///d:/HULIAC/HULIAC_Project/DG_AI_Zone4/Assets/Scripts/Outro/OutroFlowController.cs)에서 `IPublisher<MoveIdleEvent>`를 주입받아, 홈 버튼 클릭 시 `_moveIdlePublisher.Publish(new MoveIdleEvent())`를 호출.
  2. **아웃트로 타임아웃 시 한정으로 `move_idle` 전송**: [`APIManager.cs`](file:///d:/HULIAC/HULIAC_Project/DG_AI_Zone4/Assets/Scripts/Network/APIManager.cs)에서 `InactivityTimeoutEvent` 구독을 오버라이드하여, 현재 활성 씬이 `Constants.Scenes.Outro`(`5_Outro`)인 경우 `SendMoveIdleLogAsync()`(`move_idle`)를 전송하고, 그 외 씬(인트로/레벨선택/게임 등)에서의 타임아웃만 기존처럼 `SendMoveIdleTimeoutLogAsync()`(`move_idle_timeout`)를 전송하도록 분기.

- **TODO (Zone1 작업 시 반영할 내용)**:
  1. Zone1의 `OutroSceneManager.cs`에 `IPublisher<MoveIdleEvent>`를 주입받아, `OnEndButtonClicked`에서 `_moveIdlePublisher.Publish(new MoveIdleEvent())`를 호출하도록 수정.
  2. Zone1의 `Assets/Scripts/Network/APIManager.cs`에도 활성 씬 분기 로직을 적용하여, 아웃트로 씬에서 타임아웃 발생 시 `move_idle`이 전송되도록 수정.


