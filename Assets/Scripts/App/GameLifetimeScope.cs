using System;
using System.Collections.Generic;
using TMPro;
using DGAIZone.Data;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;
using VContainer.Unity;
using HuliacDev.App;

namespace DGAIZone.App
{
    /// <summary>
    /// 게임 전역 LifetimeScope. 템플릿의 RootLifetimeScope가 구성하는 전역 로깅, MessagePipe, Core(SystemCanvas, GameCloser), Optional(FadeManager 등) 컴포넌트를 그대로 사용하고,
    /// 게임 매니저와 씬 전환 서비스 등 프로젝트 고유 컴포넌트를 컨테이너에 등록함.
    /// <para>
    /// 이 프리팹은 씬에 배치하지 않고 VContainerSettings(프리로드 에셋)의 RootLifetimeScope로 지정해 쓴다.
    /// 앱 실행 중 한 번만 생성되고 DontDestroyOnLoad로 씬 전환 후에도 유지된다. VContainerSettings는 첫 씬의
    /// sceneLoaded 콜백에서 루트를 자동 생성하는데, 이 콜백은 씬 오브젝트의 Awake 이후에 오므로 앱 시작 직후에는
    /// 0_Title의 TitleLifetimeScope가 먼저 부모를 찾게 된다. 그래서 각 씬 LifetimeScope는 FindParent()에서
    /// ResolveAndEnsureBuilt()로 루트의 생성·빌드를 보장받은 뒤 부모로 연결한다.
    /// <br/>
    /// 주의: VContainerSettings.asset의 스크립트 참조(m_Script)가 끊겨도 에디터는 m_EditorClassIdentifier로
    /// 타입을 복구해 정상 동작하지만, 빌드에서는 VContainerSettings.Instance가 null이 되어 루트가 생성되지 않는다.
    /// 그러면 씬 스코프가 부모 없이 빌드되어 SceneTransitionService 등 루트 등록 타입의 주입이 실패한다.
    /// 빌드 Player.log에 "The referenced script on this Behaviour ... is missing!" 경고가 보이면 이 참조부터 확인할 것
    /// (과거 이 증상을 "빌드에서는 프리로드 에셋이 늦게 로드된다"로 오진해 씬 배치 방식으로 우회한 적이 있음).
    /// <br/>
    /// 정적 Instance로 최초 원본을 고정하고, 혹시 중복 인스턴스가 생기면 Awake에서 즉시 비활성화 후 파괴한다.
    /// 자식 스코프는 Find&lt;T&gt;()보다 정적 Instance를 우선 사용하는 ResolveAndEnsureBuilt()를 거치므로,
    /// 파괴 예정인 중복 인스턴스를 부모로 삼아 상태(레벨 진행도 등)가 갈라지는 레이스를 피한다
    /// (씬 배치 방식이던 이전 버전에서 0_Title 재로드 시 실제로 발생했던 문제).
    /// </para>
    /// </summary>
    public class GameLifetimeScope : RootLifetimeScope
    {
        /// <summary> 씬 재로드로 중복 생성된 인스턴스가 아닌, 최초로 살아남은 진짜 영속 인스턴스. </summary>
        public static GameLifetimeScope Instance { get; private set; }

        private static readonly ProfilerMarker RegisterTmpFontsMarker = new ProfilerMarker("GameLifetimeScope.RegisterTmpFonts");

        /// <summary>
        /// 루트 스코프 인스턴스를 반환함. 아직 없으면(앱 시작 직후 VContainerSettings의 자동 생성보다 씬 스코프가
        /// 먼저 호출한 경우) VContainerSettings의 RootLifetimeScope 프리팹으로 생성하고, 컨테이너가 빌드되지 않은
        /// 경우 Build()를 보장함.
        /// </summary>
        public static GameLifetimeScope ResolveAndEnsureBuilt()
        {
            if (!Instance) Instance = Find<GameLifetimeScope>() as GameLifetimeScope;
            if (!Instance)
            {
                VContainerSettings settings = VContainerSettings.Instance;
#if UNITY_EDITOR
                if (!settings)
                {
                    settings = UnityEditor.AssetDatabase.LoadAssetAtPath<VContainerSettings>("Assets/Settings/VContainerSettings.asset");
                }
#endif
                if (settings && settings.RootLifetimeScope)
                {
                    if (Application.isPlaying)
                    {
                        LifetimeScope root = settings.GetOrCreateRootLifetimeScopeInstance();
                        Instance = root as GameLifetimeScope;
                    }
                    else
                    {
                        Instance = Instantiate(settings.RootLifetimeScope) as GameLifetimeScope;
                    }
                }
            }
            if (Instance && Instance.Container == null) Instance.Build();
            return Instance;
        }

