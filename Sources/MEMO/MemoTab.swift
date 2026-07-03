import Foundation

struct MemoTab: Codable, Equatable {
    let id: String
    var title: String

    static func create(index: Int) -> MemoTab {
        MemoTab(id: UUID().uuidString, title: "메모 \(index)")
    }
}

struct MemoSession: Codable {
    var tabs: [MemoTab]
    var selectedTabID: String?
}
