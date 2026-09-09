using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// </summary>
    public class GameLifetimeScope : RootLifetimeScope
    {
        /// <summary>
        /// 빌드 환경에서 VContainerSettings(프리로드 에셋)의 OnEnable 호출 시점이 0_Title의
        /// TitleLifetimeScope.Awake보다 늦어지는 경우를 대비한 안전장치.
        /// VContainerSettings는 에디터에서만 RuntimeInitializeOnLoadMethod로 프리로드 에셋을 강제
        /// 로드하며(내부 주석: "For editor, we need to load the Preload asset manually"), 빌드에서는
        /// 엔진의 프리로드 타이밍에 전적으로 의존한다. 이 타이밍이 첫 씬 로드보다 늦어지면
        /// VContainerSettings.Instance가 null인 채로 자식 LifetimeScope가 Build되어 부모 없이
        /// 독립 컨테이너로 구성되고, SceneTransitionService 등 루트 등록 타입 해석이 실패한다.
        /// 여기서 BeforeSceneLoad 시점에 프리로드 에셋을 직접 찾아 OnEnable을 강제 호출해
        /// 루트 스코프가 첫 씬의 Awake보다 반드시 먼저 생성되도록 보장한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRootLifetimeScopeBootstrapped()
        {
            if (VContainerSettings.Instance != null) return;

            VContainerSettings[] settingsAssets = Resources.FindObjectsOfTypeAll<VContainerSettings>();
            if (settingsAssets.Length == 0)
            {
                Debug.LogWarning("[GameLifetimeScope] VContainerSettings 프리로드 에셋을 찾을 수 없어 루트 스코프 강제 부트스트랩을 건너뜀.");
                return;
            }

            MethodInfo onEnable = typeof(VContainerSettings).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance);
            if (onEnable == null)
            {
                Debug.LogWarning("[GameLifetimeScope] VContainerSettings.OnEnable을 찾을 수 없어 루트 스코프 강제 부트스트랩을 건너뜀.");
                return;
            }

            onEnable.Invoke(settingsAssets[0], null);
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
