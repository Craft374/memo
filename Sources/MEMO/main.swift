import AppKit

let app = NSApplication.shared
let delegate = AppDelegate()

app.setActivationPolicy(.regular)
app.appearance = NSAppearance(named: .darkAqua)
app.delegate = delegate
app.run()
