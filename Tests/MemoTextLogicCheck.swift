import Foundation

@main
enum MemoTextLogicCheck {
    static func main() throws {
        let continued = MemoTextLogic.numberedListEdit(
            in: "1. first" as NSString,
            selectedRange: NSRange(location: 8, length: 0)
        )
        assert(continued == MemoTextEdit(range: NSRange(location: 8, length: 0), replacement: "\n2. "))

        let ended = MemoTextLogic.numberedListEdit(
            in: "1. first\n2. " as NSString,
            selectedRange: NSRange(location: 12, length: 0)
        )
        assert(ended == MemoTextEdit(range: NSRange(location: 9, length: 3), replacement: ""))

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
    }
}
