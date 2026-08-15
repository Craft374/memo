import Foundation

struct MemoTextEdit: Equatable {
    let range: NSRange
    let replacement: String
}

enum MemoTextLogic {
    private static let numberedLineExpression = try! NSRegularExpression(
        pattern: #"^([\t ]*)([0-9]+)\.[\t ]*(.*)$"#
    )

    static func numberedListEdit(in string: NSString, selectedRange: NSRange) -> MemoTextEdit? {
        guard selectedRange.length == 0, selectedRange.location <= string.length else {
            return nil
        }

        let lineRange = string.lineRange(for: NSRange(location: selectedRange.location, length: 0))
        var contentRange = lineRange

        while contentRange.length > 0 {
            let lastCharacter = string.character(at: NSMaxRange(contentRange) - 1)
            guard lastCharacter == 10 || lastCharacter == 13 else {
                break
            }
            contentRange.length -= 1
        }

        let line = string.substring(with: contentRange) as NSString
        guard
            let match = numberedLineExpression.firstMatch(
                in: line as String,
                range: NSRange(location: 0, length: line.length)
            ),
            selectedRange.location >= contentRange.location + match.range(at: 3).location,
            let number = Int(line.substring(with: match.range(at: 2))),
            number < Int.max
        else {
            return nil
        }

        let body = line.substring(with: match.range(at: 3))
        if body.trimmingCharacters(in: .whitespaces).isEmpty {
            return MemoTextEdit(range: contentRange, replacement: "")
        }

        let indent = line.substring(with: match.range(at: 1))
        return MemoTextEdit(range: selectedRange, replacement: "\n\(indent)\(number + 1). ")
    }

    static func searchExpression(
        pattern: String,
        usesRegularExpression: Bool
    ) throws -> NSRegularExpression {
        let resolvedPattern = usesRegularExpression ? pattern : NSRegularExpression.escapedPattern(for: pattern)
        return try NSRegularExpression(pattern: resolvedPattern, options: [.caseInsensitive])
    }

    static func nextMatch(
        in string: String,
        expression: NSRegularExpression,
        startingAt location: Int
    ) -> NSTextCheckingResult? {
        let length = (string as NSString).length
        let start = min(max(0, location), length)
        let trailingRange = NSRange(location: start, length: length - start)

        return expression.firstMatch(in: string, range: trailingRange)
            ?? expression.firstMatch(in: string, range: NSRange(location: 0, length: start))
    }
}
