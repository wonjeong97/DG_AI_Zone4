using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.Game.UI;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬 설계창(나의 코딩 결과·AI 패널)에 3_Game에서 기록한 설계(DesignStep)를 같은 블록 이미지로 다시 쌓는 도우미.
    /// 3_Game 설계창과 같은 배치 방식·설계 길이로 배율을 정해 두고(Prepare), 블록을 일정 간격으로 하나씩 붙임(StackAsync). 자동 스크롤 방식이면
    /// 블록이 붙을 때마다 맨 아래로 스크롤함.
    /// </summary>
    internal static class ResultDesignPlayback
    {
        /// <summary>
        /// 설계창을 3_Game 설계창과 같은 배치 방식(mode)으로 바꾸고 비운 뒤, steps가 모두 쌓였을 때의 모양으로 배율을 정해 시작하기 블록만 놓음.
        /// 값 블록이 하나라도 있으면 값 블록 폭을 남김.
        /// </summary>
        public static void Prepare(DesignPanel panel, IReadOnlyList<DesignStep> steps, DesignLayoutMode mode)
        {
            panel.LayoutMode = mode;
            List<DesignStepShape> shapes = new List<DesignStepShape>(steps.Count);
            bool withValueBlocks = false;
            for (int i = 0; i < steps.Count; i++)
            {
                shapes.Add(steps[i].Shape);
                if (!string.IsNullOrEmpty(steps[i].Value)) withValueBlocks = true;
            }

            panel.Initialize(shapes, withValueBlocks);
        }

        /// <summary> steps를 interval초 간격으로 하나씩 붙이고, withEnd면 마지막에 완성하기 블록까지 붙인 뒤 연출이 끝날 때까지 기다림. </summary>
        public static async UniTask StackAsync(DesignPanel panel, IReadOnlyList<DesignStep> steps, bool withEnd, float interval, CancellationToken token)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                panel.AddItem(steps[i].Shape, steps[i].Command, steps[i].Value);
                await UniTask.Delay(TimeSpan.FromSeconds(interval), DelayType.UnscaledDeltaTime, cancellationToken: token);
            }

            if (withEnd) await panel.AttachEndBlockAsync(token);
        }
    }
}
