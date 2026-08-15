import AppKit

final class MemoEditorView: NSView, NSTextViewDelegate, NSTextFieldDelegate, MemoTextViewZoomDelegate, MemoTabBarViewDelegate {
    let textView: MemoTextView

    private let tabBarView = MemoTabBarView()
    private let titleTextField = NSTextField()
    private let scrollView = NSScrollView()
    private let storage = MemoStorage()
    private let saveQueue = DispatchQueue(label: "com.craft374.memo.save", qos: .utility)
    private let baseFontName = "Menlo"
    private let minimumFontSize: CGFloat = 10
    private let maximumFontSize: CGFloat = 300
    private let defaultFontSize: CGFloat = 15
    private let textColor = NSColor(calibratedWhite: 0.92, alpha: 1)
    private let backgroundColor = NSColor(calibratedRed: 0.075, green: 0.078, blue: 0.085, alpha: 1)
    private let userDefaultsFontSizeKey = "memo.fontSize"
    private let userDefaultsLineWrapKey = "memo.lineWrapEnabled"

    private var tabs: [MemoTab] = []
    private var selectedTabID: String?
    private var lineNumberRuler: LineNumberRulerView?
    private var tabBarHeightConstraint: NSLayoutConstraint?
    private var autosaveWorkItem: DispatchWorkItem?
    private var fontSize: CGFloat
    private var lineWrapEnabled: Bool
    private var isLoadingTab = false
    private var isNormalizingTextAttributes = false
    private var isNormalizingTypingAttributes = false
    private var lastSearchPattern = ""
    private var lastSearchUsesRegularExpression = false
    private var lastFoundRange: NSRange?

