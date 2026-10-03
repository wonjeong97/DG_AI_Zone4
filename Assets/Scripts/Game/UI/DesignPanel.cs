using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ZLogger;

namespace DGAIZone.Game.UI
{
    /// <summary>
    /// 확정된 블록을 한 줄씩 쌓아 보여 주는 설계창(Image_DesignWindow). 줄을 만들고 지우는 일과,
    /// 카드가 떨어진 단계부터 뒤쪽 줄을 흐리게 표시하는 일을 맡음. 줄 내용(문구)은 호출하는 쪽이 정함.
    /// </summary>
    public class DesignPanel : MonoBehaviour
    {
        private const float DeactivatedAlpha = 0.35f;

        [SerializeField] private Transform content; // DesignScrollView/Viewport/DesignContainer
        [SerializeField] private TextMeshProUGUI itemPrefab; // 한 줄을 표시할 DesignItem 프리팹

        private readonly List<TMP_Text> _items = new List<TMP_Text>();
        private IObjectResolver _resolver;
        private ILogger<DesignPanel> _logger;

        /// <summary> 지금 설계창에 있는 줄 수. </summary>
        public int Count => _items.Count;

        /// <summary> VContainer 의존성 주입. 줄 생성용 리졸버와 로거를 할당함. </summary>
        [Inject]
        public void Construct(IObjectResolver resolver, ILogger<DesignPanel> logger)
        {
            _resolver = resolver;
            _logger = logger;
        }

        /// <summary> 설계창 맨 아래에 한 줄을 추가함. </summary>
        public void AddItem(string text)
        {
            if (!content || !itemPrefab)
            {
                if (_logger != null) _logger.ZLogWarning($"[DesignPanel] content 또는 itemPrefab이 null이라 설계 줄을 추가할 수 없음.");
                return;
            }

            if (_resolver == null)
            {
                if (_logger != null) _logger.ZLogError($"[DesignPanel] IObjectResolver가 주입되지 않아 설계 줄을 생성할 수 없음.");
                return;
            }

            TextMeshProUGUI item = _resolver.Instantiate(itemPrefab, content);
            item.text = text;
            _items.Add(item);
        }

        /// <summary> 마지막으로 추가한 줄을 지움. </summary>
        public void RemoveLastItem()
        {
            if (_items.Count == 0) return;

            int lastIndex = _items.Count - 1;
            TMP_Text last = _items[lastIndex];
            _items.RemoveAt(lastIndex);
            if (last) Destroy(last.gameObject);
        }

        /// <summary> 모든 줄을 지움. </summary>
        public void Clear()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i]) Destroy(_items[i].gameObject);
            }
            _items.Clear();
        }

        /// <summary> fromIndex번째 줄부터 끝까지 흐리게, 그 앞은 원래대로 표시함. fromIndex가 줄 수 이상이면 모두 원래대로 표시함. </summary>
        public void DimFrom(int fromIndex)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i]) _items[i].alpha = (i >= fromIndex) ? DeactivatedAlpha : 1f;
            }
        }
    }
}
