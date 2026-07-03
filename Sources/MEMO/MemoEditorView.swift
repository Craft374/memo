import AppKit

final class MemoEditorView: NSView, NSTextViewDelegate, MemoTextViewZoomDelegate, MemoTabBarViewDelegate {
    let textView: MemoTextView

    private let tabBarView = MemoTabBarView()
    private let scrollView = NSScrollView()
    private let storage = MemoStorage()
    private let saveQueue = DispatchQueue(label: "com.craft374.memo.save", qos: .utility)
    private let baseFontName = "Menlo"
    private let minimumFontSize: CGFloat = 10
    private let maximumFontSize: CGFloat = 34
    private let defaultFontSize: CGFloat = 15
    private let textColor = NSColor(calibratedWhite: 0.92, alpha: 1)
    private let backgroundColor = NSColor(calibratedRed: 0.075, green: 0.078, blue: 0.085, alpha: 1)
    private let userDefaultsFontSizeKey = "memo.fontSize"

    private var tabs: [MemoTab] = []
    private var selectedTabID: String?
    private var lineNumberRuler: LineNumberRulerView?
    private var tabBarHeightConstraint: NSLayoutConstraint?
    private var autosaveWorkItem: DispatchWorkItem?
    private var fontSize: CGFloat
    private var isLoadingTab = false

    override init(frame frameRect: NSRect) {
        let savedFontSize = UserDefaults.standard.double(forKey: userDefaultsFontSizeKey)
        fontSize = savedFontSize > 0 ? CGFloat(savedFontSize) : defaultFontSize
        textView = MemoTextView(frame: .zero)

        super.init(frame: frameRect)

        wantsLayer = true
        layer?.backgroundColor = backgroundColor.cgColor

        configureTabBar()
        configureScrollView()
        configureTextView()
        loadSavedSession()
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        nil
    }

    deinit {
        NotificationCenter.default.removeObserver(self)
    }

    override func viewDidMoveToWindow() {
        super.viewDidMoveToWindow()
        window?.makeFirstResponder(textView)
    }

    private func configureScrollView() {
        scrollView.translatesAutoresizingMaskIntoConstraints = false
        scrollView.borderType = .noBorder
        scrollView.drawsBackground = true
        scrollView.backgroundColor = backgroundColor
        scrollView.hasVerticalScroller = true
        scrollView.hasHorizontalScroller = false
        scrollView.autohidesScrollers = true
        scrollView.contentView.postsBoundsChangedNotifications = true

        addSubview(scrollView)

        NSLayoutConstraint.activate([
            scrollView.leadingAnchor.constraint(equalTo: leadingAnchor),
            scrollView.trailingAnchor.constraint(equalTo: trailingAnchor),
            scrollView.topAnchor.constraint(equalTo: tabBarView.bottomAnchor),
            scrollView.bottomAnchor.constraint(equalTo: bottomAnchor)
        ])

        NotificationCenter.default.addObserver(
            self,
            selector: #selector(scrollBoundsDidChange(_:)),
            name: NSView.boundsDidChangeNotification,
            object: scrollView.contentView
        )
    }

    private func configureTabBar() {
        tabBarView.translatesAutoresizingMaskIntoConstraints = false
        tabBarView.delegate = self
        addSubview(tabBarView)

        let heightConstraint = tabBarView.heightAnchor.constraint(equalToConstant: 0)
        tabBarHeightConstraint = heightConstraint

        NSLayoutConstraint.activate([
            tabBarView.leadingAnchor.constraint(equalTo: leadingAnchor),
            tabBarView.trailingAnchor.constraint(equalTo: trailingAnchor),
            tabBarView.topAnchor.constraint(equalTo: topAnchor),
            heightConstraint
        ])
    }

