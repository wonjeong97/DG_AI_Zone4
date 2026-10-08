using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Game.UI;
using HuliacDev.UI;
using Microsoft.Extensions.Logging;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬 설계창(나의 코딩 결과·AI 패널)에 3_Game에서 기록한 설계(DesignStep)를 같은 블록 이미지로 다시 쌓는 도우미.
    /// 3_Game 설계창과 같은 배율로 준비해 두고(Prepare), 블록을 일정 간격으로 하나씩 붙임(StackAsync). 블록(완성하기 포함)이
    /// 붙을 때마다 3_Game 설정하기와 같은 블록 장착 효과음을 내며, 설계창보다 길어지면 맨 아래로 자동 스크롤함.
    /// </summary>
    internal static class ResultDesignPlayback
    {
        /// <summary>
        /// 설계창을 비운 뒤 steps에 맞춰(값 블록이 하나라도 있으면 값 블록 폭, 함수 사용 단계가 있으면 함수 정의 블록 자리) 블록 묶음 폭과
        /// 위치를 정해 시작하기 블록만 놓음.
        /// </summary>
        public static void Prepare(DesignPanel panel, IReadOnlyList<DesignStep> steps)
        {
            bool withValueBlocks = false;
            bool withFunctionDefinition = false;
            for (int i = 0; i < steps.Count; i++)
            {
                if (!string.IsNullOrEmpty(steps[i].Value)) withValueBlocks = true;
                if (steps[i].Shape == DesignStepShape.FunctionCall) withFunctionDefinition = true;
            }

            panel.Initialize(withValueBlocks, withFunctionDefinition);
        }

        /// <summary>
        /// steps를 interval초 간격으로 하나씩 붙이고, withEnd면 마지막에 완성하기 블록까지 붙인 뒤 연출이 끝날 때까지 기다림. 블록을 붙일 때마다
        /// 블록 장착 효과음을 냄(soundManager가 없으면 logger에 경고).
        /// </summary>
        public static async UniTask StackAsync<T>(DesignPanel panel, IReadOnlyList<DesignStep> steps, bool withEnd, float interval, SoundManager soundManager, ILogger<T> logger, CancellationToken token)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                panel.AddItem(steps[i].Shape, steps[i].Command, steps[i].Value);
                SoundEffects.Play(soundManager, Constants.Sounds.BlockAssembled, logger);
                await UniTask.Delay(TimeSpan.FromSeconds(UnityEngine.Mathf.Max(0f, interval)), DelayType.UnscaledDeltaTime, cancellationToken: token); // 음수면 Delay가 예외를 냄
            }

            if (!withEnd) return;

            SoundEffects.Play(soundManager, Constants.Sounds.BlockAssembled, logger);
            await panel.AttachEndBlockAsync(token);
        }
    }
}
