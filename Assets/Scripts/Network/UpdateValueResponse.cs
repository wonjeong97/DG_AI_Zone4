using System;
using UnityEngine;

namespace DGAIZone.Network
{
    /// <summary>
    /// 체험자 서버 updateValue 응답 — 예: {"result":true,"idx_user":8,"code":"D1","value":0}.
    /// 저장 여부(result)만 씀. 필드 이름은 서버 JSON 키와 같아야 JsonUtility가 읽음.
    /// </summary>
    [Serializable]
    public class UpdateValueResponse
    {
        public bool result;

        /// <summary> 응답 본문이 저장 성공(result true)인지 판정함. JSON 앞뒤에 붙은 글자는 무시하고, JSON이 아니거나 result가 false면 실패로 봄. </summary>
        public static bool IsSaved(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;

            try
            {
                UpdateValueResponse response = JsonUtility.FromJson<UpdateValueResponse>(ApiJson.ExtractObject(body));
                return response != null && response.result;
            }
            catch (ArgumentException)
            {
                // JSON 형식이 아닌 응답(오류 페이지 등)
                return false;
            }
        }
    }
}