    override init(frame frameRect: NSRect) {
        let savedFontSize = UserDefaults.standard.double(forKey: userDefaultsFontSizeKey)
        fontSize = savedFontSize > 0 ? CGFloat(savedFontSize) : defaultFontSize
        lineWrapEnabled = UserDefaults.standard.object(forKey: userDefaultsLineWrapKey) as? Bool ?? true
        textView = MemoTextView(frame: .zero)

        super.init(frame: frameRect)

        wantsLayer = true
        layer?.backgroundColor = backgroundColor.cgColor

        configureTabBar()
        configureTitleTextField()
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
            scrollView.topAnchor.constraint(equalTo: titleTextField.bottomAnchor, constant: 4),
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

    private func configureTitleTextField() {
        titleTextField.translatesAutoresizingMaskIntoConstraints = false
        titleTextField.delegate = self
        titleTextField.font = NSFont.systemFont(ofSize: 26, weight: .bold)
        titleTextField.textColor = textColor
        titleTextField.placeholderString = "제목 없음"
        titleTextField.isBordered = false
        titleTextField.isBezeled = false
        titleTextField.drawsBackground = false
        titleTextField.focusRingType = .none
        titleTextField.maximumNumberOfLines = 1
        titleTextField.lineBreakMode = .byTruncatingTail
        titleTextField.cell?.usesSingleLineMode = true

        addSubview(titleTextField)

        NSLayoutConstraint.activate([
            titleTextField.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 14),
            titleTextField.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -14),
            titleTextField.topAnchor.constraint(equalTo: tabBarView.bottomAnchor, constant: 14),
            titleTextField.heightAnchor.constraint(equalToConstant: 38)
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
        textView.textContainerInset = NSSize(width: 14, height: 8)
        textView.backgroundColor = backgroundColor
        textView.drawsBackground = true
        textView.textColor = textColor
        textView.insertionPointColor = textColor
        textView.allowsUndo = true
        textView.isRichText = true
        textView.importsGraphics = false
        textView.usesFindBar = false
        textView.smartInsertDeleteEnabled = false
        textView.isAutomaticQuoteSubstitutionEnabled = false
        textView.isAutomaticLinkDetectionEnabled = false
        textView.isAutomaticDataDetectionEnabled = false
        textView.isAutomaticDashSubstitutionEnabled = false
        textView.isAutomaticTextReplacementEnabled = false
        textView.isAutomaticSpellingCorrectionEnabled = false
        textView.isAutomaticTextCompletionEnabled = false
        textView.enabledTextCheckingTypes = 0
        textView.delegate = self
        textView.zoomDelegate = self
        textView.font = editorFont(size: fontSize)
        textView.typingAttributes = defaultTypingAttributes(alignment: .left)
        applyLineWrapSetting()

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
        let didLoadStoredText: Bool
        var didMigrateLegacyTitle = false

        if let savedText = storage.load(tab: selectedTab) {
            didLoadStoredText = true
            textView.textStorage?.setAttributedString(savedText)
            applyDefaultStyleToLoadedText()

            if !selectedTab.hasSeparateTitle {
                didMigrateLegacyTitle = migrateLegacyTitle(for: selectedTab)
            }
        } else {
            didLoadStoredText = false
            textView.string = ""
            textView.typingAttributes = defaultTypingAttributes(alignment: .left)

            if !selectedTab.hasSeparateTitle {
                markCurrentTabAsUsingSeparateTitle()
                didMigrateLegacyTitle = true
            }
        }

        titleTextField.stringValue = currentTab?.title ?? ""

        refreshTabBar()
        lineNumberRuler?.rebuildLineStarts()
        lineNumberRuler?.needsDisplay = true
        isLoadingTab = false
        normalizeCurrentTypingAttributes()

        if didLoadStoredText || didMigrateLegacyTitle {
            autosaveCurrentTab(waitUntilDone: false, showError: false)
        }

        if didMigrateLegacyTitle {
            saveSession()
        }
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
        applyDefaultAttributes(to: textStorage, preservingAlignmentFrom: source)

        if textStorage.length == 0 {
            textView.typingAttributes = defaultTypingAttributes(alignment: .left)
        }

        textStorage.endEditing()
        normalizeCurrentTypingAttributes()
    }

    func textDidChange(_ notification: Notification) {
        guard !isLoadingTab, !isNormalizingTextAttributes else {
            return
        }

        normalizeTextAttributesInCurrentDocument()
        lineNumberRuler?.rebuildLineStarts()
        lineNumberRuler?.needsDisplay = true
        scheduleAutosave()
    }

    func controlTextDidChange(_ notification: Notification) {
        guard !isLoadingTab, notification.object as? NSTextField === titleTextField else {
            return
        }

        updateCurrentTabTitle()
    }

    func controlTextDidEndEditing(_ notification: Notification) {
        guard notification.object as? NSTextField === titleTextField else {
            return
        }

        updateCurrentTabTitle()
    }

    func control(_ control: NSControl, textView: NSTextView, doCommandBy commandSelector: Selector) -> Bool {
        guard control === titleTextField, commandSelector == #selector(NSResponder.insertNewline(_:)) else {
            return false
        }

        updateCurrentTabTitle()
        window?.makeFirstResponder(self.textView)
        return true
    }

    func textView(
        _ textView: NSTextView,
        shouldChangeTypingAttributes oldTypingAttributes: [String: Any],
        toAttributes newTypingAttributes: [NSAttributedString.Key: Any]
    ) -> [NSAttributedString.Key: Any] {
        normalizedTypingAttributes(from: newTypingAttributes)
    }

    func textViewDidChangeTypingAttributes(_ notification: Notification) {
        normalizeCurrentTypingAttributes()
    }

