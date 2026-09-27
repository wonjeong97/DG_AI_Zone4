using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 비동기 테스트 완료 대기에 실시간 기준 타임아웃 가드를 붙이는 공통 확장 메서드.
    /// </summary>
    internal static class TestTaskExtensions
    {
        /// <summary>
        /// 반환값이 없는 UniTask 완료를 기다리되, 제한 시간을 넘기면 Assert 실패로 끝낸다.
        /// </summary>
        public static async UniTask AwaitWithRealtimeTimeout(this UniTask task, float timeoutSeconds = 3f)
        {
            int finishedIndex = await UniTask.WhenAny(
                task,
                UniTask.Delay(TimeSpan.FromSeconds(timeoutSeconds), DelayType.UnscaledDeltaTime));

            if (finishedIndex != 0)
            {
                Assert.Fail($"제한 시간 {timeoutSeconds}초 내에 완료되지 않음.");
            }
        }

        /// <summary>
        /// 반환값이 있는 UniTask&lt;T&gt; 완료를 기다리되, 제한 시간을 넘기면 Assert 실패로 끝낸다.
        /// </summary>
        public static async UniTask<T> AwaitWithRealtimeTimeout<T>(this UniTask<T> task, float timeoutSeconds = 3f)
        {
            (bool hasResultLeft, T result) = await UniTask.WhenAny(
                task,
                UniTask.Delay(TimeSpan.FromSeconds(timeoutSeconds), DelayType.UnscaledDeltaTime));

            if (!hasResultLeft)
            {
                Assert.Fail($"제한 시간 {timeoutSeconds}초 내에 완료되지 않음.");
            }

            return result;
        }
    }
}
