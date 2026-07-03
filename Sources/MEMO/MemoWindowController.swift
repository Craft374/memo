import AppKit

final class MemoWindowController: NSWindowController, NSWindowDelegate {
    private let editorView = MemoEditorView()

    init() {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 900, height: 680),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )

        window.title = "MEMO"
        window.minSize = NSSize(width: 420, height: 280)
        window.center()
        window.contentView = editorView
        window.appearance = NSAppearance(named: .darkAqua)
        window.isReleasedWhenClosed = false

        super.init(window: window)
        window.delegate = self
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        nil
    }

    override func windowDidLoad() {
        super.windowDidLoad()
        window?.makeFirstResponder(editorView.textView)
    }

    func windowWillClose(_ notification: Notification) {
        saveImmediately()
    }

    func saveNow(showError: Bool = false) {
        editorView.saveNow(showError: showError)
    }

    func saveImmediately() {
        editorView.saveImmediately()
    }

    func changeFontSize(by delta: CGFloat) {
        editorView.changeFontSize(by: delta)
    }

    func resetFontSize() {
        editorView.resetFontSize()
    }

    func setAlignment(_ alignment: NSTextAlignment) {
        editorView.setAlignment(alignment)
    }

    func createNewTab() {
        editorView.createNewTab()
    }

    func closeCurrentTab() {
        editorView.closeCurrentTab()
    }

    func selectNextTab() {
        editorView.selectNextTab()
    }

    func selectPreviousTab() {
        editorView.selectPreviousTab()
    }
}
