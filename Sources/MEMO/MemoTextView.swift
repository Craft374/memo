import AppKit

protocol MemoTextViewZoomDelegate: AnyObject {
    func memoTextView(_ textView: MemoTextView, zoomBy delta: CGFloat)
}

final class MemoTextView: NSTextView {
    weak var zoomDelegate: MemoTextViewZoomDelegate?

    private var wheelRemainder: CGFloat = 0
    private let horizontalRuleColor = NSColor(calibratedWhite: 0.32, alpha: 1)

    // ___ 처럼 밑줄 3개 이상으로만 이루어진 줄은 글자를 투명하게 감추고 그 위에 가로선을 그린다.
    // 실제 텍스트는 그대로 저장되므로 RTF/복사-붙여넣기가 별도 처리 없이 그대로 동작한다.
    override func draw(_ dirtyRect: NSRect) {
        let ranges = MemoTextLogic.horizontalRuleLineRanges(in: string as NSString)
        hideHorizontalRuleGlyphs(ranges)
        super.draw(dirtyRect)
        drawHorizontalRules(ranges, in: dirtyRect)
    }

    private func hideHorizontalRuleGlyphs(_ ranges: [NSRange]) {
        guard let layoutManager else {
            return
        }

        layoutManager.removeTemporaryAttribute(.foregroundColor, forCharacterRange: NSRange(location: 0, length: (string as NSString).length))

        for range in ranges {
            layoutManager.addTemporaryAttribute(.foregroundColor, value: NSColor.clear, forCharacterRange: range)
        }
    }

    private func drawHorizontalRules(_ ranges: [NSRange], in dirtyRect: NSRect) {
        guard !ranges.isEmpty, let layoutManager, let textContainer else {
            return
        }

        let glyphCount = layoutManager.numberOfGlyphs
        guard glyphCount > 0 else {
            return
        }

        let origin = textContainerOrigin
        let width = horizontalRuleWidth(for: textContainer)
        let path = NSBezierPath()

        for range in ranges {
            let glyphIndex = min(layoutManager.glyphIndexForCharacter(at: range.location), glyphCount - 1)
            let fragment = layoutManager.lineFragmentRect(forGlyphAt: glyphIndex, effectiveRange: nil)
            let y = (fragment.midY + origin.y).rounded() + 0.5

            guard y >= dirtyRect.minY - 4, y <= dirtyRect.maxY + 4 else {
                continue
            }

            path.move(to: NSPoint(x: origin.x, y: y))
            path.line(to: NSPoint(x: origin.x + width, y: y))
        }

        horizontalRuleColor.setStroke()
        path.stroke()
    }

    private func horizontalRuleWidth(for textContainer: NSTextContainer) -> CGFloat {
        let containerWidth = textContainer.size.width
        if containerWidth.isFinite, containerWidth > 0, containerWidth < 100_000 {
            return containerWidth
        }

        // ponytail: 자동 줄바꿈이 꺼진 상태(가로 스크롤)에서는 컨테이너 폭이 무한대이므로
        // 보이는 뷰 폭으로 대체한다. 가로 스크롤 위치에 따라 선 길이가 맞지 않을 수 있음.
        return max(bounds.width, enclosingScrollView?.contentSize.width ?? bounds.width) - textContainerOrigin.x * 2
    }

    override func insertText(_ insertString: Any, replacementRange: NSRange) {
        if let text = insertString as? String {
            super.insertText(sanitizedText(text), replacementRange: replacementRange)
            breakUndoCoalescing()
            return
        }

        if let text = insertString as? NSAttributedString {
            super.insertText(sanitizedText(text.string), replacementRange: replacementRange)
            breakUndoCoalescing()
            return
        }

        super.insertText(insertString, replacementRange: replacementRange)
        breakUndoCoalescing()
    }

    override func paste(_ sender: Any?) {
        guard let text = NSPasteboard.general.string(forType: .string) else {
            super.paste(sender)
            return
        }

        insertText(sanitizedText(text), replacementRange: selectedRange())
    }

    // 드래그 앤 드롭과 서비스 메뉴는 paste를 거치지 않으므로 여기서 서식을 벗긴다.
    // 파일 드롭은 경로 문자열이 끼어들지 않도록 기본 동작에 맡긴다.
    override func readSelection(from pboard: NSPasteboard) -> Bool {
        guard pboard.availableType(from: [.fileURL]) == nil,
              let text = pboard.string(forType: .string) else {
            return super.readSelection(from: pboard)
        }

        insertText(text, replacementRange: selectedRange())
        return true
    }

    override func copy(_ sender: Any?) {
        let range = selectedRange()
        guard range.length > 0 else {
            return
        }

        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString((string as NSString).substring(with: range), forType: .string)
    }

    override func cut(_ sender: Any?) {
        let range = selectedRange()
        guard range.length > 0 else {
            return
        }

        copy(sender)
        insertText("", replacementRange: range)
    }

    override func insertNewline(_ sender: Any?) {
        if let edit = MemoTextLogic.listEdit(in: string as NSString, selectedRange: selectedRange()) {
            insertText(edit.replacement, replacementRange: edit.range)
            return
        }

        super.insertNewline(sender)
    }

    override func insertTab(_ sender: Any?) {
        applyIndentation(outdent: false)
    }

    override func insertBacktab(_ sender: Any?) {
        applyIndentation(outdent: true)
    }

    override func scrollWheel(with event: NSEvent) {
        let flags = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
        guard flags.contains(.command) else {
            super.scrollWheel(with: event)
            return
        }

        let delta = event.scrollingDeltaY
        guard abs(delta) > 0.01 else {
            return
        }

        let threshold: CGFloat = event.hasPreciseScrollingDeltas ? 5 : 1
        let fontStep: CGFloat = 0.25
        wheelRemainder += delta

        while abs(wheelRemainder) >= threshold {
            let step = wheelRemainder > 0 ? 1 : -1
            zoomDelegate?.memoTextView(self, zoomBy: CGFloat(step) * fontStep)
            wheelRemainder -= CGFloat(step) * threshold
        }
    }

    private func sanitizedText(_ text: String) -> String {
        var result = ""
        result.reserveCapacity(text.count)

        for scalar in text.unicodeScalars {
            switch scalar.value {
            case 0x2028, 0x2029:
                result.append("\n")
            case 0xFEFF, 0xFFFC:
                continue
            default:
                result.unicodeScalars.append(scalar)
            }
        }

        return result
    }

    private func applyIndentation(outdent: Bool) {
        guard let edit = MemoTextLogic.indentationEdit(
            in: string as NSString,
            selectedRange: selectedRange(),
            outdent: outdent
        ) else {
            return
        }

        insertText(edit.replacement, replacementRange: edit.range)
        if let selection = edit.selection {
            setSelectedRange(selection)
        }
    }
}