    private func configureTextView() {
        let contentSize = scrollView.contentSize

        textView.frame = NSRect(origin: .zero, size: contentSize)
        textView.minSize = NSSize(width: 0, height: contentSize.height)
        textView.maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
        textView.isVerticallyResizable = true
        textView.isHorizontallyResizable = false
        textView.autoresizingMask = [.width]
        textView.textContainerInset = NSSize(width: 14, height: 16)
        textView.textContainer?.containerSize = NSSize(width: contentSize.width, height: CGFloat.greatestFiniteMagnitude)
        textView.textContainer?.widthTracksTextView = true
        textView.backgroundColor = backgroundColor
        textView.drawsBackground = true
        textView.textColor = textColor
        textView.insertionPointColor = textColor
        textView.allowsUndo = true
        textView.isRichText = true
        textView.importsGraphics = false
        textView.usesFindBar = false
        textView.isAutomaticQuoteSubstitutionEnabled = false
        textView.isAutomaticDashSubstitutionEnabled = false
        textView.isAutomaticTextReplacementEnabled = false
        textView.isAutomaticSpellingCorrectionEnabled = false
        textView.delegate = self
        textView.zoomDelegate = self
        textView.font = editorFont(size: fontSize)
        textView.typingAttributes = defaultTypingAttributes(alignment: .left)

        scrollView.documentView = textView

        let ruler = LineNumberRulerView(textView: textView, scrollView: scrollView)
        lineNumberRuler = ruler
        scrollView.verticalRulerView = ruler
        scrollView.hasVerticalRuler = true
        scrollView.rulersVisible = true
    }

    private func loadSavedSession() {
        let session = storage.loadSession()
        tabs = session.tabs.isEmpty ? [MemoTab.create(index: 1)] : session.tabs
        selectedTabID = session.selectedTabID ?? tabs.first?.id

        loadSelectedTab()
        saveSession()
    }

    private func loadSelectedTab() {
        guard let selectedTab = currentTab else {
            return
        }

        isLoadingTab = true

        if let savedText = storage.load(tab: selectedTab) {
            textView.textStorage?.setAttributedString(savedText)
            applyDefaultStyleToLoadedText()
        } else {
            textView.string = ""
            textView.typingAttributes = defaultTypingAttributes(alignment: .left)
        }

        refreshTabBar()
        lineNumberRuler?.rebuildLineStarts()
        lineNumberRuler?.needsDisplay = true
        isLoadingTab = false
    }

    private func applyDefaultStyleToLoadedText() {
        guard let textStorage = textView.textStorage else {
            return
        }

        guard textStorage.length > 0 else {
            textView.typingAttributes = defaultTypingAttributes(alignment: .left)
            return
        }

        let source = NSAttributedString(attributedString: textStorage)
        let cleanedString = sanitizedStoredString(source.string)
        textStorage.setAttributedString(NSAttributedString(string: cleanedString))

        textStorage.beginEditing()
        let fullString = textStorage.string as NSString
        let fullRange = NSRange(location: 0, length: textStorage.length)

        fullString.enumerateSubstrings(in: fullRange, options: [.byParagraphs, .substringNotRequired]) { _, paragraphRange, _, _ in
            let alignment = self.paragraphAlignment(in: source, at: min(paragraphRange.location, max(0, source.length - 1)))
            textStorage.setAttributes(self.defaultTypingAttributes(alignment: alignment), range: paragraphRange)
        }

        if fullRange.length == 0 {
            textView.typingAttributes = defaultTypingAttributes(alignment: .left)
        }

        textStorage.endEditing()
    }

    func textDidChange(_ notification: Notification) {
        guard !isLoadingTab else {
            return
        }

        lineNumberRuler?.rebuildLineStarts()
        lineNumberRuler?.needsDisplay = true
        updateCurrentTabTitle()
        scheduleAutosave()
    }

    func memoTextView(_ textView: MemoTextView, zoomBy delta: CGFloat) {
        changeFontSize(by: delta)
    }

    func changeFontSize(by delta: CGFloat) {
        setFontSize(fontSize + delta)
    }

    func resetFontSize() {
        setFontSize(defaultFontSize)
    }

    func setAlignment(_ alignment: NSTextAlignment) {
        let selectedRange = textView.selectedRange()
        let fullString = textView.string as NSString
        let paragraphRange = fullString.paragraphRange(for: selectedRange)

        textView.setAlignment(alignment, range: paragraphRange)
        textView.typingAttributes = defaultTypingAttributes(alignment: alignment)
        textView.needsDisplay = true
        scheduleAutosave()
    }

