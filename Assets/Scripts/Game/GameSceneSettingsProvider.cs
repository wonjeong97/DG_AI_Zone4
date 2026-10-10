using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using HuliacDev.Utils;

namespace DGAIZone.Game
{
    /// <summary>
    /// StreamingAssets/Json/3_Game.json(GameSceneSettings)을 최초 1회만 로드해 3_Game 씬 안에서 공유하는 정적 유틸리티.
    /// CodingCategoryIndicatorController, IngredientSelectionController, MissionBoardController가 각자 중복 로드하지 않도록 함.
    /// </summary>
    public static class GameSceneSettingsProvider
    {
        // 공유 소스로 UniTask 대신 Task를 캐싱함 — UniTask는 awaiter를 한 번만 등록할 수 있어, 로드가 끝나기 전에
        // 여러 컴포넌트가 같은 프레임에 동시에 await하면 "Already continuation registered" 예외가 남.
        // Task는 다중 awaiter를 기본 지원하므로 3_Game의 세 컴포넌트가 Start()에서 동시에 불러도 안전함.
        private static Task<GameSceneSettings> _loadTask;

        /// <summary>
        /// 3_Game.json을 로드하여 반환함. 최초 호출 시에만 실제 로드가 시작되고, 이후 호출은 완료 여부와 무관하게 같은 로드 결과를 공유함.
        /// cancellationToken은 호출자의 대기만 취소하며 공유 로드 자체는 취소하지 않음.
        /// </summary>
        public static UniTask<GameSceneSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            _loadTask ??= LoadAsync().AsTask();
            return _loadTask.AsUniTask().AttachExternalCancellation(cancellationToken);
        }

        /// <summary> 테스트 전용: 캐시한 로드를 비워 다음 GetAsync가 '로드 전 동시 호출' 상황에서 시작하게 함. </summary>
        internal static void ResetForTest()
        {
            _loadTask = null;
        }

        /// <summary> 3_Game.json을 실제로 한 번 로드함. 예외가 새어 나와도 폴백 기본값으로 완료해 대기 중인 호출자가 무한 대기하지 않게 함. </summary>
        private static async UniTask<GameSceneSettings> LoadAsync()
        {
            try
            {
                return await JsonLoader.LoadAsync<GameSceneSettings>($"{Constants.ResourcePaths.SceneSettingsFolder}/{Constants.Scenes.Game}");
            }
            catch (System.Exception e)
            {
                // 정적 유틸이라 로거를 주입받을 수 없어 Debug로 대체 출력함
                UnityEngine.Debug.LogError($"[GameSceneSettingsProvider] 3_Game.json 로드 실패, 기본값으로 대체함: {e.Message}");
                return new GameSceneSettings();
            }
        }
    }
}
