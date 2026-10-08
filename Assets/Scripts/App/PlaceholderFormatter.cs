using System;

namespace DGAIZone.App
{
    /// <summary>
    /// 스토리·미션 문구의 자리표시자({name}, 숫자 자리표시자, 숫자 뒤 조사 자리표시자)를 실제 값으로 치환하는 공용 유틸.
    /// 이미 치환한 문구에는 자리표시자가 남지 않으므로, 값이 늦게 정해지면(체험자 이름 로드 등) 원본 템플릿(LevelData 문구 등)을 다시 넘겨 치환해야 함.
    /// </summary>
    public static class PlaceholderFormatter
    {
        /// <summary> text의 {name}을 visitorName으로 치환함. visitorName이 비어 있으면 Constants.DefaultVisitorName을 씀. </summary>
        public static string ReplaceVisitorName(string text, string visitorName)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string name = string.IsNullOrEmpty(visitorName) ? Constants.DefaultVisitorName : visitorName;
            return text.Replace(Constants.VisitorPlaceholder, name);
        }

        /// <summary>
        /// text의 숫자 자리표시자(예: {distance})를 value로 치환하고, 같은 이름에 조사 접미사를 붙인 자리표시자(예: {distance|이에요})는
        /// value를 읽었을 때 받침 유무에 맞는 "이에요"/"예요"로 치환함.
        /// </summary>
        public static string ReplaceNumber(string text, string placeholder, int value)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string copulaPlaceholder = placeholder.Insert(placeholder.Length - 1, Constants.MissionPlaceholders.CopulaSuffix);
            return text.Replace(copulaPlaceholder, GetCopula(value)).Replace(placeholder, value.ToString());
        }

        /// <summary>
        /// 숫자를 한국어로 읽었을 때 받침이 있으면 "이에요", 없으면 "예요"를 반환함(예: 10이에요, 20이에요, 5예요).
        /// 끝자리가 0이면 십·백·천·만·억으로 끝나 모두 받침이 있고, 1(일)·3(삼)·6(육)·7(칠)·8(팔)도 받침이 있음.
        /// </summary>
        public static string GetCopula(int number)
        {
            int lastDigit = Math.Abs(number % 10);
            bool hasFinalConsonant = lastDigit == 0 || lastDigit == 1 || lastDigit == 3 || lastDigit == 6 || lastDigit == 7 || lastDigit == 8;
            return hasFinalConsonant ? "이에요" : "예요";
        }

        /// <summary>
        /// word 뒤에 받침 유무에 맞는 주격 조사를 붙임(예: 홍길동이, 김철수가). 마지막 글자가 한글 음절이 아니면(영문·숫자 등) "이(가)"를 붙임.
        /// </summary>
        public static string AppendSubjectParticle(string word)
        {
            if (string.IsNullOrEmpty(word)) return string.Empty;

            char last = word[word.Length - 1];
            if (last < '가' || last > '힣') return word + "이(가)";

            bool hasFinalConsonant = (last - '가') % 28 != 0;
            return word + (hasFinalConsonant ? "이" : "가");
        }
    }
}
