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
        private static CommonSettings _cached;

        /// <summary> 00_Common.json을 로드하여 반환함. 이미 로드했다면 캐시된 값을 그대로 반환함. </summary>
        public static async UniTask<CommonSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            _cached ??= await JsonLoader.LoadAsync<CommonSettings>(
                $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.ResourcePaths.CommonSettingsFileName}", cancellationToken);

            return _cached;
        }
    }
}
