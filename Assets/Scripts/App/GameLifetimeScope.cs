using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;
using VContainer.Unity;
using Wonjeong.App;

namespace DGAIZone.App
{
    /// <summary>
    /// 게임 전역 LifetimeScope. 템플릿의 RootLifetimeScope가 구성하는 전역 로깅, MessagePipe, Core(SystemCanvas, GameCloser), Optional(FadeManager 등) 컴포넌트를 그대로 사용하고,
    /// 게임 매니저와 씬 전환 서비스 등 프로젝트 고유 컴포넌트를 컨테이너에 등록함.
    /// <para>
    /// VContainerSettings(프리로드 에셋) 기반 자동 루트 스코프 생성 대신, 0_Title 씬에 직접 배치한
    /// 인스턴스를 사용함. 빌드된 스탠드얼론 플레이어에서는 프리로드 에셋이 BeforeSceneLoad
    /// 시점까지도 메모리에 로드되지 않는 경우가 확인되어(에디터는 AssetDatabase를 통해 즉시
    /// 접근 가능하므로 재현되지 않음), VContainerSettings.Instance에 의존하는 자동 부트스트랩은
    /// 빌드에서 신뢰할 수 없다. 대신 DontDestroyOnLoad로 씬 전환 후에도 유지시키며, 각 씬
    /// LifetimeScope는 FindParent()에서 ResolveAndEnsureBuilt()로 이 인스턴스를 직접 찾아 부모로 연결한다.
    /// <br/>
    /// 0_Title은 아웃트로의 종료하기 버튼이나 비활동 타임아웃으로 반복해서 재로드될 수 있는데(Single 모드),
    /// 그때마다 씬 파일에 저장된 이 프리팹 인스턴스가 다시 생성되어 기존 DontDestroyOnLoad 인스턴스와
    /// 중복될 수 있다. 정적 Instance로 원본을 고정하고, 이후 재로드분은 즉시 비활성화 후 파괴한다.
    /// <br/>
    /// 주의: 이전 버전에서는 "정적 bool 플래그 + Find&lt;GameLifetimeScope&gt;()"로 중복을 걸렀으나, Awake 실행
    /// 순서가 보장되지 않는 탓에(DefaultExecutionOrder는 힌트일 뿐 강제가 아님) 다음 레이스가 실제로 발생했다:
    /// 0_Title 재로드로 생긴 "곧 자멸할 중복 인스턴스"가 자기 Awake(비활성화+파괴)를 실행하기 *전에*, 같은 프레임의
    /// TitleLifetimeScope.FindParent()가 Find&lt;GameLifetimeScope&gt;()로 그 중복을 먼저 찾아 Container == null이라고
    /// 판단해 강제 Build()해버림. 그러면 TitleFlowController 등은 이 "버려질 중복 컨테이너"의 UnlockedLevelStore 등을
    /// 주입받고, 진짜 영속 인스턴스의 상태(레벨 진행도 등)는 계속 방치되어 두 인스턴스의 상태가 갈라진다
    /// (레벨 선택 화면에서 초기화됐어야 할 레벨 잠금이 풀린 채로 보이는 버그로 나타남).
    /// 지금은 자식 스코프가 Find&lt;T&gt;() 대신 정적 Instance(ResolveAndEnsureBuilt)만 사용하므로, 중복 인스턴스가
    /// 잠깐 살아있더라도 "원본이 아니면" 아예 후보가 되지 않아 이 레이스가 원천적으로 불가능하다.
    /// </para>
    /// </summary>
    public class GameLifetimeScope : RootLifetimeScope
    {
        /// <summary> 씬 재로드로 중복 생성된 인스턴스가 아닌, 최초로 살아남은 진짜 영속 인스턴스. </summary>
        public static GameLifetimeScope Instance { get; private set; }

        /// <summary>
        /// 각 씬 LifetimeScope의 FindParent()가 공용으로 호출함. Instance가 아직 비어 있으면(자신의 Awake보다
        /// 먼저 호출된 0_Title 최초 콜드 스타트 상황) 씬에서 직접 찾아 Instance로 확정한 뒤, 아직 빌드되지 않았다면
        /// (Container == null) 그 자리에서 Build()해 부모로 쓸 수 있게 함. Instance는 항상 원본만 가리키므로
        /// (씬 재로드로 생긴 중복은 Awake에서 즉시 비활성화·파괴되어 절대 Instance가 되지 않음) 이 메서드가 중복
        /// 인스턴스를 잘못 확정하거나 이중으로 Build()할 위험이 없다.
        /// </summary>
        public static GameLifetimeScope ResolveAndEnsureBuilt()
        {
            if (Instance == null) Instance = Find<GameLifetimeScope>() as GameLifetimeScope;
            if (Instance != null && Instance.Container == null) Instance.Build();
            return Instance;
        }

        /// <summary>
        /// 0_Title 재로드로 생성된 중복 인스턴스라면(Instance가 이미 다른 원본을 가리키고 있다면) 즉시
        /// 비활성화 후 파괴해 원본만 유지함. 원본이라면 Instance로 자신을 확정하고, 씬 전환 후에도 컨테이너가
        /// 유지되도록 파괴되지 않게 하며, ResolveAndEnsureBuilt()가 이미 강제로 Build()해 두었다면
        /// (Container != null) 재빌드를 건너뜀.
        /// </summary>
        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            DontDestroyOnLoad(gameObject);
            if (Container != null) return;
            base.Awake();
        }

        /// <summary>
        /// 템플릿 기본 구성을 먼저 적용한 뒤 게임 매니저, 씬 전환 서비스, 게임 결과 저장소를 등록함.
        /// </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponentInHierarchy<GameManager>();
            builder.Register<SceneTransitionService>(Lifetime.Singleton);
            builder.Register<GameResultStore>(Lifetime.Singleton);
            builder.Register<SelectedLevelStore>(Lifetime.Singleton);
            builder.Register<UnlockedLevelStore>(Lifetime.Singleton);
            builder.Register<VisitorInfoProvider>(Lifetime.Singleton);

            RegisterTmpFonts();
        }

        /// <summary>
        /// Addressables로 관리하는 TMP 폰트를 MaterialReferenceManager 캐시에 미리 등록한다.
        /// TMP의 &lt;font="..."&gt; 태그는 이 캐시를 먼저 조회하고, 없으면 Resources에서만 폰트를 찾는다.
        /// 등록해 두지 않으면 태그가 해석되지 않고 문자열 그대로 화면에 출력된다.
        /// 첫 씬이 그려지기 전에 끝나야 하므로 동기 로드한다.
        /// </summary>
        private static void RegisterTmpFonts()
        {
            try
            {
                IList<TMP_FontAsset> fonts = Addressables
                    .LoadAssetsAsync<TMP_FontAsset>(Constants.ResourcePaths.TmpFontLabel, null)
                    .WaitForCompletion();

                if (fonts is null) return;

                foreach (TMP_FontAsset font in fonts)
                    if (font) MaterialReferenceManager.AddFontAsset(font);
            }
            catch (Exception ex)
            {
                // 폰트 등록 실패는 치명적이지 않다 — 태그가 해석되지 않을 뿐이므로 부팅은 계속 진행
                Debug.LogWarning($"[GameLifetimeScope] TMP 폰트 등록 실패: {ex.Message}");
            }
        }
    }
}
