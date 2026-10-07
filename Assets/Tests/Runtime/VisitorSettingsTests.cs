using DGAIZone.App;
using DGAIZone.Data;
using NUnit.Framework;
using UnityEngine;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 운영 모드·체험자 이름 설정 — 관리자 화면에서 바꾼 값은 앱을 다시 켜도 유지되고, 지우면 에셋 기본값으로 돌아가야 함.
    /// 빌드에서는 런타임에 바꾼 SO 값이 재시작 때 사라지므로 PlayerPrefs로 유지하는 것이 핵심임.
    /// </summary>
    public class VisitorSettingsTests
    {
        // 에디터 PlayerPrefs를 건드리므로 테스트 전 값을 기억했다가 되돌림
        private bool _hadServerConnected;
        private int _serverConnected;
        private bool _hadVisitorName;
        private string _visitorName;

        private VisitorSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _hadServerConnected = PlayerPrefs.HasKey(VisitorSettings.ServerConnectedKey);
            _serverConnected = PlayerPrefs.GetInt(VisitorSettings.ServerConnectedKey);
            _hadVisitorName = PlayerPrefs.HasKey(VisitorSettings.VisitorNameKey);
            _visitorName = PlayerPrefs.GetString(VisitorSettings.VisitorNameKey);

            _settings = ScriptableObject.CreateInstance<VisitorSettings>();
            _settings.ClearOverrides();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);

            if (_hadServerConnected) PlayerPrefs.SetInt(VisitorSettings.ServerConnectedKey, _serverConnected);
            else PlayerPrefs.DeleteKey(VisitorSettings.ServerConnectedKey);

            if (_hadVisitorName) PlayerPrefs.SetString(VisitorSettings.VisitorNameKey, _visitorName);
            else PlayerPrefs.DeleteKey(VisitorSettings.VisitorNameKey);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// 관리자 화면에서 바꾼 적이 없으면 에셋 기본값(로컬 모드, '체험자')을 씀.
        /// </summary>
        [Test]
        public void 바꾼_적이_없으면_에셋_기본값을_쓴다()
        {
            Assert.IsFalse(_settings.IsServerConnected);
            Assert.AreEqual("체험자", _settings.VisitorName);
        }

        /// <summary>
        /// 바꾼 값은 새로 불러온 설정(앱 재시작)에서도 유지되고, 변경값을 지우면 기본값으로 돌아감.
        /// </summary>
        [Test]
        public void 바꾼_값은_다시_불러와도_유지되고_지우면_기본값으로_돌아간다()
        {
            _settings.IsServerConnected = true;
            _settings.VisitorName = "홍길동";

            VisitorSettings reloaded = ScriptableObject.CreateInstance<VisitorSettings>();
            try
            {
                Assert.IsTrue(reloaded.IsServerConnected, "모드 변경이 유지되지 않음");
                Assert.AreEqual("홍길동", reloaded.VisitorName, "이름 변경이 유지되지 않음");

                reloaded.ClearOverrides();

                Assert.IsFalse(_settings.IsServerConnected);
                Assert.AreEqual("체험자", _settings.VisitorName);
            }
            finally
            {
                Object.DestroyImmediate(reloaded);
            }
        }

        /// <summary> 로컬 모드는 설정의 이름을 쓰고, 이름이 비어 있으면 기본 이름을 씀. </summary>
        [Test]
        public void 로컬_모드는_설정_이름을_쓰고_빈_이름이면_기본_이름을_쓴다()
        {
            VisitorInfoProvider provider = new VisitorInfoProvider(_settings, null);

            _settings.VisitorName = "홍길동";
            Assert.IsFalse(provider.IsServerConnected);
            Assert.AreEqual("홍길동", provider.GetNameAsync().GetAwaiter().GetResult());

            _settings.VisitorName = string.Empty;
            Assert.AreEqual(Constants.DefaultVisitorName, provider.GetNameAsync().GetAwaiter().GetResult());
        }

        /// <summary> 서버 모드는 QR로 확인한 서버 이름을 쓰고, 확인한 체험자가 없으면 설정 이름 대신 기본 이름을 씀. </summary>
        [Test]
        public void 서버_모드는_서버_이름을_쓰고_없으면_기본_이름을_쓴다()
        {
            VisitorInfoProvider provider = new VisitorInfoProvider(_settings, null);
            _settings.IsServerConnected = true;
            _settings.VisitorName = "홍길동";

            Assert.IsTrue(provider.IsServerConnected);
            Assert.IsFalse(provider.HasServerVisitor);
            Assert.AreEqual(Constants.DefaultVisitorName, provider.GetNameAsync().GetAwaiter().GetResult(), "QR 확인 전에는 로컬 이름이 아니라 기본 이름이어야 함");

            provider.SetServerVisitor(10, "LLL");
            Assert.IsTrue(provider.HasServerVisitor);
            Assert.AreEqual(10, provider.VisitorIdx);
            Assert.AreEqual("LLL", provider.GetNameAsync().GetAwaiter().GetResult());

            provider.ClearServerVisitor();
            Assert.IsFalse(provider.HasServerVisitor);
            Assert.AreEqual(Constants.DefaultVisitorName, provider.GetNameAsync().GetAwaiter().GetResult());
        }
    }
}
