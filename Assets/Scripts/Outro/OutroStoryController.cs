using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DGAIZone.App;
using DGAIZone.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DGAIZone.Outro
{
    /// <summary>
    /// 아웃트로 스토리 텍스트를 한 줄씩 올라오는 연출(공용 StoryLineAnimator, 타이틀/레벨 선택과 동일)로 보여주는 컨트롤러.
    /// 연출 중 클릭하면 즉시 전체를 출력(스킵)하고, 연출이 끝나면 "처음으로" 버튼을 활성화함.
    /// 전체를 덮는 raycast 오브젝트(BG 등)에 부착하면 홈 버튼을 제외한 모든 클릭을 받음.
    /// </summary>
    public class OutroStoryController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private GameObject homeButton; // 연출이 끝나면 활성화할 "처음으로" 버튼

        private bool _isAnimating;
        private bool _skipRequested;

        // 00_Common.json 튜닝 값 — 로드 완료 전까지는 null이며 Constants.StoryLine 폴백 값을 그대로 사용함
        private CommonSettings _commonSettings;

        /// <summary> 스토리 텍스트를 한 줄씩 올라오는 연출로 표시함. </summary>
        private void Start()
        {
            if (storyText == null) return;

            // 연출이 끝나기 전까지 "처음으로" 버튼을 숨김
            if (homeButton != null) homeButton.SetActive(false);

            // 페이드인 도중 전체 텍스트가 잠깐 보이지 않도록 미리 숨겨 둠(AnimateAsync가 다시 전체 노출로 되돌린 뒤 진행함)
            storyText.ForceMeshUpdate();
            storyText.maxVisibleCharacters = 0;

            AnimateStoryAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary> 클릭 시 연출 중이면 즉시 전체를 출력함(스킵). </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isAnimating) _skipRequested = true;
        }

        /// <summary> 00_Common.json 연출 타이밍을 불러온 뒤 공용 유틸로 스토리 텍스트를 한 줄씩 올리는 연출을 실행하고, 끝나면 홈 버튼을 활성화함. </summary>
        private async UniTaskVoid AnimateStoryAsync(CancellationToken token)
        {
            _isAnimating = true;
            _skipRequested = false;
            try
            {
                _commonSettings = await CommonSettingsProvider.GetAsync(token);

                await StoryLineAnimator.AnimateAsync(storyText,
                    _commonSettings?.storyLineMoveDuration ?? Constants.StoryLine.StoryLineMoveDuration,
                    _commonSettings?.storyLineInterval ?? Constants.StoryLine.StoryLineInterval,
                    _commonSettings?.storyLineYOffset ?? Constants.StoryLine.StoryLineYOffset,
                    () => _skipRequested, token);

                OnFullyShown();
            }
            catch (OperationCanceledException) { }
            finally { _isAnimating = false; }
        }

        /// <summary> 텍스트가 완전히 노출된 시점 처리. "처음으로" 버튼을 활성화함. </summary>
        private void OnFullyShown()
        {
            if (homeButton != null) homeButton.SetActive(true);
        }
    }
}
