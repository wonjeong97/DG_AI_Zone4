using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using Wonjeong.Utils;

namespace DGAIZone.Game
{
    /// <summary>
    /// StreamingAssets/Json/3_Game.json(GameSceneSettings)을 최초 1회만 로드해 3_Game 씬 안에서 공유하는 정적 유틸리티.
    /// CodingCategoryIndicatorController, IngredientSelectionController, MissionBoardController가 각자 중복 로드하지 않도록 함.
    /// </summary>
    public static class GameSceneSettingsProvider
    {
        // Preserve()로 여러 번 await 가능한 공유 UniTask로 캐싱함 — 값만 캐싱하면 로드가 끝나기 전에
        // 여러 컴포넌트가 동시에 GetAsync를 호출할 때마다 JsonLoader.LoadAsync가 중복 실행될 수 있음.
        private static UniTask<GameSceneSettings> _cachedTask;
        private static bool _isLoadStarted;

        /// <summary>
        /// 3_Game.json을 로드하여 반환함. 이미 로드를 시작했다면(완료 여부 무관) 그 태스크를 그대로 공유함.
        /// 개별 호출자의 취소는 자신의 await에만 적용되고, 공유 로드 자체는 취소되지 않음.
        /// </summary>
        public static async UniTask<GameSceneSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            if (!_isLoadStarted)
            {
                _isLoadStarted = true;
                _cachedTask = LoadAsync().Preserve();
            }

            return await _cachedTask.AttachExternalCancellation(cancellationToken);
        }

        private static UniTask<GameSceneSettings> LoadAsync()
        {
            return JsonLoader.LoadAsync<GameSceneSettings>(
                $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Game}");
        }
    }
}
