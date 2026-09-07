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

## 6. 2_LevelSelect 레벨 버튼 선택 연출 (스토리 위치 이동 연출)

- Zone1: 레벨 버튼 클릭 시, 해당 버튼을 두 패널의 공통 부모(`Image_Window3`)로 옮겨(`worldPositionStays: false`)
  패널 페이드 알파 영향에서 분리하고, 하위 별 아이콘(`Image_StarN`)을 숨긴 뒤 스토리 패널 페이드인과
  동시에 `SelectedLevelButtonPosition`(`(-932f, 224f)`)으로 `Ease.OutBack` 트윈 이동.
- Zone4 기존: 클릭된 버튼의 스프라이트를 `Image_Story`에 복사하고 제자리에서 단순히 스프라이트만 교체.
- Zone4 변경: Zone1 방식으로 일체화.
  - `Start()`에서 기존 `storyImage` 플레이스홀더 오브젝트를 비활성화.
  - 레벨 버튼 클릭 시 해당 버튼을 `storyPanel.transform.parent`로 계층 분리, 인터랙션 비활성화, 자식(별 등) 숨김 처리.
  - `storyPanel` 페이드인 비동기 시퀀스 중에 `DOAnchorPos(targetPos, duration).SetEase(Ease.OutBack, overshoot)` 동시 실행.
  - 연출 파라미터(`selectedLevelButtonMoveDuration: 1.0`, `selectedLevelButtonMoveOvershoot: 1.3`)를
    `LevelSelectSceneSettings` 및 `StreamingAssets/Json/2_LevelSelect.json`에 외부화하여 재빌드 없이 튜닝 가능하도록 구성.

