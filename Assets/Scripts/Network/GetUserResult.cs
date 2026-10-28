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

        // 기록(성공·실패)이 있는 이 존의 레벨 — 레벨 번호(1부터) - 1번째 비트가 켜져 있음
        private readonly int _recordedLevelMask;

        /// <summary> 진행도를 읽지 못한 이유 — 응답에 uid·이름이 있어 원문 대신 이것만 로그에 남김. </summary>
        public string FailReason { get; }

        /// <summary>
        /// 이 존의 레벨(1부터)에 성공·실패 기록이 있는지. 레벨 선택 화면은 기록이 있는 레벨에 결과 화면까지 간 레벨처럼 V를 붙임(CompletedLevelStore).
        /// </summary>
        public bool IsRecorded(int level) =>
            level >= 1 && level <= Constants.LastLevel && (_recordedLevelMask & (1 << (level - 1))) != 0;

        /// <summary> 찾음 여부, 기록이 있는 레벨, 실패 사유로 결과를 만듦. </summary>
        private GetUserResult(bool isFound, int recordedLevelMask, string failReason)
        {
            IsFound = isFound;
            _recordedLevelMask = recordedLevelMask;
            FailReason = failReason;
        }

        /// <summary> 진행도를 읽지 못한 결과를 만듦. </summary>
        public static GetUserResult Failed(string reason) => new(false, NoRecord, reason);

        /// <summary>
        /// getUser 응답 본문을 해석함. JSON 앞뒤에 붙은 글자는 무시하고, result가 true일 때만 찾은 것으로 보며,
        /// 이 존의 레벨(D1~D5) 값 중 null이 아닌 레벨을 모음.
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

            int recordedLevelMask = NoRecord;
            foreach (Match match in ZoneLevelPattern.Matches(json))
            {
                if (match.Groups[2].Value == NullValue) continue;

                // 이 존의 레벨(D1~D5)만 봄 — 서버에 D6처럼 없는 레벨 키가 생겨도 없는 레벨을 기록으로 세지 않음
                if (int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int levelNumber)
                    && levelNumber >= 1 && levelNumber <= Constants.LastLevel)
                    recordedLevelMask |= 1 << (levelNumber - 1);
            }

            return new GetUserResult(true, recordedLevelMask, null);
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