    func createNewTab() {
        autosaveCurrentTab(waitUntilDone: false, showError: false)

        let tab = MemoTab.create(index: nextTabNumber())
        tabs.append(tab)
        selectedTabID = tab.id
        loadSelectedTab()
        saveSession()
        window?.makeFirstResponder(textView)
    }

    func closeCurrentTab() {
        guard let id = selectedTabID else {
            return
        }

        closeTab(id: id)
    }

    func selectNextTab() {
        selectTab(offset: 1)
    }

    func selectPreviousTab() {
        selectTab(offset: -1)
    }

    func saveNow(showError: Bool = false) {
        autosaveCurrentTab(waitUntilDone: false, showError: showError)
    }

    func saveImmediately() {
        autosaveWorkItem?.cancel()
        autosaveCurrentTab(waitUntilDone: true, showError: false)
        saveSession(waitUntilDone: true)
    }

    func tabBarView(_ tabBarView: MemoTabBarView, didSelectTab id: String) {
        selectTab(id: id)
    }

    func tabBarView(_ tabBarView: MemoTabBarView, didCloseTab id: String) {
        closeTab(id: id)
    }

    private var currentTab: MemoTab? {
        guard let selectedTabID else {
            return nil
        }

        return tabs.first { $0.id == selectedTabID }
    }

    private func autosaveCurrentTab(waitUntilDone: Bool, showError: Bool) {
        guard let tab = currentTab else {
            return
        }

        let snapshot = NSAttributedString(attributedString: textView.attributedString())
        save(snapshot: snapshot, tab: tab, waitUntilDone: waitUntilDone, showError: showError)
        saveSession(waitUntilDone: waitUntilDone)
    }

    private func selectTab(id: String) {
        guard id != selectedTabID, tabs.contains(where: { $0.id == id }) else {
            return
        }

        autosaveCurrentTab(waitUntilDone: false, showError: false)
        selectedTabID = id
        loadSelectedTab()
        saveSession()
        window?.makeFirstResponder(textView)
    }

    private func closeTab(id: String) {
        guard let closingIndex = tabs.firstIndex(where: { $0.id == id }) else {
            return
        }

        if id == selectedTabID {
            autosaveCurrentTab(waitUntilDone: false, showError: false)
        }

        let removedTab = tabs.remove(at: closingIndex)
        storage.delete(tab: removedTab)

        if tabs.isEmpty {
            tabs.append(MemoTab.create(index: 1))
        }

        if id == selectedTabID {
            let nextIndex = min(closingIndex, tabs.count - 1)
            selectedTabID = tabs[nextIndex].id
            loadSelectedTab()
        } else {
            refreshTabBar()
        }

        saveSession()
        window?.makeFirstResponder(textView)
    }

    private func selectTab(offset: Int) {
        guard tabs.count > 1, let selectedTabID, let currentIndex = tabs.firstIndex(where: { $0.id == selectedTabID }) else {
            return
        }

        let nextIndex = (currentIndex + offset + tabs.count) % tabs.count
        selectTab(id: tabs[nextIndex].id)
    }

    private func updateCurrentTabTitle() {
        guard let selectedTabID, let index = tabs.firstIndex(where: { $0.id == selectedTabID }) else {
            return
        }

        let fallbackTitle = tabs[index].title
        let newTitle = titleFromCurrentText(fallback: fallbackTitle)

        guard tabs[index].title != newTitle else {
            return
        }

        tabs[index].title = newTitle
        refreshTabBar()
        saveSession()
    }

    private func refreshTabBar() {
        let shouldShowTabBar = tabs.count > 1

        tabBarView.isHidden = !shouldShowTabBar
        tabBarHeightConstraint?.constant = shouldShowTabBar ? 32 : 0
        tabBarView.update(tabs: shouldShowTabBar ? tabs : [], selectedTabID: selectedTabID)
        layoutSubtreeIfNeeded()
    }

    private func titleFromCurrentText(fallback: String) -> String {
        let lines = textView.string.split(whereSeparator: \.isNewline)

        for line in lines {
            let title = String(line).trimmingCharacters(in: .whitespacesAndNewlines)

            if !title.isEmpty {
                return String(title.prefix(24))
            }
        }

        return fallback.hasPrefix("메모 ") ? fallback : "메모 \(nextTabNumber())"
    }

