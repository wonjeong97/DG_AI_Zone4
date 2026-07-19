using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DGAIZone.Outro
{
    /// <summary>
    /// 아웃트로 스토리 텍스트를 타이핑 연출로 보여주는 컨트롤러.
    /// 타이핑 중 클릭하면 즉시 전체를 출력(스킵)하고, 연출이 끝나면 "처음으로" 버튼을 활성화함.
    /// 전체를 덮는 raycast 오브젝트(BG 등)에 부착하면 홈 버튼을 제외한 모든 클릭을 받음.
    /// </summary>
    public class OutroStoryController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private float charsPerSecond = 30f;
        [SerializeField] private GameObject homeButton; // 연출이 끝나면 활성화할 "처음으로" 버튼

        private bool _isTyping;
        private CancellationTokenSource _typingCts;

        /// <summary> 스토리 텍스트를 타이핑 연출로 표시함. </summary>
        private void Start()
        {
            if (storyText == null) return;

            // 스토리 연출이 끝나기 전까지 "처음으로" 버튼을 숨김
            if (homeButton != null) homeButton.SetActive(false);

            StartTyping();
        }

        /// <summary> 클릭 시 타이핑 중이면 즉시 전체를 출력함(스킵). </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (storyText == null) return;
            if (_isTyping) CompleteTyping();
        }

        /// <summary> 현재 스토리 텍스트를 타이핑 연출로 시작함. </summary>
        private void StartTyping()
        {
            storyText.ForceMeshUpdate();
            storyText.maxVisibleCharacters = 0;

            CancelTyping();
            _typingCts = new CancellationTokenSource();
            TypeAsync(storyText.textInfo.characterCount, _typingCts.Token).Forget();
        }

        /// <summary> 글자를 하나씩 노출하는 타이핑 루프. </summary>
        private async UniTaskVoid TypeAsync(int total, CancellationToken token)
        {
            _isTyping = true;
            try
            {
                float shown = 0f;
                while (shown < total)
                {
                    shown += Mathf.Max(1f, charsPerSecond) * Time.deltaTime;
                    storyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                storyText.maxVisibleCharacters = total;
                OnFullyShown();
            }
            catch (OperationCanceledException) { }
            finally { _isTyping = false; }
        }

        /// <summary> 텍스트를 즉시 전부 노출함(스킵). </summary>
        private void CompleteTyping()
        {
            CancelTyping();
            storyText.maxVisibleCharacters = storyText.textInfo.characterCount;
            _isTyping = false;
            OnFullyShown();
        }

        /// <summary> 텍스트가 완전히 노출된 시점 처리. "처음으로" 버튼을 활성화함. </summary>
        private void OnFullyShown()
        {
            if (homeButton != null) homeButton.SetActive(true);
        }

        /// <summary> 진행 중인 타이핑 작업을 취소함. </summary>
        private void CancelTyping()
        {
            if (_typingCts != null)
            {
                _typingCts.Cancel();
                _typingCts.Dispose();
                _typingCts = null;
            }
        }

        /// <summary> 파괴 시 타이핑 작업을 정리함. </summary>
        private void OnDestroy()
        {
            CancelTyping();
        }
    }
}
