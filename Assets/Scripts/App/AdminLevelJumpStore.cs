namespace DGAIZone.App
{
    /// <summary>
    /// 관리자 화면의 레벨 이동을 씬 전환 너머로 보관하는 루트 스코프 서비스.
    /// 관리자 화면이 Begin으로 이동할 레벨을 기록하면, 2_LevelSelect가 그 레벨을 고른 것처럼 스토리를 바로 띄우고(TryTakePendingStoryLevel),
    /// 이 판의 결과 화면은 다음 버튼으로 타이틀의 관리자 화면에 돌아가며(OpenAdminOnTitle) 결과를 서버에 올리지 않음.
    /// 0_Title 진입 시 EndLevelJump로 이동 표시를 비워 다음 체험자의 판이 관리자 판으로 남지 않게 함.
    /// </summary>
    public class AdminLevelJumpStore
    {
        private const int NoPendingLevel = 0;

        private int _pendingStoryLevel = NoPendingLevel;

        /// <summary> 관리자 레벨 이동으로 시작한 판인지(타이틀로 돌아가기 전까지 유지). </summary>
        public bool IsLevelJump { get; private set; }

        /// <summary> 다음 0_Title 진입 때 비밀번호 없이 관리자 화면을 바로 열지. AdminTrigger가 열면서 비움. </summary>
        public bool OpenAdminOnTitle { get; set; }

        /// <summary> 관리자 레벨 이동을 시작함. level(1부터)은 2_LevelSelect가 바로 스토리를 띄울 레벨. </summary>
        public void Begin(int level)
        {
            IsLevelJump = true;
            _pendingStoryLevel = level;
        }

        /// <summary> 바로 스토리를 띄울 레벨이 있으면 꺼내고 비움(한 번만 쓰임). </summary>
        public bool TryTakePendingStoryLevel(out int level)
        {
            level = _pendingStoryLevel;
            _pendingStoryLevel = NoPendingLevel;
            return level != NoPendingLevel;
        }

        /// <summary> 관리자 레벨 이동 표시와 남은 스토리 레벨을 비움. 0_Title 진입 시 호출됨(OpenAdminOnTitle은 AdminTrigger가 따로 비움). </summary>
        public void EndLevelJump()
        {
            IsLevelJump = false;
            _pendingStoryLevel = NoPendingLevel;
        }
    }
}
