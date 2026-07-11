import AppKit

final class MemoStorage {
    private let fileManager = FileManager.default
    private let directoryURL: URL
    private let tabsDirectoryURL: URL
    private let manifestURL: URL
    private let legacyRTFURL: URL
    private let legacyPlainTextURL: URL

    init() {
        let supportURL = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
        let baseURL = supportURL ?? fileManager.homeDirectoryForCurrentUser

        directoryURL = baseURL.appendingPathComponent("MEMO", isDirectory: true)
        tabsDirectoryURL = directoryURL.appendingPathComponent("tabs", isDirectory: true)
        manifestURL = directoryURL.appendingPathComponent("tabs.json", isDirectory: false)
        legacyRTFURL = directoryURL.appendingPathComponent("memo.rtf", isDirectory: false)
        legacyPlainTextURL = directoryURL.appendingPathComponent("memo.txt", isDirectory: false)
    }

    func loadSession() -> MemoSession {
        if let data = try? Data(contentsOf: manifestURL),
           let session = try? JSONDecoder().decode(MemoSession.self, from: data) {
            let tabs = normalizedTabs(session.tabs)

            if !tabs.isEmpty {
                let selectedID = tabs.contains(where: { $0.id == session.selectedTabID }) ? session.selectedTabID : tabs.first?.id
                return MemoSession(tabs: tabs, selectedTabID: selectedID)
            }
        }

        return MemoSession(tabs: [MemoTab.create(index: 1)], selectedTabID: nil)
    }

    func saveSession(_ session: MemoSession) throws {
        try fileManager.createDirectory(at: directoryURL, withIntermediateDirectories: true)

        let data = try JSONEncoder().encode(session)
        try data.write(to: manifestURL, options: [.atomic])
    }

    func load(tab: MemoTab) -> NSAttributedString? {
        let rtfURL = tabRTFURL(for: tab.id)
        let plainTextURL = tabPlainTextURL(for: tab.id)

        if let rtfData = try? Data(contentsOf: rtfURL),
           let attributedText = NSAttributedString(rtf: rtfData, documentAttributes: nil) {
            return attributedText
        }

        guard let plainText = try? String(contentsOf: plainTextURL, encoding: .utf8) else {
            return loadLegacyMemoIfNeeded()
        }

        return NSAttributedString(string: plainText)
    }

    func save(_ attributedText: NSAttributedString, for tab: MemoTab) throws {
        try fileManager.createDirectory(at: tabsDirectoryURL, withIntermediateDirectories: true)

        let fullRange = NSRange(location: 0, length: attributedText.length)
        let rtfData = try attributedText.data(
            from: fullRange,
            documentAttributes: [.documentType: NSAttributedString.DocumentType.rtf]
        )

        try rtfData.write(to: tabRTFURL(for: tab.id), options: [.atomic])
        try attributedText.string.write(to: tabPlainTextURL(for: tab.id), atomically: true, encoding: .utf8)
    }

    func delete(tab: MemoTab) {
        try? fileManager.removeItem(at: tabRTFURL(for: tab.id))
        try? fileManager.removeItem(at: tabPlainTextURL(for: tab.id))
    }

    private func loadLegacyMemoIfNeeded() -> NSAttributedString? {
        if hasStoredTabFiles() {
            return nil
        }

        if let rtfData = try? Data(contentsOf: legacyRTFURL),
           let attributedText = NSAttributedString(rtf: rtfData, documentAttributes: nil) {
            return attributedText
        }

        guard let plainText = try? String(contentsOf: legacyPlainTextURL, encoding: .utf8) else {
            return nil
        }

        return NSAttributedString(string: plainText)
    }

    private func hasStoredTabFiles() -> Bool {
        guard let files = try? fileManager.contentsOfDirectory(at: tabsDirectoryURL, includingPropertiesForKeys: nil) else {
            return false
        }

        return files.contains { file in
            let pathExtension = file.pathExtension.lowercased()
            return pathExtension == "rtf" || pathExtension == "txt"
        }
    }

    private func normalizedTabs(_ tabs: [MemoTab]) -> [MemoTab] {
        var seen = Set<String>()

        return tabs.compactMap { tab in
            let id = normalizedID(tab.id)
            guard !id.isEmpty, !seen.contains(id) else {
                return nil
            }

            seen.insert(id)
            let title = tab.title.trimmingCharacters(in: .whitespacesAndNewlines)
            return MemoTab(
                id: id,
                title: title.isEmpty ? "메모 \(seen.count)" : title,
                hasSeparateTitle: tab.hasSeparateTitle
            )
        }
    }

    private func normalizedID(_ id: String) -> String {
        let allowed = CharacterSet.alphanumerics.union(CharacterSet(charactersIn: "-_"))
        let scalars = id.unicodeScalars.filter { allowed.contains($0) }
        return String(String.UnicodeScalarView(scalars))
    }

    private func tabRTFURL(for id: String) -> URL {
        tabsDirectoryURL.appendingPathComponent("\(normalizedID(id)).rtf", isDirectory: false)
    }

    private func tabPlainTextURL(for id: String) -> URL {
        tabsDirectoryURL.appendingPathComponent("\(normalizedID(id)).txt", isDirectory: false)
    }
}
