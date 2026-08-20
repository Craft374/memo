import Foundation

struct MemoTextEdit: Equatable {
    let range: NSRange
    let replacement: String
    let selection: NSRange?

    init(range: NSRange, replacement: String, selection: NSRange? = nil) {
        self.range = range
        self.replacement = replacement
        self.selection = selection
    }
}

enum MemoTextLogic {
    private static let numberedLineExpression = try! NSRegularExpression(
        pattern: #"^([\t ]*)([0-9]+)\.[\t ]*(.*)$"#
    )
    private static let bulletLineExpression = try! NSRegularExpression(
        pattern: #"^([\t ]*)-(?:[\t ]+(.*))?$"#
    )

    static func listEdit(in string: NSString, selectedRange: NSRange) -> MemoTextEdit? {
        guard selectedRange.length == 0, selectedRange.location <= string.length else {
            return nil
        }

        let lineRange = string.lineRange(for: NSRange(location: selectedRange.location, length: 0))
        let contentRange = contentRange(in: string, lineRange: lineRange)
        let line = string.substring(with: contentRange) as NSString

        if let match = numberedLineExpression.firstMatch(
            in: line as String,
            range: NSRange(location: 0, length: line.length)
        ), selectedRange.location >= contentRange.location + match.range(at: 3).location,
           let number = Int(line.substring(with: match.range(at: 2))),
           number < Int.max {
            let body = line.substring(with: match.range(at: 3))
            if body.trimmingCharacters(in: .whitespaces).isEmpty {
                return MemoTextEdit(range: contentRange, replacement: "")
            }

            let indent = line.substring(with: match.range(at: 1))
            return MemoTextEdit(range: selectedRange, replacement: "\n\(indent)\(number + 1). ")
        }

        guard let match = bulletLineExpression.firstMatch(
            in: line as String,
            range: NSRange(location: 0, length: line.length)
        ) else {
            return nil
        }

        let bodyRange = match.range(at: 2).location == NSNotFound
            ? NSRange(location: line.length, length: 0)
            : match.range(at: 2)
        guard selectedRange.location >= contentRange.location + bodyRange.location else {
            return nil
        }

        let body = line.substring(with: bodyRange)
        let indent = line.substring(with: match.range(at: 1))
        if body.trimmingCharacters(in: .whitespaces).isEmpty {
            if let parent = parentNumber(in: string, before: contentRange.location, bulletIndent: indent) {
                return MemoTextEdit(range: contentRange, replacement: "\(parent.indent)\(parent.number + 1). ")
            }

            return MemoTextEdit(range: contentRange, replacement: "")
        }

        return MemoTextEdit(range: selectedRange, replacement: "\n\(indent)- ")
    }

    static func indentationEdit(
        in string: NSString,
        selectedRange: NSRange,
        outdent: Bool
    ) -> MemoTextEdit? {
        guard selectedRange.location <= string.length, NSMaxRange(selectedRange) <= string.length else {
            return nil
        }

        var coveredRange = selectedRange
        if coveredRange.length > 0 {
            coveredRange.length -= 1
        }

        let affectedRange = string.lineRange(for: coveredRange)
        if affectedRange.length == 0 {
            guard !outdent else {
                return nil
            }

            return MemoTextEdit(
                range: affectedRange,
                replacement: "\t",
                selection: NSRange(location: selectedRange.location + 1, length: 0)
            )
        }

        var replacement = ""
        var changes: [(location: Int, delta: Int)] = []
        var location = affectedRange.location

        while location < NSMaxRange(affectedRange) {
            let lineRange = NSIntersectionRange(
                string.lineRange(for: NSRange(location: location, length: 0)),
                affectedRange
            )

            if outdent {
                if lineRange.length > 0, string.character(at: lineRange.location) == 9 {
                    replacement += string.substring(
                        with: NSRange(location: lineRange.location + 1, length: lineRange.length - 1)
                    )
                    changes.append((lineRange.location, -1))
                } else {
                    replacement += string.substring(with: lineRange)
                }
            } else {
                replacement += "\t" + string.substring(with: lineRange)
                changes.append((lineRange.location, 1))
            }

            location = NSMaxRange(lineRange)
        }

        guard !changes.isEmpty else {
            return nil
        }

        let start = selectedRange.location
        let end = NSMaxRange(selectedRange)
        let newStart = start + changes.reduce(0) { result, change in
            result + ((change.location < start || (change.delta > 0 && change.location == start)) ? change.delta : 0)
        }
        let newEnd = end + changes.reduce(0) { result, change in
            result + (change.location < end ? change.delta : 0)
        }

        return MemoTextEdit(
            range: affectedRange,
            replacement: replacement,
            selection: NSRange(location: newStart, length: max(0, newEnd - newStart))
        )
    }

    static func isHorizontalRuleLine(_ line: String) -> Bool {
        let trimmed = line.trimmingCharacters(in: .whitespaces)
        return trimmed.count >= 3 && trimmed.allSatisfy { $0 == "_" }
    }

    static func horizontalRuleLineRanges(in string: NSString) -> [NSRange] {
        guard string.length > 0 else {
            return []
        }

        var ranges: [NSRange] = []
        var location = 0

        while location < string.length {
            let lineRange = string.lineRange(for: NSRange(location: location, length: 0))
            let range = contentRange(in: string, lineRange: lineRange)

            if isHorizontalRuleLine(string.substring(with: range)) {
                ranges.append(range)
            }

            location = NSMaxRange(lineRange)
        }

        return ranges
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

    private static func contentRange(in string: NSString, lineRange: NSRange) -> NSRange {
        var range = lineRange

        while range.length > 0 {
            let lastCharacter = string.character(at: NSMaxRange(range) - 1)
            guard lastCharacter == 10 || lastCharacter == 13 else {
                break
            }
            range.length -= 1
        }

        return range
    }

    private static func parentNumber(
        in string: NSString,
        before location: Int,
        bulletIndent: String
    ) -> (indent: String, number: Int)? {
        var location = location

        while location > 0 {
            let lineRange = string.lineRange(for: NSRange(location: location - 1, length: 0))
            let line = string.substring(with: contentRange(in: string, lineRange: lineRange)) as NSString
            let matchRange = NSRange(location: 0, length: line.length)

            if let match = numberedLineExpression.firstMatch(in: line as String, range: matchRange) {
                let indent = line.substring(with: match.range(at: 1))
                if bulletIndent.count > indent.count,
                   bulletIndent.hasPrefix(indent),
                   let number = Int(line.substring(with: match.range(at: 2))),
                   number < Int.max {
                    return (indent, number)
                }
            } else if bulletLineExpression.firstMatch(in: line as String, range: matchRange) == nil {
                return nil
            }

            location = lineRange.location
        }

        return nil
    }
}
