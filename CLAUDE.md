# CLAUDE.md

## 프로젝트 개요

바로 켜서 쓰는 초경량 메모 앱. 원래 맥(AppKit/Swift)용으로 만들어졌고,
이 브랜치(`windows`)는 C# WinForms(.NET 8)로 포팅된 윈도우 버전임.
맥 버전은 `mac` 브랜치에 있음.

## 빌드/실행

```powershell
.\build.ps1 -Run
```

- .NET SDK는 `dotnet`(PATH) 또는 `%USERPROFILE%\.dotnet`에서 찾고, 없으면 자동 설치함
- 결과물: `build\MEMO.exe`

## 구조

- `MEMO.csproj` — net8.0-windows, WinForms
- `Sources/MEMO/MainForm.cs` — 창/메뉴/탭/저장 로직 전부 (맥 버전의 MemoEditorView + WindowController + AppDelegate 역할)
- `Sources/MEMO/MemoRichTextBox.cs` — 에디터. 붙여넣기 서식 제거, Ctrl+휠 줌, EM_SETTARGETDEVICE로 줄바꿈 토글(핸들 재생성 없음), EM_SETZOOM으로 글자 크기(문서는 항상 11.25pt 유지)
- `Sources/MEMO/LineNumberGutter.cs` — 줄번호 (논리 줄 기준, 줄바꿈 접힘 무시)
- `Sources/MEMO/TabBarControl.cs` — 커스텀 탭바 (2개 이상일 때만 표시, 높이 애니메이션)
- `Sources/MEMO/MemoStorage.cs` — `%APPDATA%\MEMO`에 tabs.json + tabs/*.rtf/*.txt (맥 버전과 동일 스키마)

## 주의점

- 다크 테마 고정이 컨셉. 색상 값은 `Theme.cs`에 맥 버전과 동일하게 환산되어 있음
- 글자 크기는 폰트를 바꾸지 않고 EM_SETZOOM 배율로 처리함 (fontSize 11.25pt = 배율 1.0)
- 단일 인스턴스: 뮤텍스 + 브로드캐스트 메시지로 기존 창 활성화
