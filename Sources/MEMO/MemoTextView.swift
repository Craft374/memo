import AppKit

protocol MemoTextViewZoomDelegate: AnyObject {
    func memoTextView(_ textView: MemoTextView, zoomBy delta: CGFloat)
}

final class MemoTextView: NSTextView {
    weak var zoomDelegate: MemoTextViewZoomDelegate?

    private var wheelRemainder: CGFloat = 0

    override func insertText(_ insertString: Any, replacementRange: NSRange) {
        if let text = insertString as? String {
            super.insertText(sanitizedText(text), replacementRange: replacementRange)
            return
        }

        if let text = insertString as? NSAttributedString {
            super.insertText(sanitizedText(text.string), replacementRange: replacementRange)
            return
        }

        super.insertText(insertString, replacementRange: replacementRange)
    }

    override func paste(_ sender: Any?) {
        guard let text = NSPasteboard.general.string(forType: .string) else {
            super.paste(sender)
            return
        }

        insertText(sanitizedText(text), replacementRange: selectedRange())
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
}
