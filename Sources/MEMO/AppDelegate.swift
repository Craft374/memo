import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var windowController: MemoWindowController?
    private var lineWrapMenuItem: NSMenuItem?

    func applicationDidFinishLaunching(_ notification: Notification) {
        buildMenu()

        let controller = MemoWindowController()
        windowController = controller
        controller.showWindow(nil)
        updateLineWrapMenuItem()

        NSApp.activate(ignoringOtherApps: true)
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }

    func applicationWillTerminate(_ notification: Notification) {
        windowController?.saveImmediately()
    }

    private func buildMenu() {
        let mainMenu = NSMenu()
        NSApp.mainMenu = mainMenu

        let appMenuItem = NSMenuItem(title: "MEMO", action: nil, keyEquivalent: "")
        mainMenu.addItem(appMenuItem)

        let appMenu = NSMenu(title: "MEMO")
        appMenuItem.submenu = appMenu
        appMenu.addItem(withTitle: "MEMO 종료", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")

        let fileMenuItem = NSMenuItem(title: "파일", action: nil, keyEquivalent: "")
        mainMenu.addItem(fileMenuItem)

        let fileMenu = NSMenu(title: "파일")
        fileMenuItem.submenu = fileMenu
        fileMenu.addItem(withTitle: "지금 저장", action: #selector(saveNow(_:)), keyEquivalent: "s").target = self
        fileMenu.addItem(.separator())
        let closeWindowItem = fileMenu.addItem(withTitle: "창 닫기", action: #selector(NSWindow.performClose(_:)), keyEquivalent: "w")
        closeWindowItem.keyEquivalentModifierMask = [.command, .shift]

        let editMenuItem = NSMenuItem(title: "편집", action: nil, keyEquivalent: "")
        mainMenu.addItem(editMenuItem)

        let editMenu = NSMenu(title: "편집")
        editMenuItem.submenu = editMenu
        editMenu.addItem(withTitle: "실행 취소", action: Selector(("undo:")), keyEquivalent: "z")

        let redoItem = editMenu.addItem(withTitle: "다시 실행", action: Selector(("redo:")), keyEquivalent: "z")
        redoItem.keyEquivalentModifierMask = [.command, .shift]

        editMenu.addItem(.separator())
        editMenu.addItem(withTitle: "오려두기", action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        editMenu.addItem(withTitle: "복사", action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        editMenu.addItem(withTitle: "붙여넣기", action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        editMenu.addItem(withTitle: "전체 선택", action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        editMenu.addItem(.separator())
        editMenu.addItem(withTitle: "검색…", action: #selector(showFind(_:)), keyEquivalent: "f").target = self
        editMenu.addItem(withTitle: "다음 찾기", action: #selector(findNext(_:)), keyEquivalent: "g").target = self
        let replaceItem = editMenu.addItem(withTitle: "바꾸기…", action: #selector(showReplace(_:)), keyEquivalent: "f")
        replaceItem.target = self
        replaceItem.keyEquivalentModifierMask = [.command, .option]
        editMenu.addItem(.separator())
        editMenu.addItem(withTitle: "왼쪽 정렬", action: #selector(alignLeft(_:)), keyEquivalent: "").target = self
        editMenu.addItem(withTitle: "가운데 정렬", action: #selector(alignCenter(_:)), keyEquivalent: "").target = self
        editMenu.addItem(withTitle: "오른쪽 정렬", action: #selector(alignRight(_:)), keyEquivalent: "").target = self

        let viewMenuItem = NSMenuItem(title: "보기", action: nil, keyEquivalent: "")
        mainMenu.addItem(viewMenuItem)

        let viewMenu = NSMenu(title: "보기")
        viewMenuItem.submenu = viewMenu
        let lineWrapItem = viewMenu.addItem(withTitle: "자동 줄바꿈", action: #selector(toggleLineWrap(_:)), keyEquivalent: "w")
        lineWrapItem.target = self
        lineWrapItem.keyEquivalentModifierMask = [.command, .option]
        lineWrapMenuItem = lineWrapItem
        viewMenu.addItem(.separator())
        viewMenu.addItem(withTitle: "글자 크게", action: #selector(zoomIn(_:)), keyEquivalent: "+").target = self
        viewMenu.addItem(withTitle: "글자 작게", action: #selector(zoomOut(_:)), keyEquivalent: "-").target = self
        viewMenu.addItem(withTitle: "글자 크기 초기화", action: #selector(resetZoom(_:)), keyEquivalent: "0").target = self

        let tabMenuItem = NSMenuItem(title: "탭", action: nil, keyEquivalent: "")
        mainMenu.addItem(tabMenuItem)

        let tabMenu = NSMenu(title: "탭")
        tabMenuItem.submenu = tabMenu
        tabMenu.addItem(withTitle: "새 탭", action: #selector(newTab(_:)), keyEquivalent: "d").target = self
        tabMenu.addItem(withTitle: "현재 탭 닫기", action: #selector(closeCurrentTab(_:)), keyEquivalent: "w").target = self

        let previousTabItem = tabMenu.addItem(withTitle: "이전 탭", action: #selector(previousTab(_:)), keyEquivalent: "[")
        previousTabItem.target = self

        let nextTabItem = tabMenu.addItem(withTitle: "다음 탭", action: #selector(nextTab(_:)), keyEquivalent: "]")
        nextTabItem.target = self

        let previousTabControlItem = tabMenu.addItem(withTitle: "이전 탭", action: #selector(previousTab(_:)), keyEquivalent: "\t")
        previousTabControlItem.target = self
        previousTabControlItem.keyEquivalentModifierMask = [.control, .shift]

        let nextTabControlItem = tabMenu.addItem(withTitle: "다음 탭", action: #selector(nextTab(_:)), keyEquivalent: "\t")
        nextTabControlItem.target = self
        nextTabControlItem.keyEquivalentModifierMask = [.control]

        let windowMenuItem = NSMenuItem(title: "창", action: nil, keyEquivalent: "")
        mainMenu.addItem(windowMenuItem)

        let windowMenu = NSMenu(title: "창")
        windowMenuItem.submenu = windowMenu
        windowMenu.addItem(withTitle: "최소화", action: #selector(NSWindow.miniaturize(_:)), keyEquivalent: "m")
        windowMenu.addItem(withTitle: "확대/축소", action: #selector(NSWindow.performZoom(_:)), keyEquivalent: "f").keyEquivalentModifierMask = [.command, .control]
        NSApp.windowsMenu = windowMenu

        let helpMenuItem = NSMenuItem(title: "도움말", action: nil, keyEquivalent: "")
        mainMenu.addItem(helpMenuItem)

        let helpMenu = NSMenu(title: "도움말")
        helpMenuItem.submenu = helpMenu
        NSApp.helpMenu = helpMenu
    }

    @objc private func saveNow(_ sender: Any?) {
        windowController?.saveNow(showError: true)
    }

    @objc private func zoomIn(_ sender: Any?) {
        windowController?.changeFontSize(by: 1)
    }

    @objc private func zoomOut(_ sender: Any?) {
        windowController?.changeFontSize(by: -1)
    }

    @objc private func resetZoom(_ sender: Any?) {
        windowController?.resetFontSize()
    }

    @objc private func toggleLineWrap(_ sender: Any?) {
        windowController?.toggleLineWrap()
        updateLineWrapMenuItem()
    }

    @objc private func newTab(_ sender: Any?) {
        windowController?.createNewTab()
    }

    @objc private func closeCurrentTab(_ sender: Any?) {
        windowController?.closeCurrentTab()
    }

    @objc private func nextTab(_ sender: Any?) {
        windowController?.selectNextTab()
    }

    @objc private func previousTab(_ sender: Any?) {
        windowController?.selectPreviousTab()
    }

    @objc private func alignLeft(_ sender: Any?) {
        windowController?.setAlignment(.left)
    }

    @objc private func alignCenter(_ sender: Any?) {
        windowController?.setAlignment(.center)
    }

    @objc private func alignRight(_ sender: Any?) {
        windowController?.setAlignment(.right)
    }

    @objc private func showFind(_ sender: Any?) {
        windowController?.showFind()
    }

    @objc private func findNext(_ sender: Any?) {
        windowController?.findNext()
    }

    @objc private func showReplace(_ sender: Any?) {
        windowController?.showReplace()
    }

    private func updateLineWrapMenuItem() {
        lineWrapMenuItem?.state = windowController?.isLineWrapEnabled == false ? .off : .on
    }
}
