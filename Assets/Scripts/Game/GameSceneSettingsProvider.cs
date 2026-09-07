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
        // 값만 캐싱하고, 로드 완료 여부는 폴링(UniTask.WaitUntil)으로 기다림 — UniTask.Preserve()는 로드가 끝나기 전에
        // 2개 이상의 호출자가 동시에(같은 프레임에) await를 걸면 내부적으로 "Already continuation registered" 예외를
        // 던지는 문제가 있어(3_Game은 실제로 세 컴포넌트가 Start()에서 동시에 GetAsync를 호출함) 쓰지 않음.
        private static GameSceneSettings _cached;
        private static bool _isLoaded;
        private static bool _isLoadStarted;

        /// <summary>
        /// 3_Game.json을 로드하여 반환함. 이미 로드를 시작했다면(완료 여부 무관) 그 결과를 그대로 공유함.
        /// 여러 호출자가 동시에 불러도 안전함(로드 자체는 한 번만 실행됨).
        /// </summary>
        public static async UniTask<GameSceneSettings> GetAsync(CancellationToken cancellationToken = default)
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
            _cached = await JsonLoader.LoadAsync<GameSceneSettings>(
                $"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Game}");
            _isLoaded = true;
        }
    }
}
