# MEMO

맥과 윈도우에서 바로 켜서 쓰는 아주 단순한 메모 앱

CotEditor보다 더 가볍게, 메모만 빠르게 적으려고 제작했습니다.
현재 0.1.2v

## 실행 방법

macOS:

```bash
./build.sh
open build/MEMO.app
```

Windows 빌드:

```bash
./build_windows.sh
```

빌드 결과는 `build/windows/MEMO-Windows.exe`에 생성됩니다.

## 완료

- macOS 네이티브 AppKit 앱
- Windows WinForms 버전
- 다크모드 고정
- 왼쪽 줄 번호
- 탭 메모
- 탭별 자동 저장
- 내부 저장 폴더 사용
- 직접 만든 단순 앱 아이콘
- Command + 마우스 휠 0.25pt 단위 글자 크기 조절
- 메뉴에서 왼쪽, 가운데, 오른쪽 정렬
- 탭 위쪽 UI에서 x로 탭 닫기
- 탭이 2개 이상일 때만 탭 UI 표시
- 긴 줄이 화면에서 접혀도 줄번호는 증가하지 않음
- 웹/트위터 복사 붙여넣기 시 서식 제거

## 단축키

- `Command/Ctrl + D`: 새 탭
- `Command/Ctrl + W`: 현재 탭 닫기
- `Command/Ctrl + [`: 이전 탭
- `Command/Ctrl + ]`: 다음 탭
- `Command/Ctrl + S`: 지금 저장
- `Command/Ctrl + L`: 왼쪽 정렬
- `Command/Ctrl + E`: 가운데 정렬
- `Command/Ctrl + R`: 오른쪽 정렬
- `Command/Ctrl + +`: 글자 크게
- `Command/Ctrl + -`: 글자 작게
- `Command/Ctrl + 0`: 글자 크기 초기화

## 개발중

- 실제 사용하면서 저장 안정성 확인
- 탭이 많아졌을 때 위쪽 UI 표시 확인

## 고민중

- 탭 이름 수동 변경 기능 추가 여부

## 사용 기술

- Swift
- AppKit
- C#
- WinForms

## 참고

- macOS 메모는 `~/Library/Application Support/MEMO` 안에 자동 저장됩니다.
- Windows 메모는 `%APPDATA%\MEMO` 안에 자동 저장됩니다.
- macOS 탭 목록은 `tabs.json`, Windows 탭 목록은 `tabs.tsv`에 저장됩니다.
- 각 탭은 정렬 저장용 `.rtf`와 일반 텍스트 백업용 `.txt`를 같이 만듭니다.
- 네트워크 접근이나 외부 패키지는 사용하지 않습니다.
