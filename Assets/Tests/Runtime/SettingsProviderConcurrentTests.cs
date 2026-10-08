using System.Collections;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using DGAIZone.Game;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DGAIZone.Tests
{
    /// <summary>
    /// CommonSettingsProvider와 GameSceneSettingsProvider를 여러 곳에서 동시에 await해도
    /// InvalidOperationException(Already continuation registered) 등의 예외 없이
    /// 안전하게 결과를 공유받는지 검증하는 테스트.
    /// </summary>
    public class SettingsProviderConcurrentTests
    {
        [UnityTest]
        public IEnumerator CommonSettingsProvider_동시_호출_시_예외없이_모두_완료된다() => UniTask.ToCoroutine(async () =>
        {
            UniTask<CommonSettings> task1 = CommonSettingsProvider.GetAsync();
            UniTask<CommonSettings> task2 = CommonSettingsProvider.GetAsync();
            UniTask<CommonSettings> task3 = CommonSettingsProvider.GetAsync();
            UniTask<CommonSettings> task4 = CommonSettingsProvider.GetAsync();

            (CommonSettings r1, CommonSettings r2, CommonSettings r3, CommonSettings r4) =
                await UniTask.WhenAll(task1, task2, task3, task4).AwaitWithRealtimeTimeout(5f);

            Assert.IsNotNull(r1, "task1 결과가 null임");
            Assert.IsNotNull(r2, "task2 결과가 null임");
            Assert.IsNotNull(r3, "task3 결과가 null임");
            Assert.IsNotNull(r4, "task4 결과가 null임");
            Assert.AreSame(r1, r2, "동일한 캐시 인스턴스를 공유해야 함");
            Assert.AreSame(r2, r3, "동일한 캐시 인스턴스를 공유해야 함");
            Assert.AreSame(r3, r4, "동일한 캐시 인스턴스를 공유해야 함");
        });

        [UnityTest]
        public IEnumerator GameSceneSettingsProvider_동시_호출_시_예외없이_모두_완료된다() => UniTask.ToCoroutine(async () =>
        {
            UniTask<GameSceneSettings> task1 = GameSceneSettingsProvider.GetAsync();
            UniTask<GameSceneSettings> task2 = GameSceneSettingsProvider.GetAsync();
            UniTask<GameSceneSettings> task3 = GameSceneSettingsProvider.GetAsync();

            (GameSceneSettings r1, GameSceneSettings r2, GameSceneSettings r3) =
                await UniTask.WhenAll(task1, task2, task3).AwaitWithRealtimeTimeout(5f);

            Assert.IsNotNull(r1, "task1 결과가 null임");
            Assert.IsNotNull(r2, "task2 결과가 null임");
            Assert.IsNotNull(r3, "task3 결과가 null임");
            Assert.AreSame(r1, r2, "동일한 캐시 인스턴스를 공유해야 함");
            Assert.AreSame(r2, r3, "동일한 캐시 인스턴스를 공유해야 함");
        });
    }
}
