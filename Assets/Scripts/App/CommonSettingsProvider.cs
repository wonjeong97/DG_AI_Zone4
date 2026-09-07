using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.Data;
using Wonjeong.Utils;

namespace DGAIZone.App
{
    /// <summary>
    /// StreamingAssets/Json/00_Common.json(CommonSettings)을 최초 1회만 로드해 앱 전역에서 공유하는 정적 유틸리티.
    /// 여러 씬 컨트롤러가 같은 공통 연출 타이밍(씬 전환 페이드, 패널 페이드, 스토리 라인 파라미터)을 중복 로드하지 않도록 함.
    /// </summary>
    public static class CommonSettingsProvider
    {
        // 값만 캐싱하고, 로드 완료 여부는 폴링(UniTask.WaitUntil)으로 기다림 — UniTask.Preserve()는 로드가 끝나기 전에
        // 2개 이상의 호출자가 동시에(같은 프레임에) await를 걸면 내부적으로 "Already continuation registered" 예외를
        // 던지는 문제가 있어(3_Game처럼 여러 컴포넌트가 동시에 GetAsync를 호출하는 씬에서 실제로 발생함) 쓰지 않음.
        private static CommonSettings _cached;
        private static bool _isLoaded;
        private static bool _isLoadStarted;

        /// <summary>
        /// 00_Common.json을 로드하여 반환함. 이미 로드를 시작했다면(완료 여부 무관) 그 결과를 그대로 공유함.
        /// 여러 호출자가 동시에 불러도 안전함(로드 자체는 한 번만 실행됨).
        /// </summary>
        public static async UniTask<CommonSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            if (!_isLoadStarted)
            {
                _isLoadStarted = true;
                LoadAndCacheAsync().Forget();
            }

            await UniTask.WaitUntil(() => _isLoaded, cancellationToken: cancellationToken);
            return _cached;
        }

        private static async UniTaskVoid LoadAndCacheAsync()
        {
            _cached = await JsonLoader.LoadAsync<CommonSettings>(
                $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.ResourcePaths.CommonSettingsFileName}");
            _isLoaded = true;
        }
    }
}
