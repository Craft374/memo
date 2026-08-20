import Foundation

@main
enum MemoTextLogicCheck {
    static func main() throws {
        let continued = MemoTextLogic.listEdit(
            in: "1. first" as NSString,
            selectedRange: NSRange(location: 8, length: 0)
        )
        assert(continued == MemoTextEdit(range: NSRange(location: 8, length: 0), replacement: "\n2. "))

        let ended = MemoTextLogic.listEdit(
            in: "1. first\n2. " as NSString,
            selectedRange: NSRange(location: 12, length: 0)
        )
        assert(ended == MemoTextEdit(range: NSRange(location: 9, length: 3), replacement: ""))

        let bullet = MemoTextLogic.listEdit(
            in: "- first" as NSString,
            selectedRange: NSRange(location: 7, length: 0)
        )
        assert(bullet == MemoTextEdit(range: NSRange(location: 7, length: 0), replacement: "\n- "))

        let resumedNumber = MemoTextLogic.listEdit(
            in: "1. first\n\t- detail\n\t- " as NSString,
            selectedRange: NSRange(location: 22, length: 0)
        )
        assert(resumedNumber == MemoTextEdit(range: NSRange(location: 19, length: 3), replacement: "2. "))

        let indentedCaret = MemoTextLogic.indentationEdit(
            in: "hello" as NSString,
            selectedRange: NSRange(location: 2, length: 0),
            outdent: false
        )
        assert(indentedCaret == MemoTextEdit(
            range: NSRange(location: 0, length: 5),
            replacement: "\thello",
            selection: NSRange(location: 3, length: 0)
        ))

        let indentedLines = MemoTextLogic.indentationEdit(
            in: "one\ntwo\nthree" as NSString,
            selectedRange: NSRange(location: 1, length: 6),
            outdent: false
        )
        assert(indentedLines == MemoTextEdit(
            range: NSRange(location: 0, length: 8),
            replacement: "\tone\n\ttwo\n",
            selection: NSRange(location: 2, length: 7)
        ))

        let outdentedLines = MemoTextLogic.indentationEdit(
            in: "\tone\n\ttwo" as NSString,
            selectedRange: NSRange(location: 0, length: 9),
            outdent: true
        )
        assert(outdentedLines == MemoTextEdit(
            range: NSRange(location: 0, length: 9),
            replacement: "one\ntwo",
            selection: NSRange(location: 0, length: 7)
        ))

        let expression = try MemoTextLogic.searchExpression(
            pattern: #"item ([0-9]+)"#,
            usesRegularExpression: true
        )
        let match = MemoTextLogic.nextMatch(in: "ITEM 12", expression: expression, startingAt: 0)
        assert(match?.range == NSRange(location: 0, length: 7))

        let literal = try MemoTextLogic.searchExpression(
            pattern: "a.b",
            usesRegularExpression: false
        )
        let wrapped = MemoTextLogic.nextMatch(in: "a.b x", expression: literal, startingAt: 5)
        assert(wrapped?.range == NSRange(location: 0, length: 3))

        assert(MemoTextLogic.isHorizontalRuleLine("___"))
        assert(MemoTextLogic.isHorizontalRuleLine("_____"))
        assert(MemoTextLogic.isHorizontalRuleLine("  ___  "))
        assert(!MemoTextLogic.isHorizontalRuleLine("__"))
        assert(!MemoTextLogic.isHorizontalRuleLine("___x"))
        assert(!MemoTextLogic.isHorizontalRuleLine(""))

        let ruleRanges = MemoTextLogic.horizontalRuleLineRanges(in: "hello\n___\nworld\n____\n" as NSString)
        assert(ruleRanges == [NSRange(location: 6, length: 3), NSRange(location: 16, length: 4)])

        assert(MemoTextLogic.horizontalRuleLineRanges(in: "no rules here" as NSString).isEmpty)
    }
}