        /// <summary>
        /// 중복 생성된 인스턴스라면(Instance가 이미 다른 원본을 가리키고 있다면) 즉시
        /// 비활성화 후 파괴해 원본만 유지함. 원본이라면 Instance로 자신을 확정하고, 씬 전환 후에도 컨테이너가
        /// 유지되도록 파괴되지 않게 하며, ResolveAndEnsureBuilt()가 이미 강제로 Build()해 두었다면
        /// (Container != null) 재빌드를 건너뜀.
        /// </summary>
        protected override void Awake()
        {
            if (Instance && Instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            if (Container != null) return;
            base.Awake();
        }

        /// <summary>
        /// 템플릿 기본 구성을 먼저 적용한 뒤 게임 매니저, 씬 전환 서비스, 게임 결과·레벨·관리자 레벨 이동 저장소, 체험자 설정(SO)을 등록하고
        /// 템플릿 디버그 단축키를 Ctrl 조합으로 바꿈.
        /// </summary>
        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.RegisterComponentInHierarchy<GameManager>();
            builder.Register<SceneTransitionService>(Lifetime.Singleton);
            builder.Register<GameResultStore>(Lifetime.Singleton);
            builder.Register<SelectedLevelStore>(Lifetime.Singleton);
            builder.Register<UnlockedLevelStore>(Lifetime.Singleton);
            builder.Register<AdminLevelJumpStore>(Lifetime.Singleton);
            builder.Register<VisitorInfoProvider>(Lifetime.Singleton);

            // 운영 모드·체험자 이름 — 관리자 화면에서 바꾼 값은 PlayerPrefs에 남아 있어 재부팅 후에도 유지됨
            builder.RegisterInstance(LoadVisitorSettings());

            // 템플릿 디버그 단축키(D·I·M)를 Ctrl 조합으로 — QR 스캐너가 입력하는 uid 문자와 겹치지 않게.
            // GameManagerBase가 주입받는 것과 같은 싱글톤 인스턴스라 그대로 반영됨
            builder.RegisterBuildCallback(container =>
                DebugShortcutBindings.Apply(container.Resolve<TemplateInputActions>()));

            using (RegisterTmpFontsMarker.Auto())
            {
                RegisterTmpFonts();
            }
        }

        /// <summary>
        /// 체험자 설정(VisitorSettings SO)을 Addressables로 불러옴. Configure는 동기 실행이라 WaitForCompletion으로 동기 로드함.
        /// 불러오지 못하면 에셋 기본값(로컬 모드, '체험자')과 같은 임시 인스턴스를 써서 부팅은 이어 가되 에러를 남김
        /// (관리자 화면에서 바꾼 PlayerPrefs 값은 그대로 읽힘).
        /// </summary>
        private static VisitorSettings LoadVisitorSettings()
        {
            try
            {
                VisitorSettings settings = Addressables.LoadAssetAsync<VisitorSettings>(Constants.ResourcePaths.VisitorSettingsKey).WaitForCompletion();
                if (settings) return settings;
            }
            catch (Exception ex)
            {
                // 컨테이너 구성 도중이라 로거를 아직 주입받을 수 없어 Debug로 대체 출력함
                Debug.LogError($"[GameLifetimeScope] VisitorSettings 로드 중 예외: {ex.Message}");
            }

            Debug.LogError($"[GameLifetimeScope] Addressables 주소 '{Constants.ResourcePaths.VisitorSettingsKey}'의 VisitorSettings를 불러오지 못해 기본값으로 대체함.");
            return ScriptableObject.CreateInstance<VisitorSettings>();
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
                // 폰트 등록 실패는 치명적이지 않다 — 태그가 해석되지 않을 뿐이므로 부팅은 계속 진행.
                // 컨테이너 구성 도중이라 로거를 아직 주입받을 수 없어 Debug로 대체 출력함
                Debug.LogWarning($"[GameLifetimeScope] TMP 폰트 등록 실패: {ex.Message}");
            }
        }
    }
}
