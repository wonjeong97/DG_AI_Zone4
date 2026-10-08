using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Game.UI;
using HuliacDev.UI;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

namespace DGAIZone.Result
{
    /// <summary>
    /// 결과 씬 왼쪽 아래 '나의 코딩 결과' 패널(PlayerPanel 프레임). 씬이 시작되면 3_Game 설계창과 같은 배율로 시작하기 블록을 놓아 두고,
    /// ResultFlowController가 4_Result.json을 불러온 뒤 Play를 부르면 플레이어가 쌓은 블록(GameResultStore.PlayerDesign)을 같은 블록 이미지로
    /// designBlockInterval초 간격으로 하나씩 쌓으며, 코딩완료로 마쳤으면 완성하기 블록까지 붙임.
    /// </summary>
    public class ResultPlayerPanel : MonoBehaviour
    {
        [SerializeField] private DesignPanel designPanel; // 플레이어 설계를 블록으로 쌓는 설계창(3_Game과 같은 DesignBlock 프리팹)

        private GameResultStore _resultStore;
        private ILogger<ResultPlayerPanel> _logger;
        private SoundManager _soundManager;

        /// <summary> VContainer 의존성 주입. 플레이어 설계를 담은 게임 결과 저장소, 로거, 효과음 매니저를 할당하고 설계창에 블록 생성용 리졸버를 주입함. </summary>
        [Inject]
        public void Construct(GameResultStore resultStore, IObjectResolver resolver, ILogger<ResultPlayerPanel> logger, SoundManager soundManager = null)
        {
            _resultStore = resultStore;
            _logger = logger;
            _soundManager = soundManager;

            if (designPanel) resolver.Inject(designPanel);
            else if (_logger != null) _logger.ZLogWarning($"[ResultPlayerPanel] designPanel이 null이라 나의 코딩 결과를 표시할 수 없음.");
        }

        /// <summary> 플레이어 설계에 맞춰 설계창 블록 묶음 폭을 정하고 시작하기 블록만 놓아 둠. </summary>
        private void Start()
        {
            if (!designPanel) return; // Construct에서 이미 경고를 남김

            ResultDesignPlayback.Prepare(designPanel, PlayerDesign());
        }

        /// <summary> 플레이어 블록을 blockInterval초 간격으로 쌓기 시작함(ResultFlowController가 연출 설정을 불러온 뒤 부름). </summary>
        public void Play(float blockInterval)
        {
            if (!designPanel)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultPlayerPanel] designPanel이 null이라 나의 코딩 결과를 쌓을 수 없음.");
                return;
            }

            StackAsync(PlayerDesign(), blockInterval, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 결과 저장소의 플레이어 설계. 없으면 경고를 남기고 빈 목록(시작하기만 보임)을 반환함. </summary>
        private IReadOnlyList<DesignStep> PlayerDesign()
        {
            if (_resultStore != null) return _resultStore.PlayerDesign;

            if (_logger != null) _logger.ZLogWarning($"[ResultPlayerPanel] resultStore가 null이라 플레이어 설계 없이 시작하기 블록만 표시함.");
            return Array.Empty<DesignStep>();
        }

        /// <summary> 플레이어 블록을 하나씩 쌓고, 코딩완료로 마쳤으면 완성하기 블록까지 붙임. 씬이 바뀌어 취소되면 조용히 멈춤. </summary>
        private async UniTaskVoid StackAsync(IReadOnlyList<DesignStep> steps, float blockInterval, CancellationToken token)
        {
            try
            {
                bool completed = _resultStore != null && _resultStore.PlayerDesignCompleted;
                await ResultDesignPlayback.StackAsync(designPanel, steps, completed, blockInterval, _soundManager, _logger, token);
            }
            catch (OperationCanceledException) { }
        }
    }
}
