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
    /// LifetimeScope는 FindParent()로 이 인스턴스를 직접 찾아 부모로 연결한다.
    /// <br/>
    /// Awake 실행 순서는 보장되지 않으므로([DefaultExecutionOrder]는 힌트일 뿐 강제가 아님),
    /// 자식 스코프의 FindParent()가 이 인스턴스를 먼저 발견해 Container == null인 상태로
    /// 강제 Build()할 수 있다. 그런 경우를 대비해 이 클래스의 Awake()는 이미 빌드되어 있으면
    /// (Container != null) 재빌드를 건너뛴다.
    /// <br/>
    /// 0_Title은 아웃트로의 종료하기 버튼이나 비활동 타임아웃으로 반복해서 재로드될 수 있는데(Single 모드),
    /// 그때마다 씬 파일에 저장된 이 프리팹 인스턴스가 다시 생성되어 기존 DontDestroyOnLoad 인스턴스와
    /// 중복될 수 있다. 정적 플래그로 최초 1회만 생존시키고, 이후 재로드분은 즉시 비활성화 후 파괴한다
    /// (그대로 두면 별도 컨테이너를 빌드해 GameManager/ShutdownScheduler 등이 중복되고, 새로 생성된
    /// 자식들은 아무도 주입해주지 않아 "Dependencies were not injected" 경고가 발생한다).
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameLifetimeScope : RootLifetimeScope
    {
        private static bool created;

        /// <summary>
        /// 0_Title 재로드로 생성된 중복 인스턴스라면 즉시 비활성화 후 파괴해 원본만 유지함.
        /// 최초 인스턴스라면 씬 전환 후에도 컨테이너가 유지되도록 파괴되지 않게 하고,
        /// 자식 스코프가 이미 강제로 Build()해 두었다면(Container != null) 재빌드를 건너뜀.
        /// </summary>
        protected override void Awake()
        {
            if (created)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            created = true;

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
