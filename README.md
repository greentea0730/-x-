# LumiBot

Eternal Return 전적 검색, Discord 알림, 익명 게시판, YouTube 공략 검색을 제공하는 Discord 봇입니다.

## 주요 기능

### 전적 검색

- `!전적 닉네임`으로 Eternal Return 유저의 전적 조회
- `!등록 게임닉네임`으로 닉네임을 등록한 뒤 `!전적`만 입력해 빠르게 조회
- 현재 시즌 티어, MMR, 순위, 승률, Top 3 비율, 모스트 캐릭터 표시
- 최근 7일 RP 변화 그래프 제공
- 최근 20게임 순위 요약 제공
- 최근 5게임의 캐릭터, 순위, 경기 시간, TK/K/A, DMG/VIS/H 상세 정보 제공
- `!루미 설정`으로 최근 5게임 상세 Embed 출력 여부 설정

전적 조회 결과는 하나의 게임 기록 목록을 재사용해 그래프, 순위, 상세 정보를 구성합니다. 랭크 기록이 없거나 API 오류가 발생한 경우에도 안내 메시지를 표시합니다.

### Discord 명령어

| 명령어 | 설명 |
| --- | --- |
| `!도움말` 또는 `!루미 도움말` | 사용 가능한 명령어 안내 |
| `!전적 닉네임` | 지정한 유저의 전적 조회 |
| `!전적` | 등록된 닉네임의 전적 조회 |
| `!등록 게임닉네임` | 내 게임 닉네임 등록 |
| `!루미 설정` | 최근 5게임 상세 출력 설정 |
| `!공략 검색어` | YouTube 공략 영상 검색 |
| `!루미 공지` | Eternal Return 공식 소식 링크 표시 |
| `!루미 안녕` | 루미의 인사 메시지 |
| `!루미 주사위` | 주사위 결과 출력 |
| `!루미 정보` | 봇 버전 및 라이브러리 정보 표시 |

### 자동 알림 및 익명 게시판

- Eternal Return 접속 시 지정된 Discord 채널에 알림
- Eternal Return 공식 공지사항을 주기적으로 확인해 새 소식 전달
- 봇 DM으로 `!익명 메시지내용`을 보내면 지정된 익명 게시판에 전달
- 익명 게시판 메시지는 별도 로그 채널에도 기록

## 다운로드

- [소스 코드 다운로드 (ZIP)](https://github.com/boseong0807/-x-/archive/refs/heads/main.zip)
- GitHub 페이지의 **Code → Download ZIP**에서도 다운로드할 수 있습니다.

## 실행 전 설정

공개 저장소에 비밀정보가 올라가지 않도록 다음 값은 플레이스홀더로 비워 두었습니다.

- `MainProgram.cs`
  - `YOUR_DISCORD_BOT_TOKEN`
  - `YOUR_YOUTUBE_API_KEY`
  - `YOUR_YOUTUBE_PLAYLIST_ID`
  - Discord 서버 및 채널 ID
- `Services/ERApiServices.cs`
  - `YOUR_ER_API_KEY`


## 요구 사항

- .NET 10 SDK
- Discord 봇 및 필요한 API 키

## 실행

```bash
dotnet restore
dotnet run
```
