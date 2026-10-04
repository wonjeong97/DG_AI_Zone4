using HuliacDev.UI;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DGAIZone.App
{
    /// <summary>
    /// 효과음 재생 공용 유틸(Constants.Sounds 키). SoundManager는 루트 GameLifetimeScope 프리팹에 있으며, 없으면 소리 없이 넘어가되 경고를 남김.
    /// 키가 Settings.json에 없거나 설정을 아직 못 읽은 경우의 경고는 SoundManager가 남김. 로그 태그에는 넘겨받은 logger의 대상 클래스(T) 이름이 남음.
    /// </summary>
    public static class SoundEffects
    {
        /// <summary> key 효과음을 재생함. </summary>
        public static void Play<T>(SoundManager soundManager, string key, ILogger<T> logger)
        {
            if (soundManager)
            {
                soundManager.PlaySFX(key);
                return;
            }

            if (logger != null) logger.ZLogWarning($"[{typeof(T).Name}] SoundManager가 null이라 '{key}' 효과음을 재생할 수 없음.");
        }
    }
}
