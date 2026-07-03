import AppKit

final class LineNumberRulerView: NSRulerView {
    weak var textView: NSTextView?

    var fontSize: CGFloat = 15 {
        didSet {
            needsDisplay = true
        }
    }

    private var lineStarts: [Int] = [0]
    private let gutterBackground = NSColor(calibratedRed: 0.055, green: 0.058, blue: 0.065, alpha: 1)
    private let separatorColor = NSColor(calibratedWhite: 0.22, alpha: 1)
    private let numberColor = NSColor(calibratedWhite: 0.52, alpha: 1)

    init(textView: NSTextView, scrollView: NSScrollView) {
        self.textView = textView
        super.init(scrollView: scrollView, orientation: .verticalRuler)
        clientView = textView
        ruleThickness = 46
        fontSize = textView.font?.pointSize ?? 15
        rebuildLineStarts()
    }

    @available(*, unavailable)
    required init(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    func rebuildLineStarts() {
        guard let string = textView?.string as NSString? else {
            lineStarts = [0]
            return
        }

        var starts = [0]
        var searchLocation = 0

        while searchLocation < string.length {
            let range = NSRange(location: searchLocation, length: string.length - searchLocation)
            let newlineRange = string.range(of: "\n", options: [], range: range)

            guard newlineRange.location != NSNotFound else {
                break
            }

            let nextStart = newlineRange.location + newlineRange.length
            starts.append(nextStart)
            searchLocation = nextStart
        }

        lineStarts = starts
        updateRuleThickness()
    }

    override func drawHashMarksAndLabels(in rect: NSRect) {
        guard
            let textView,
            let layoutManager = textView.layoutManager,
            let textContainer = textView.textContainer,
            let scrollView = scrollView
        else {
            return
        }

        gutterBackground.setFill()
        bounds.fill()

        separatorColor.setStroke()
        NSBezierPath.strokeLine(
            from: NSPoint(x: bounds.maxX - 0.5, y: bounds.minY),
            to: NSPoint(x: bounds.maxX - 0.5, y: bounds.maxY)
        )

        layoutManager.ensureLayout(for: textContainer)

        if textView.string.isEmpty {
            updateRuleThickness()
            let y = textView.textContainerOrigin.y + convert(.zero, from: textView).y
            let font = textView.font ?? NSFont.systemFont(ofSize: fontSize)
            drawLineNumber(1, y: y, lineHeight: layoutManager.defaultLineHeight(for: font))
            return
        }

        let visibleRect = scrollView.contentView.bounds
        let textContainerOriginY = textView.textContainerOrigin.y
        let rulerOffsetY = convert(.zero, from: textView).y
        let textLength = (textView.string as NSString).length

        for (index, characterIndex) in lineStarts.enumerated() {
            guard let fragment = lineFragment(forCharacterAt: characterIndex, textLength: textLength, layoutManager: layoutManager) else {
                continue
            }

            let documentY = fragment.minY + textContainerOriginY
            let rulerY = documentY + rulerOffsetY

            if documentY + fragment.height >= visibleRect.minY - 20 && documentY <= visibleRect.maxY + 20 {
                drawLineNumber(index + 1, y: rulerY, lineHeight: fragment.height)
            }
        }
    }

    private func updateRuleThickness() {
        let digits = max(2, String(max(1, lineStarts.count)).count)
        let width = CGFloat(digits) * 8 + 24
        let newThickness = max(46, width)

        if abs(ruleThickness - newThickness) > 0.5 {
            ruleThickness = newThickness
            needsDisplay = true
        }
    }

    private func lineFragment(forCharacterAt characterIndex: Int, textLength: Int, layoutManager: NSLayoutManager) -> NSRect? {
        if characterIndex >= textLength {
            let rect = layoutManager.extraLineFragmentRect
            return rect.isEmpty ? nil : rect
        }

        let glyphCount = layoutManager.numberOfGlyphs
        guard glyphCount > 0 else {
            return nil
        }

        let glyphIndex = min(layoutManager.glyphIndexForCharacter(at: characterIndex), glyphCount - 1)
        return layoutManager.lineFragmentRect(forGlyphAt: glyphIndex, effectiveRange: nil)
    }

    private func drawLineNumber(_ lineNumber: Int, y: CGFloat, lineHeight: CGFloat) {
        let numberFont = NSFont.monospacedDigitSystemFont(ofSize: max(10, min(13, fontSize - 2)), weight: .regular)
        let attributes: [NSAttributedString.Key: Any] = [
            .font: numberFont,
            .foregroundColor: numberColor
        ]
        let text = "\(lineNumber)" as NSString
        let textSize = text.size(withAttributes: attributes)
        let rect = NSRect(
            x: max(4, bounds.width - textSize.width - 12),
            y: y + max(0, (lineHeight - textSize.height) / 2),
            width: textSize.width,
            height: textSize.height
        )

        text.draw(in: rect, withAttributes: attributes)
    }
}
