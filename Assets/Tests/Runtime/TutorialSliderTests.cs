using DGAIZone.Intro;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 튜토리얼 이미지 슬라이더(체험 방법) 페이지 이동 및 완료 이벤트 동작 검증 테스트.
    /// </summary>
    public class TutorialSliderTests
    {
        private GameObject _sliderGo;
        private TutorialImageSlider _slider;

        [SetUp]
        public void SetUp()
        {
            _sliderGo = new GameObject("TestTutorialSlider");
            _sliderGo.AddComponent<RectTransform>();
            _sliderGo.AddComponent<Image>();
            _slider = _sliderGo.AddComponent<TutorialImageSlider>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_sliderGo) Object.DestroyImmediate(_sliderGo);
        }

        [Test]
        public void TutorialImageSlider_초기페이지_인덱스는_0이다()
        {
            Assert.AreEqual(0, _slider.CurrentIndex, "초기 상태에서는 CurrentIndex가 0(1페이지)이어야 함.");
        }

        [Test]
        public void TutorialImageSlider_1페이지에서_이전호출시_7페이지로_순환하지않고_0을유지한다()
        {
            // Act: 1페이지(0)에서 이전 페이지 호출
            _slider.ShowPrevious();

            // Assert: 순환되지 않고 0에 머물러야 함
            Assert.AreEqual(0, _slider.CurrentIndex, "1페이지에서 이전 호출 시 마지막 페이지로 순환하지 않고 0을 유지해야 함.");
        }

        [Test]
        public void TutorialImageSlider_다음_및_이전호출시_인덱스가_정상변경된다()
        {
            // Act 1: 1페이지 -> 2페이지
            _slider.ShowNext();
            Assert.AreEqual(1, _slider.CurrentIndex, "ShowNext 호출 후 인덱스가 1이어야 함.");

            // Act 2: 2페이지 -> 1페이지
            _slider.ShowPrevious();
            Assert.AreEqual(0, _slider.CurrentIndex, "ShowPrevious 호출 후 인덱스가 0이어야 함.");
        }

        [Test]
        public void TutorialImageSlider_마지막페이지에서_다음호출시_OnTutorialCompleted이벤트가_발생한다()
        {
            // TotalPages는 7 (인덱스 0~6)
            for (int i = 0; i < 6; i++)
            {
                _slider.ShowNext();
            }
            Assert.AreEqual(6, _slider.CurrentIndex, "6번 ShowNext 호출 후 인덱스는 6(7/7)이어야 함.");

            bool completedFired = false;
            using System.IDisposable subscription = _slider.TutorialCompleted.Subscribe(_ => completedFired = true);

            // Act: 7/7 페이지에서 다음 호출
            _slider.ShowNext();

            // Assert: 완료 이벤트가 발생하고 인덱스는 6을 유지해야 함(0으로 순환되지 않음)
            Assert.IsTrue(completedFired, "마지막 페이지에서 ShowNext 호출 시 TutorialCompleted가 값을 내보내야 함.");
            Assert.AreEqual(6, _slider.CurrentIndex, "완료 이벤트 발생 후에도 인덱스는 6을 유지해야 함.");
        }

        [Test]
        public void TutorialImageSlider_마지막페이지에서_이전호출시_6페이지로_정상이동한다()
        {
            for (int i = 0; i < 6; i++)
            {
                _slider.ShowNext();
            }
            Assert.AreEqual(6, _slider.CurrentIndex);

            // Act
            _slider.ShowPrevious();

            // Assert
            Assert.AreEqual(5, _slider.CurrentIndex, "7페이지에서 ShowPrevious 호출 시 6페이지(인덱스 5)로 이동해야 함.");
        }
    }
}
