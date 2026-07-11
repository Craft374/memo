import Foundation

struct MemoTab: Codable, Equatable {
    let id: String
    var title: String
    var hasSeparateTitle: Bool

    private enum CodingKeys: String, CodingKey {
        case id
        case title
        case hasSeparateTitle
    }

    init(id: String, title: String, hasSeparateTitle: Bool = true) {
        self.id = id
        self.title = title
        self.hasSeparateTitle = hasSeparateTitle
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        title = try container.decode(String.self, forKey: .title)
        hasSeparateTitle = try container.decodeIfPresent(Bool.self, forKey: .hasSeparateTitle) ?? false
    }

    static func create(index: Int) -> MemoTab {
        MemoTab(id: UUID().uuidString, title: "메모 \(index)")
    }
}

struct MemoSession: Codable {
    var tabs: [MemoTab]
    var selectedTabID: String?
}
