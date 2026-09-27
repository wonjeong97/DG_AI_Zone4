using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 연속 비동기 시퀀스 호출 시(경고 연출, 진행도 적용 등), 이전 비동기 작업의 finally 블록이
    /// 새로 생성된 CancellationTokenSource를 실수로 null 처리하거나 Dispose하지 않는지
    /// 레이스 컨디션 안전성을 검증하는 테스트.
    /// </summary>
    public class CancellationTokenRaceTests
    {
        /// <summary>
        /// 프로젝트 컨트롤러들(IngredientSelectionController, MissionBoardController 등)에서
        /// 사용하는 안전한 CTS 라이프사이클 관리 패턴을 모방한 테스터 클래스.
        /// </summary>
        private class CtsRaceHarness
        {
            public CancellationTokenSource ActiveCts { get; private set; }
            public int CompletedCount { get; private set; }
            public int CanceledCount { get; private set; }

            public async UniTask ExecuteSequenceAsync(int delayMs)
            {
                ActiveCts?.Cancel();
                ActiveCts?.Dispose();
                CancellationTokenSource cts = new CancellationTokenSource();
                ActiveCts = cts;
                CancellationToken token = cts.Token;

                try
                {
                    await UniTask.Delay(delayMs, cancellationToken: token);
                    CompletedCount++;
                }
                catch (OperationCanceledException)
                {
                    CanceledCount++;
                }
                finally
                {
                    // 최신 CTS가 이미 교체된 경우(=현재 작업이 취소된 이전 작업인 경우) 건드리지 않음
                    if (ActiveCts == cts)
                    {
                        ActiveCts.Dispose();
                        ActiveCts = null;
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator 연속_호출_시_이전_작업의_finally가_새로운_CTS를_제거하지_않는다() => UniTask.ToCoroutine(async () =>
        {
            CtsRaceHarness harness = new CtsRaceHarness();

            // 첫 번째 작업: 긴 지연시간(300ms)
            UniTask firstTask = harness.ExecuteSequenceAsync(300);
            CancellationTokenSource firstCts = harness.ActiveCts;
            Assert.IsNotNull(firstCts, "첫 번째 작업의 CTS가 생성되어야 함.");

            // 잠시 대기 후 두 번째 작업 실행: 짧은 지연시간(50ms)
            await UniTask.Delay(30);
            UniTask secondTask = harness.ExecuteSequenceAsync(50);
            CancellationTokenSource secondCts = harness.ActiveCts;

            Assert.AreNotSame(firstCts, secondCts, "두 번째 작업 시 새로운 CTS로 교체되어야 함.");
            Assert.IsTrue(firstCts.IsCancellationRequested, "첫 번째 작업의 CTS는 취소되어야 함.");

            // 두 작업이 모두 끝날 때까지 대기
            await UniTask.WhenAll(firstTask, secondTask).AwaitWithRealtimeTimeout(5f);

            Assert.AreEqual(1, harness.CanceledCount, "첫 번째 작업은 취소되어야 함.");
            Assert.AreEqual(1, harness.CompletedCount, "두 번째 작업은 정상 완료되어야 함.");
            Assert.IsNull(harness.ActiveCts, "두 번째 작업이 최종 완료된 후에는 ActiveCts가 null이어야 함.");
        });

        [UnityTest]
        public IEnumerator 단독_작업_완료_시_CTS가_정상적으로_정리된다() => UniTask.ToCoroutine(async () =>
        {
            CtsRaceHarness harness = new CtsRaceHarness();

            UniTask task = harness.ExecuteSequenceAsync(20);
            Assert.IsNotNull(harness.ActiveCts);

            await task.AwaitWithRealtimeTimeout(5f);

            Assert.AreEqual(0, harness.CanceledCount);
            Assert.AreEqual(1, harness.CompletedCount);
            Assert.IsNull(harness.ActiveCts, "완료 후에는 CTS가 정리되어 null이어야 함.");
        });

        [Test]
        public void IngredientSelectionController_경고_CTS_필드가_올바른_타입으로_정의되어있다()
        {
            FieldInfo field = typeof(IngredientSelectionController).GetField("_warningCts", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "IngredientSelectionController에 _warningCts 필드가 존재해야 함.");
            Assert.AreEqual(typeof(CancellationTokenSource), field.FieldType, "필드 타입이 CancellationTokenSource여야 함.");
        }
    }
}
