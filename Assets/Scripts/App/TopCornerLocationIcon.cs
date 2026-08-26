using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// TopCornerImages 프리팹의 Image_TopRight에 부착되어, 선택된 레벨(SelectedLevelStore)에 맞는
    /// Location_Level 이미지를 Resources에서 불러와 표시함.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TopCornerLocationIcon : MonoBehaviour
    {
        private const string SpriteNamePrefix = "Location_Level";

        private Image _image;
        private SelectedLevelStore _selectedLevelStore;
        private ILogger<TopCornerLocationIcon> _logger;

        /// <summary> VContainer 의존성 주입. 선택된 레벨 저장소와 로거를 할당함. </summary>
        [Inject]
        public void Construct(SelectedLevelStore selectedLevelStore, ILogger<TopCornerLocationIcon> logger)
        {
            _selectedLevelStore = selectedLevelStore;
            _logger = logger;
        }

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        /// <summary> 주입이 끝난 뒤 현재 선택된 레벨에 맞는 위치 이미지를 적용함. </summary>
        private void Start()
        {
            int level = 1;
            if (_selectedLevelStore != null)
            {
                level = _selectedLevelStore.SelectedLevel;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[TopCornerLocationIcon] selectedLevelStore is null. Falling back to level {level}.");
            }

            ApplyLocationSprite(level);
        }

        /// <summary> Resources에서 "Location_Level{level}" 스프라이트를 찾아 적용함. 없으면 경고 로그만 남기고 기존 스프라이트를 유지함. </summary>
        private void ApplyLocationSprite(int level)
        {
            string spriteName = $"{SpriteNamePrefix}{level}";
            Sprite sprite = Resources.Load<Sprite>(spriteName);

            if (sprite != null)
            {
                _image.sprite = sprite;
            }
            else if (_logger != null)
            {
                _logger.ZLogWarning($"[TopCornerLocationIcon] '{spriteName}' not found in Resources.");
            }
        }
    }
}
