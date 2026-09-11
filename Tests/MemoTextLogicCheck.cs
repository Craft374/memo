using Memo;

// 맥 버전 Tests/MemoTextLogicCheck.swift와 같은 항목
Check(MemoTextLogic.ListEdit("1. first", 8) == new TextEdit(8, 0, "\n2. "), "번호 목록 이어쓰기");
Check(MemoTextLogic.ListEdit("1. first\n2. ", 12) == new TextEdit(9, 3, ""), "빈 번호 항목에서 목록 끝내기");
Check(MemoTextLogic.ListEdit("- first", 7) == new TextEdit(7, 0, "\n- "), "글머리표 이어쓰기");
Check(MemoTextLogic.ListEdit("1. first\n\t- detail\n\t- ", 22) == new TextEdit(19, 3, "2. "), "하위 글머리표에서 부모 번호로 복귀");

Check(MemoTextLogic.IndentationEdit("hello", 2, 0, outdent: false) == new TextEdit(0, 5, "\thello", 3, 0), "커서 줄 들여쓰기");
Check(MemoTextLogic.IndentationEdit("one\ntwo\nthree", 1, 6, outdent: false) == new TextEdit(0, 8, "\tone\n\ttwo\n", 2, 7), "여러 줄 들여쓰기");
Check(MemoTextLogic.IndentationEdit("\tone\n\ttwo", 0, 9, outdent: true) == new TextEdit(0, 9, "one\ntwo", 0, 7), "여러 줄 내어쓰기");

var expression = MemoTextLogic.SearchRegex("item ([0-9]+)", useRegex: true);
var match = MemoTextLogic.NextMatch(expression, "ITEM 12", 0);
Check(match is { Index: 0, Length: 7 }, "정규식 대소문자 무시 검색");

var literal = MemoTextLogic.SearchRegex("a.b", useRegex: false);
var wrapped = MemoTextLogic.NextMatch(literal, "a.b x", 5);
Check(wrapped is { Index: 0, Length: 3 }, "일반 검색 처음으로 돌아가기");

Check(MemoTextLogic.IsHorizontalRuleLine("___"), "___");
Check(MemoTextLogic.IsHorizontalRuleLine("_____"), "_____");
Check(MemoTextLogic.IsHorizontalRuleLine("  ___  "), "공백 포함 ___");
Check(!MemoTextLogic.IsHorizontalRuleLine("__"), "__ 는 구분선 아님");
Check(!MemoTextLogic.IsHorizontalRuleLine("___x"), "___x 는 구분선 아님");
Check(!MemoTextLogic.IsHorizontalRuleLine(""), "빈 줄은 구분선 아님");

Check(MemoTextLogic.HorizontalRuleLineRanges("hello\n___\nworld\n____\n").SequenceEqual(new[] { (6, 3), (16, 4) }), "구분선 줄 범위");
Check(MemoTextLogic.HorizontalRuleLineRanges("no rules here").Count == 0, "구분선 없음");

Console.WriteLine("MemoTextLogic OK");

static void Check(bool condition, string name)
{
    if (!condition)
    {
        throw new Exception($"실패: {name}");
    }
}
