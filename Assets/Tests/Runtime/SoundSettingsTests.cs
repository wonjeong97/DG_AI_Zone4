using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DGAIZone.App;
using HuliacDev.Data;
using HuliacDev.UI;
using HuliacDev.Utils;
using NUnit.Framework;
using UnityEngine;

namespace DGAIZone.Tests
{
    /// <summary>
    /// 코드가 재생하는 효과음 키(Constants.Sounds)가 Settings.json에 등록돼 있고 파일도 있는지(1존과 같은 검사), 루트 GameLifetimeScope 프리팹에
    /// SoundManager가 있는지 검증함. 하나라도 어긋나면 경고 로그만 남고 소리 없이 넘어가 놓치기 쉬움.
    /// </summary>
    public class SoundSettingsTests
    {
        private const string SettingsFileName = "Settings.json"; // 템플릿 AppSettingsProvider가 읽는 StreamingAssets 파일

        [Test]
        public void 효과음_키는_모두_Settings_json에_등록돼_있고_파일도_있다()
        {
            Settings settings = JsonLoader.Load<Settings>(SettingsFileName);
            Assert.IsNotNull(settings, "Settings.json을 읽지 못함");
            Assert.IsNotNull(settings.sounds, "Settings.json에 sounds 항목이 없음");

            Dictionary<string, SoundSetting> byKey = new Dictionary<string, SoundSetting>();
            foreach (SoundSetting s in settings.sounds) byKey[s.key] = s;

            foreach (FieldInfo field in typeof(Constants.Sounds).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                string key = (string)field.GetValue(null);
                Assert.IsTrue(byKey.TryGetValue(key, out SoundSetting setting), $"Settings.json에 '{key}' 키가 없음");
                Assert.IsTrue(File.Exists(Path.Combine(Application.streamingAssetsPath, setting.clipPath)), $"'{key}'의 파일이 없음: {setting.clipPath}");
            }
        }

        [Test]
        public void 루트_GameLifetimeScope_프리팹에_SoundManager가_있다()
        {
#if UNITY_EDITOR
            GameObject root = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameLifetimeScope.prefab");
            Assert.IsNotNull(root, "Assets/Prefabs/GameLifetimeScope.prefab을 찾지 못함");
            Assert.IsNotNull(root.GetComponentInChildren<SoundManager>(true), "루트 GameLifetimeScope 프리팹에 SoundManager가 없어 효과음이 나지 않음");
#else
            Assert.Ignore("프리팹은 에디터에서만 경로로 읽을 수 있음");
#endif
        }
    }
}