    func textViewDidChangeSelection(_ notification: Notification) {
        normalizeCurrentTypingAttributes()
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

    var isLineWrapEnabled: Bool {
        lineWrapEnabled
    }

    func toggleLineWrap() {
        lineWrapEnabled.toggle()
        UserDefaults.standard.set(lineWrapEnabled, forKey: userDefaultsLineWrapKey)
        applyLineWrapSetting()
        applyParagraphStylesToCurrentText()
        lineNumberRuler?.rebuildLineStarts()
        lineNumberRuler?.needsDisplay = true
        scheduleAutosave()
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

    func showFind() {
        let alert = NSAlert()
        alert.messageText = "검색"
        alert.informativeText = "일반 검색과 정규식을 사용할 수 있습니다."

        let accessoryView = NSView(frame: NSRect(x: 0, y: 0, width: 340, height: 64))
        let searchField = NSSearchField(frame: NSRect(x: 0, y: 34, width: 340, height: 24))
        searchField.placeholderString = "찾을 내용"
        searchField.stringValue = lastSearchPattern

        let regularExpressionButton = NSButton(
            checkboxWithTitle: "정규식",
            target: nil,
            action: nil
        )
        regularExpressionButton.frame = NSRect(x: 0, y: 2, width: 90, height: 24)
        regularExpressionButton.state = lastSearchUsesRegularExpression ? .on : .off

        accessoryView.addSubview(searchField)
        accessoryView.addSubview(regularExpressionButton)
        alert.accessoryView = accessoryView
        alert.addButton(withTitle: "다음 찾기")
        alert.addButton(withTitle: "취소")
        alert.window.initialFirstResponder = searchField

        guard alert.runModal() == .alertFirstButtonReturn else {
            window?.makeFirstResponder(textView)
            return
        }

        updateSearchOptions(
            pattern: searchField.stringValue,
            usesRegularExpression: regularExpressionButton.state == .on
        )

        guard !lastSearchPattern.isEmpty else {
            showMessage("검색", detail: "찾을 내용을 입력해주세요.")
            return
        }

        findNext()
    }

    func findNext() {
        guard !lastSearchPattern.isEmpty else {
            showFind()
            return
        }

        do {
            let expression = try currentSearchExpression()
            let selection = textView.selectedRange()
            var start = NSMaxRange(selection)

            if selection.length == 0, selection == lastFoundRange {
                start += 1
            }

            guard let match = MemoTextLogic.nextMatch(
                in: textView.string,
                expression: expression,
                startingAt: start
            ) else {
                showMessage("검색 결과 없음", detail: "찾는 내용이 없습니다.")
                return
            }

            lastFoundRange = match.range
            textView.setSelectedRange(match.range)
            textView.scrollRangeToVisible(match.range)
            window?.makeFirstResponder(textView)
        } catch {
            showMessage("정규식 오류", detail: error.localizedDescription)
        }
    }

    func showReplace() {
        let alert = NSAlert()
        alert.messageText = "바꾸기"
        alert.informativeText = "정규식에서는 $1 형식으로 그룹을 사용할 수 있습니다."

        let accessoryView = NSView(frame: NSRect(x: 0, y: 0, width: 340, height: 100))
        let searchField = NSSearchField(frame: NSRect(x: 0, y: 70, width: 340, height: 24))
        searchField.placeholderString = "찾을 내용"
        searchField.stringValue = lastSearchPattern

        let replacementField = NSTextField(frame: NSRect(x: 0, y: 38, width: 340, height: 24))
        replacementField.placeholderString = "바꿀 내용"

        let regularExpressionButton = NSButton(
            checkboxWithTitle: "정규식",
            target: nil,
            action: nil
        )
        regularExpressionButton.frame = NSRect(x: 0, y: 4, width: 90, height: 24)
        regularExpressionButton.state = lastSearchUsesRegularExpression ? .on : .off

        accessoryView.addSubview(searchField)
        accessoryView.addSubview(replacementField)
        accessoryView.addSubview(regularExpressionButton)
        alert.accessoryView = accessoryView
        alert.addButton(withTitle: "하나 바꾸기")
        alert.addButton(withTitle: "모두 바꾸기")
        alert.addButton(withTitle: "취소")
        alert.window.initialFirstResponder = searchField

        let response = alert.runModal()
        guard response == .alertFirstButtonReturn || response == .alertSecondButtonReturn else {
            window?.makeFirstResponder(textView)
            return
        }

        updateSearchOptions(
            pattern: searchField.stringValue,
            usesRegularExpression: regularExpressionButton.state == .on
        )

        guard !lastSearchPattern.isEmpty else {
            showMessage("바꾸기", detail: "찾을 내용을 입력해주세요.")
            return
        }

        do {
            let expression = try currentSearchExpression()
            if response == .alertFirstButtonReturn {
                replaceOne(with: replacementField.stringValue, expression: expression)
            } else {
                replaceAll(with: replacementField.stringValue, expression: expression)
            }
        } catch {
            showMessage("정규식 오류", detail: error.localizedDescription)
        }
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

        requestCloseTab(id: id)
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
        requestCloseTab(id: id)
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

        updateCurrentTabTitle()
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

    private func requestCloseTab(id: String) {
        guard confirmCloseTab(id: id) else {
            window?.makeFirstResponder(textView)
            return
        }

        closeTab(id: id)
    }

    private func confirmCloseTab(id: String) -> Bool {
        guard let tab = tabs.first(where: { $0.id == id }) else {
            return false
        }

        let alert = NSAlert()
        alert.messageText = "탭을 닫을까요?"
        alert.informativeText = "\"\(tab.title)\" 탭이 삭제됩니다."
        alert.alertStyle = .warning
        alert.addButton(withTitle: "닫기")
        alert.addButton(withTitle: "취소")

        return alert.runModal() == .alertFirstButtonReturn
    }

    private func closeTab(id: String) {
        guard let closingIndex = tabs.firstIndex(where: { $0.id == id }) else {
            return
        }

        if id == selectedTabID {
            autosaveWorkItem?.cancel()
        }

        let removedTab = tabs.remove(at: closingIndex)
        delete(tab: removedTab)

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
        let newTitle = normalizedTitle(titleTextField.stringValue, fallback: fallbackTitle)

        guard tabs[index].title != newTitle || !tabs[index].hasSeparateTitle else {
            return
        }

        tabs[index].title = newTitle
        tabs[index].hasSeparateTitle = true
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

    private func normalizedTitle(_ title: String, fallback: String) -> String {
        let firstLine = title.split(maxSplits: 1, whereSeparator: \.isNewline).first.map(String.init) ?? ""
        let trimmedTitle = firstLine.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmedTitle.isEmpty ? fallback : trimmedTitle
    }

    @discardableResult
    private func migrateLegacyTitle(for tab: MemoTab) -> Bool {
        guard let index = tabs.firstIndex(where: { $0.id == tab.id }) else {
            return false
        }

        let migratedTitle = removeLegacyTitleFromBody() ?? tab.title
        tabs[index].title = normalizedTitle(migratedTitle, fallback: tab.title)
        tabs[index].hasSeparateTitle = true
        return true
    }

    private func markCurrentTabAsUsingSeparateTitle() {
        guard let selectedTabID, let index = tabs.firstIndex(where: { $0.id == selectedTabID }) else {
            return
        }

        tabs[index].hasSeparateTitle = true
    }

    private func removeLegacyTitleFromBody() -> String? {
        guard let textStorage = textView.textStorage else {
            return nil
        }

        let string = textStorage.string as NSString
        var location = 0

        while location < string.length {
            let lineRange = string.lineRange(for: NSRange(location: location, length: 0))
            let title = string.substring(with: lineRange).trimmingCharacters(in: .whitespacesAndNewlines)

            if !title.isEmpty {
                textStorage.replaceCharacters(in: NSRange(location: 0, length: NSMaxRange(lineRange)), with: "")
                return title
            }

            location = NSMaxRange(lineRange)
        }

        return nil
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

        textView.layoutManager?.invalidateLayout(forCharacterRange: fullRange, actualCharacterRange: nil)
        textView.layoutManager?.invalidateDisplay(forCharacterRange: fullRange)
        textView.setNeedsDisplay(textView.visibleRect)
        scrollView.contentView.needsDisplay = true

        var typingAttributes = textView.typingAttributes
        typingAttributes[.font] = font
        typingAttributes[.foregroundColor] = textColor
        textView.typingAttributes = typingAttributes

        lineNumberRuler?.fontSize = clampedSize
        lineNumberRuler?.rebuildLineStarts()
        lineNumberRuler?.needsDisplay = true
        normalizeCurrentTypingAttributes()
        scheduleAutosave()
    }

    private func updateSearchOptions(pattern: String, usesRegularExpression: Bool) {
        lastSearchPattern = pattern
        lastSearchUsesRegularExpression = usesRegularExpression
        lastFoundRange = nil
    }

    private func currentSearchExpression() throws -> NSRegularExpression {
        try MemoTextLogic.searchExpression(
            pattern: lastSearchPattern,
            usesRegularExpression: lastSearchUsesRegularExpression
        )
    }

    private func replaceOne(with replacement: String, expression: NSRegularExpression) {
        let source = textView.string
        let selection = textView.selectedRange()
        let selectedMatch = expression.firstMatch(in: source, range: selection)
        let match = selectedMatch?.range == selection
            ? selectedMatch
            : MemoTextLogic.nextMatch(in: source, expression: expression, startingAt: NSMaxRange(selection))

        guard let match else {
            showMessage("바꾸기", detail: "찾는 내용이 없습니다.")
            return
        }

        let template = replacementTemplate(replacement)
        let resolvedReplacement = expression.replacementString(
            for: match,
            in: source,
            offset: 0,
            template: template
        )
        let replacementRange = NSRange(location: match.range.location, length: (resolvedReplacement as NSString).length)

        textView.insertText(resolvedReplacement, replacementRange: match.range)
        textView.setSelectedRange(replacementRange)
        lastFoundRange = nil
        findNext()
    }

    private func replaceAll(with replacement: String, expression: NSRegularExpression) {
        let source = textView.string
        let fullRange = NSRange(location: 0, length: (source as NSString).length)
        let matches = expression.matches(in: source, range: fullRange)

        guard !matches.isEmpty else {
            showMessage("바꾸기", detail: "찾는 내용이 없습니다.")
            return
        }

        let template = replacementTemplate(replacement)
        textView.undoManager?.beginUndoGrouping()
        for match in matches.reversed() {
            let resolvedReplacement = expression.replacementString(
                for: match,
                in: source,
                offset: 0,
                template: template
            )
            textView.insertText(resolvedReplacement, replacementRange: match.range)
        }
        textView.undoManager?.endUndoGrouping()

        lastFoundRange = nil
        window?.makeFirstResponder(textView)
        showMessage("바꾸기 완료", detail: "\(matches.count)개를 바꿨습니다.")
    }

    private func replacementTemplate(_ replacement: String) -> String {
        lastSearchUsesRegularExpression ? replacement : NSRegularExpression.escapedTemplate(for: replacement)
    }

    private func showMessage(_ title: String, detail: String) {
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = detail
        alert.addButton(withTitle: "확인")
        alert.runModal()
        window?.makeFirstResponder(textView)
    }

    private func applyLineWrapSetting() {
        let contentSize = scrollView.contentSize

        scrollView.hasHorizontalScroller = !lineWrapEnabled
        textView.isHorizontallyResizable = !lineWrapEnabled
        textView.autoresizingMask = lineWrapEnabled ? [.width] : [.height]
        textView.minSize = NSSize(width: 0, height: contentSize.height)
        textView.maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)

        if lineWrapEnabled {
            textView.frame.size.width = contentSize.width
            textView.textContainer?.containerSize = NSSize(width: contentSize.width, height: CGFloat.greatestFiniteMagnitude)
            textView.textContainer?.widthTracksTextView = true
        } else {
            textView.textContainer?.containerSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
            textView.textContainer?.widthTracksTextView = false
        }

        textView.layoutManager?.invalidateLayout(
            forCharacterRange: NSRange(location: 0, length: (textView.string as NSString).length),
            actualCharacterRange: nil
        )
        textView.needsDisplay = true
    }

    private func applyParagraphStylesToCurrentText() {
        guard let textStorage = textView.textStorage else {
            return
        }

        let fullRange = NSRange(location: 0, length: textStorage.length)
        guard fullRange.length > 0 else {
            textView.typingAttributes = defaultTypingAttributes(alignment: .left)
            return
        }

        let fullString = textStorage.string as NSString
        textStorage.beginEditing()
        let source = NSAttributedString(attributedString: textStorage)

        for paragraphRange in paragraphRanges(in: fullString, length: textStorage.length) {
            let alignment = paragraphAlignment(in: source, at: min(paragraphRange.location, max(0, source.length - 1)))
            textStorage.addAttribute(.paragraphStyle, value: paragraphStyle(alignment: alignment), range: paragraphRange)
        }
        textStorage.endEditing()

        normalizeCurrentTypingAttributes()
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

    private func delete(tab: MemoTab) {
        saveQueue.async { [storage, tab] in
            storage.delete(tab: tab)
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

    private func normalizeTextAttributesInCurrentDocument() {
        guard !isNormalizingTextAttributes, !textView.hasMarkedText(), let textStorage = textView.textStorage else {
            normalizeCurrentTypingAttributes()
            return
        }

        guard textStorage.length > 0 else {
            textView.typingAttributes = defaultTypingAttributes(alignment: .left)
            return
        }

        isNormalizingTextAttributes = true
        let undoManager = textView.undoManager
        let shouldRestoreUndo = undoManager?.isUndoRegistrationEnabled == true

        if shouldRestoreUndo {
            undoManager?.disableUndoRegistration()
        }

        textStorage.beginEditing()
        let source = NSAttributedString(attributedString: textStorage)
        applyDefaultAttributes(to: textStorage, preservingAlignmentFrom: source)

        textStorage.endEditing()

        if shouldRestoreUndo {
            undoManager?.enableUndoRegistration()
        }

        isNormalizingTextAttributes = false
        normalizeCurrentTypingAttributes()
    }

    private func normalizeCurrentTypingAttributes() {
        guard !isNormalizingTypingAttributes else {
            return
        }

        isNormalizingTypingAttributes = true
        textView.typingAttributes = normalizedTypingAttributes(from: textView.typingAttributes)
        isNormalizingTypingAttributes = false
    }

    private func normalizedTypingAttributes(from attributes: [NSAttributedString.Key: Any]) -> [NSAttributedString.Key: Any] {
        let alignment = (attributes[.paragraphStyle] as? NSParagraphStyle)?.alignment ?? currentParagraphAlignment()
        return defaultTypingAttributes(alignment: alignment)
    }

    private func currentParagraphAlignment() -> NSTextAlignment {
        guard let textStorage = textView.textStorage, textStorage.length > 0 else {
            return .left
        }

        let selectedRange = textView.selectedRange()
        let safeLocation = min(max(0, selectedRange.location), textStorage.length - 1)
        return paragraphAlignment(in: textStorage, at: safeLocation)
    }

    private func applyDefaultAttributes(to textStorage: NSMutableAttributedString, preservingAlignmentFrom source: NSAttributedString) {
        let fullRange = NSRange(location: 0, length: textStorage.length)
        guard fullRange.length > 0 else {
            return
        }

        textStorage.setAttributes(defaultTypingAttributes(alignment: .left), range: fullRange)

        let fullString = textStorage.string as NSString
        for paragraphRange in paragraphRanges(in: fullString, length: textStorage.length) {
            let alignment = paragraphAlignment(in: source, at: min(paragraphRange.location, max(0, source.length - 1)))
            textStorage.setAttributes(defaultTypingAttributes(alignment: alignment), range: paragraphRange)
        }
    }

    private func paragraphRanges(in string: NSString, length: Int) -> [NSRange] {
        guard length > 0 else {
            return []
        }

        let fullRange = NSRange(location: 0, length: length)
        var ranges: [NSRange] = []
        var location = 0

        while location < length {
            let paragraphRange = string.paragraphRange(for: NSRange(location: location, length: 0))
            let safeRange = NSIntersectionRange(paragraphRange, fullRange)

            if safeRange.length > 0 {
                ranges.append(safeRange)
            }

            let nextLocation = max(NSMaxRange(paragraphRange), location + 1)
            location = min(nextLocation, length)
        }

        return ranges
    }

    private func defaultTypingAttributes(alignment: NSTextAlignment) -> [NSAttributedString.Key: Any] {
        return [
            .font: editorFont(size: fontSize),
            .foregroundColor: textColor,
            .paragraphStyle: paragraphStyle(alignment: alignment)
        ]
    }

    private func paragraphStyle(alignment: NSTextAlignment) -> NSParagraphStyle {
        let style = NSMutableParagraphStyle()
        style.alignment = alignment
        style.lineBreakMode = lineWrapEnabled ? .byWordWrapping : .byClipping
        return style
    }

    @objc private func scrollBoundsDidChange(_ notification: Notification) {
        lineNumberRuler?.needsDisplay = true
    }
}
