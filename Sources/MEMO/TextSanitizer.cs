using System.Text;

namespace Memo;

internal static class TextSanitizer
{
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;
    private const char ByteOrderMark = (char)0xFEFF;
    private const char ObjectReplacement = (char)0xFFFC;

    // 웹/트위터 등에서 복사한 텍스트의 특수 개행·비표시 문자 정리 (맥 버전과 동일)
    public static string Sanitize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            switch (c)
            {
                case LineSeparator:
                case ParagraphSeparator:
                    builder.Append('\n');
                    break;
                case ByteOrderMark:
                case ObjectReplacement:
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }
}