    private func nextTabNumber() -> Int {
        var number = 1
        let existingTitles = Set(tabs.map(\.title))

        while existingTitles.contains("메모 \(number)") {
            number += 1
        }

        return number
    }

    private func setFontSize(_ newSize: CGFloat) {
        let clampedSize = min(max(newSize, minimumFontSize), maximumFontSize)
        guard clampedSize != fontSize else {
            return
        }

        fontSize = clampedSize
        UserDefaults.standard.set(Double(clampedSize), forKey: userDefaultsFontSizeKey)

        guard let textStorage = textView.textStorage else {
            return
        }

        let font = editorFont(size: clampedSize)
        let fullRange = NSRange(location: 0, length: textStorage.length)

        textView.font = font
        if fullRange.length > 0 {
            textStorage.addAttribute(.font, value: font, range: fullRange)
        }

        var typingAttributes = textView.typingAttributes
        typingAttributes[.font] = font
        typingAttributes[.foregroundColor] = textColor
        textView.typingAttributes = typingAttributes

        lineNumberRuler?.fontSize = clampedSize
        lineNumberRuler?.rebuildLineStarts()
        lineNumberRuler?.needsDisplay = true
        scheduleAutosave()
    }

    private func scheduleAutosave() {
        autosaveWorkItem?.cancel()

        let item = DispatchWorkItem { [weak self] in
            self?.saveNow()
        }

        autosaveWorkItem = item
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.35, execute: item)
    }

    private func save(snapshot: NSAttributedString, tab: MemoTab, waitUntilDone: Bool, showError: Bool) {
        let work = { [storage, tab] in
            do {
                try storage.save(snapshot, for: tab)
            } catch {
                guard showError else {
                    NSLog("MEMO 저장 실패: \(error.localizedDescription)")
                    return
                }

                DispatchQueue.main.async {
                    let alert = NSAlert()
                    alert.messageText = "저장 실패"
                    alert.informativeText = error.localizedDescription
                    alert.alertStyle = .warning
                    alert.runModal()
                }
            }
        }

        if waitUntilDone {
            saveQueue.sync(execute: work)
        } else {
            saveQueue.async(execute: work)
        }
    }

    private func saveSession(waitUntilDone: Bool = false) {
        let session = MemoSession(tabs: tabs, selectedTabID: selectedTabID)

        let work = { [storage, session] in
            do {
                try storage.saveSession(session)
            } catch {
                NSLog("MEMO 탭 저장 실패: \(error.localizedDescription)")
            }
        }

        if waitUntilDone {
            saveQueue.sync(execute: work)
        } else {
            saveQueue.async(execute: work)
        }
    }

    private func editorFont(size: CGFloat) -> NSFont {
        NSFont(name: baseFontName, size: size) ?? NSFont.monospacedSystemFont(ofSize: size, weight: .regular)
    }

    private func sanitizedStoredString(_ string: String) -> String {
        var result = ""
        result.reserveCapacity(string.count)

        for scalar in string.unicodeScalars {
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

    private func paragraphAlignment(in text: NSAttributedString, at location: Int) -> NSTextAlignment {
        guard text.length > 0 else {
            return .left
        }

        let safeLocation = min(max(0, location), text.length - 1)
        let paragraphStyle = text.attribute(.paragraphStyle, at: safeLocation, effectiveRange: nil) as? NSParagraphStyle
        return paragraphStyle?.alignment ?? .left
    }

    private func defaultTypingAttributes(alignment: NSTextAlignment) -> [NSAttributedString.Key: Any] {
        let paragraphStyle = NSMutableParagraphStyle()
        paragraphStyle.alignment = alignment
        paragraphStyle.lineBreakMode = .byWordWrapping

        return [
            .font: editorFont(size: fontSize),
            .foregroundColor: textColor,
            .paragraphStyle: paragraphStyle
        ]
    }

    @objc private func scrollBoundsDidChange(_ notification: Notification) {
        lineNumberRuler?.needsDisplay = true
    }
}
