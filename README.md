# MEMO

맥에서 바로 켜서 쓰는 아주 단순한 메모 앱

CotEditor보다 더 가볍게, 메모만 빠르게 적으려고 제작했습니다.
현재 0.1.4v

윈도우 버전(C# WinForms)은 [`windows` 브랜치](https://github.com/Craft374/memo/tree/windows)에 있습니다.

## 빌드 & 실행

```bash
./build.sh
open build/MEMO.app
```

## 기능

- macOS 네이티브 AppKit 앱
- 다크모드 고정
- 왼쪽 줄 번호
- 탭 메모
- 탭별 자동 저장
- 내부 저장 폴더 사용
- 직접 만든 단순 앱 아이콘
- Command + 마우스 휠 0.25pt 단위 글자 크기 조절, 최대 300pt 확대
- 메뉴에서 왼쪽, 가운데, 오른쪽 정렬
- 탭 위쪽 UI에서 x로 탭 닫기
- 탭 닫기 전 확인창 표시
- 탭이 2개 이상일 때만 탭 UI 표시
- 긴 줄이 화면에서 접혀도 줄번호는 증가하지 않음
- 웹/트위터 복사 붙여넣기 시 서식 제거
- 다른 앱으로 복사할 때 일반 텍스트로 복사
- 일반 검색과 정규식 검색·바꾸기
- `1.`처럼 시작한 번호 목록 자동 이어쓰기
- 메뉴에서 자동 줄바꿈 켜기/끄기

## 단축키

- `Command + D`: 새 탭
- `Command + W`: 현재 탭 닫기
- `Command + [` / `Command + ]`: 이전/다음 탭
- `Control + Tab` / `Shift + Control + Tab`: 다음/이전 탭
- `Command + S`: 지금 저장
- `Command + F`: 검색
- `Command + G`: 다음 찾기
- `Command + Option + F`: 바꾸기
- `Command + +` / `Command + -` / `Command + 0`: 글자 크게/작게/초기화
- `Command + Option + W`: 자동 줄바꿈 켜기/끄기

## 개발중

- 실제 사용하면서 저장 안정성 확인
- 탭이 많아졌을 때 위쪽 UI 표시 확인

## 고민중

- 탭 이름 수동 변경 기능 추가 여부

## 사용 기술

- Swift / AppKit
- 외부 패키지·네트워크 접근 없음

## 참고

- 메모는 `~/Library/Application Support/MEMO` 안에 자동 저장됩니다.
- 탭 목록은 `tabs.json`, 탭 내용은 `tabs` 폴더 안에 저장됩니다.
- 각 탭은 정렬 저장용 `.rtf`와 일반 텍스트 백업용 `.txt`를 같이 만듭니다.
