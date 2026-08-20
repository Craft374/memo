import AppKit

protocol MemoTabBarViewDelegate: AnyObject {
    func tabBarView(_ tabBarView: MemoTabBarView, didSelectTab id: String)
    func tabBarView(_ tabBarView: MemoTabBarView, didCloseTab id: String)
    func tabBarView(_ tabBarView: MemoTabBarView, didReorderTabs tabs: [MemoTab])
}

final class MemoTabBarView: NSView {
    weak var delegate: MemoTabBarViewDelegate?

    private var tabs: [MemoTab] = []
    private var selectedTabID: String?
    private var tabRects: [String: NSRect] = [:]
    private var closeRects: [String: NSRect] = [:]
    private var draggingTabID: String?
    private var dragStartPoint: NSPoint = .zero
    private var hasDraggedPastThreshold = false

    private let backgroundColor = NSColor(calibratedRed: 0.055, green: 0.058, blue: 0.064, alpha: 1)
    private let selectedColor = NSColor(calibratedRed: 0.10, green: 0.105, blue: 0.115, alpha: 1)
    private let normalColor = NSColor(calibratedRed: 0.075, green: 0.078, blue: 0.086, alpha: 1)
    private let borderColor = NSColor(calibratedWhite: 0.18, alpha: 1)
    private let textColor = NSColor(calibratedWhite: 0.86, alpha: 1)
    private let closeColor = NSColor(calibratedWhite: 0.60, alpha: 1)

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        wantsLayer = true
        layer?.backgroundColor = backgroundColor.cgColor
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        nil
    }

    func update(tabs: [MemoTab], selectedTabID: String?) {
        self.tabs = tabs
        self.selectedTabID = selectedTabID
        needsDisplay = true
    }

    override func draw(_ dirtyRect: NSRect) {
        backgroundColor.setFill()
        bounds.fill()

        tabRects.removeAll(keepingCapacity: true)
        closeRects.removeAll(keepingCapacity: true)

        let font = NSFont.systemFont(ofSize: 12, weight: .regular)
        let titleAttributes: [NSAttributedString.Key: Any] = [
            .font: font,
            .foregroundColor: textColor
        ]
        let closeAttributes: [NSAttributedString.Key: Any] = [
            .font: NSFont.systemFont(ofSize: 13, weight: .regular),
            .foregroundColor: closeColor
        ]

        var x: CGFloat = 0
        let height = bounds.height

        for tab in tabs {
            let title = tab.title as NSString
            let titleWidth = title.size(withAttributes: titleAttributes).width
            let tabWidth = min(max(112, titleWidth + 48), 190)
            let rect = NSRect(x: x, y: 0, width: tabWidth, height: height)

            guard rect.minX < bounds.maxX else {
                break
            }

            tabRects[tab.id] = rect
            let isSelected = tab.id == selectedTabID
            let path = NSBezierPath(rect: rect)
            (isSelected ? selectedColor : normalColor).setFill()
            path.fill()

            borderColor.setStroke()
            NSBezierPath.strokeLine(from: NSPoint(x: rect.maxX - 0.5, y: 0), to: NSPoint(x: rect.maxX - 0.5, y: height))

            let closeText = "×" as NSString
            let closeSize = closeText.size(withAttributes: closeAttributes)
            let closeRect = NSRect(
                x: rect.maxX - closeSize.width - 13,
                y: (height - closeSize.height) / 2,
                width: closeSize.width + 4,
                height: closeSize.height
            )
            closeRects[tab.id] = closeRect.insetBy(dx: -5, dy: -5)

            let textRect = NSRect(x: rect.minX + 13, y: (height - 16) / 2, width: max(20, rect.width - 42), height: 16)
            title.draw(in: textRect, withAttributes: titleAttributes)
            closeText.draw(in: closeRect, withAttributes: closeAttributes)

            x += tabWidth
        }

        borderColor.setStroke()
        NSBezierPath.strokeLine(from: NSPoint(x: 0, y: 0.5), to: NSPoint(x: bounds.width, y: 0.5))
    }

    override func mouseDown(with event: NSEvent) {
        let point = convert(event.locationInWindow, from: nil)

        for (id, rect) in closeRects where rect.contains(point) {
            delegate?.tabBarView(self, didCloseTab: id)
            return
        }

        for (id, rect) in tabRects where rect.contains(point) {
            draggingTabID = id
            dragStartPoint = point
            hasDraggedPastThreshold = false
            return
        }
    }

    override func mouseDragged(with event: NSEvent) {
        guard let draggingTabID else {
            return
        }

        let point = convert(event.locationInWindow, from: nil)

        if !hasDraggedPastThreshold {
            guard abs(point.x - dragStartPoint.x) > 4 else {
                return
            }
            hasDraggedPastThreshold = true
        }

        guard let targetIndex = tabIndex(forX: point.x),
              let reordered = reorderedTabs(moving: draggingTabID, toIndex: targetIndex) else {
            return
        }

        tabs = reordered
        needsDisplay = true
    }

    override func mouseUp(with event: NSEvent) {
        guard let draggingTabID else {
            return
        }

        defer {
            self.draggingTabID = nil
            hasDraggedPastThreshold = false
        }

        if hasDraggedPastThreshold {
            delegate?.tabBarView(self, didReorderTabs: tabs)
        } else if let rect = tabRects[draggingTabID], rect.contains(convert(event.locationInWindow, from: nil)) {
            delegate?.tabBarView(self, didSelectTab: draggingTabID)
        }
    }

    private func tabIndex(forX x: CGFloat) -> Int? {
        for (index, tab) in tabs.enumerated() {
            guard let rect = tabRects[tab.id], x < rect.maxX else {
                continue
            }
            return index
        }
        return tabs.indices.last
    }

    private func reorderedTabs(moving id: String, toIndex targetIndex: Int) -> [MemoTab]? {
        guard let currentIndex = tabs.firstIndex(where: { $0.id == id }), currentIndex != targetIndex else {
            return nil
        }

        var result = tabs
        let tab = result.remove(at: currentIndex)
        result.insert(tab, at: min(targetIndex, result.count))
        return result
    }
}
