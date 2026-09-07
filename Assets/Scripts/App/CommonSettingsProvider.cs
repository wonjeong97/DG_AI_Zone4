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
        // Preserve()로 여러 번 await 가능한 공유 UniTask로 캐싱함 — 값(CommonSettings)만 캐싱하면 로드가
        // 끝나기 전에 여러 컴포넌트가 동시에 GetAsync를 호출할 때마다 JsonLoader.LoadAsync가 중복 실행될 수 있음.
        private static UniTask<CommonSettings> _cachedTask;
        private static bool _isLoadStarted;

        /// <summary>
        /// 00_Common.json을 로드하여 반환함. 이미 로드를 시작했다면(완료 여부 무관) 그 태스크를 그대로 공유함.
        /// 개별 호출자의 취소는 자신의 await에만 적용되고, 공유 로드 자체는 취소되지 않음.
        /// </summary>
        public static async UniTask<CommonSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            if (!_isLoadStarted)
            {
                _isLoadStarted = true;
                _cachedTask = LoadAsync().Preserve();
            }

            return await _cachedTask.AttachExternalCancellation(cancellationToken);
        }

        private static UniTask<CommonSettings> LoadAsync()
        {
            return JsonLoader.LoadAsync<CommonSettings>(
                $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.ResourcePaths.CommonSettingsFileName}");
        }
    }
}
