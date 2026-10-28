using System.Collections.Generic;

namespace DGAIZone.App
{
    /// <summary>
    /// 이번 체험에서 결과 화면까지 간 레벨(넘어가기 포함)과 QR로 확인한 체험자의 서버 기록(getUser D1~D5 값이 null 아님)이 있는 레벨을
    /// 씬 전환 너머로 보관하는 루트 스코프 서비스. 모든 레벨은 처음부터 열려 있고, 레벨 선택 화면이 이 레벨 버튼에 V를 붙임.
    /// 0_Title 진입·QR 체험자 비움·관리자 레벨 이동 때 Reset()으로 비워 이전 체험자의 V가 다음 체험자에게 넘어가지 않게 함.
    /// </summary>
    public class CompletedLevelStore
    {
        private readonly HashSet<int> _levels = new HashSet<int>();

        /// <summary> 레벨(1부터)을 완료한 것으로 기록함. 1~Constants.LastLevel 밖의 값은 무시하고 false를 돌려줌. </summary>
        public bool MarkCompleted(int level)
        {
            if (level < 1 || level > Constants.LastLevel) return false;
            _levels.Add(level);
            return true;
        }

        /// <summary> 레벨(1부터)을 완료했는지(V를 붙일지). </summary>
        public bool IsCompleted(int level) => _levels.Contains(level);

        /// <summary> 완료한 레벨을 모두 비움. </summary>
        public void Reset() => _levels.Clear();
    }
}
