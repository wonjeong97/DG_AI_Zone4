using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Cysharp.Text;
using DGAIZone.App;
using UnityEngine;

namespace DGAIZone.Network
{
    /// <summary>
    /// 체험자 서버 getUser 호출 결과 중 이 존(4존, D1~D5)의 진행도.
    /// 응답 예: {"result": true, "user": {"idx_user": 10, ..., "A1": null, ..., "D5": null}} — 값은 성공 1·실패 0·기록 없음 null.
    /// 없는 uid면 {"result":false,"message":"NOT_FOUND"}. 다른 존(A~C)의 값은 무시함.
    /// </summary>
    public readonly struct GetUserResult
    {
        private const int NoRecord = 0;
        private const string NullValue = "null";

        // JsonUtility는 int 칸의 null과 0을 구분하지 못해 이 존의 레벨 값("D1": 1 등)은 정규식으로 직접 읽음
        private readonly static Regex ZoneLevelPattern =
            new("\"" + Constants.VisitorApi.ZoneCode + "(\\d+)\"\\s*:\\s*(null|\"?\\d+\"?)", RegexOptions.Compiled);

        public bool IsFound { get; }

        /// <summary> 기록(성공·실패)이 있는 이 존의 마지막 레벨 번호(1부터) — 기록이 없으면 0. </summary>
        public int LastRecordedLevel { get; }

        /// <summary> 진행도를 읽지 못한 이유 — 응답에 uid·이름이 있어 원문 대신 이것만 로그에 남김. </summary>
        public string FailReason { get; }

        /// <summary>
        /// 서버 기록으로 열 레벨 수(UnlockedLevelStore.UnlockedLevelCount). 로컬 규칙(결과 화면에서 성공·실패와 상관없이 다음 레벨을 엶)과 같게,
        /// 기록이 있는 마지막 레벨의 다음 레벨까지 열고 Constants.LastLevel을 넘지 않음. 기록이 없으면 1.
        /// </summary>
        public int UnlockedLevelCount => Mathf.Clamp(LastRecordedLevel + 1, 1, Constants.LastLevel);

        /// <summary> 찾음 여부, 마지막 기록 레벨 번호, 실패 사유로 결과를 만듦. </summary>
        private GetUserResult(bool isFound, int lastRecordedLevel, string failReason)
        {
            IsFound = isFound;
            LastRecordedLevel = lastRecordedLevel;
            FailReason = failReason;
        }

        /// <summary> 진행도를 읽지 못한 결과를 만듦. </summary>
        public static GetUserResult Failed(string reason) => new(false, NoRecord, reason);

        /// <summary>
        /// getUser 응답 본문을 해석함. JSON 앞뒤에 붙은 글자는 무시하고, result가 true일 때만 찾은 것으로 보며,
        /// 이 존의 레벨(D1~D5) 값 중 null이 아닌 마지막 레벨을 찾음.
        /// </summary>
        public static GetUserResult Parse(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return Failed("빈 응답");

            string json = ApiJson.ExtractObject(body);

            GetUserResponse response;
            try
            {
                response = JsonUtility.FromJson<GetUserResponse>(json);
            }
            catch (ArgumentException e)
            {
                // JsonUtility 오류 문구에는 응답 본문이 들어가지 않아 uid·이름이 로그에 남지 않음 — 응답이 어떻게 깨졌는지 짐작하는 단서로 남김
                return Failed(ZString.Concat("JSON이 아닌 응답 (", e.Message, ")"));
            }

            if (response == null) return Failed("빈 응답");
            if (!response.result) return Failed(string.IsNullOrEmpty(response.message) ? "result false" : response.message);

            int lastRecordedLevel = NoRecord;
            foreach (Match match in ZoneLevelPattern.Matches(json))
            {
                if (match.Groups[2].Value == NullValue) continue;

                // 이 존의 레벨(D1~D5)만 봄 — 서버에 D6처럼 없는 레벨 키가 생겨도 모든 레벨을 열지 않게 함
                if (int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int levelNumber)
                    && levelNumber >= 1 && levelNumber <= Constants.LastLevel)
                    lastRecordedLevel = Math.Max(lastRecordedLevel, levelNumber);
            }

            return new GetUserResult(true, lastRecordedLevel, null);
        }

        // 응답 최상위의 성공 여부와 실패 메시지만 JsonUtility로 읽음 — 필드 이름은 서버 JSON 키와 같아야 함
        [Serializable]
        private class GetUserResponse
        {
            public bool result;
            public string message;
        }
    }
}
