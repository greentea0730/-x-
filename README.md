# LumiBot

Eternal Return 전적 검색, Discord 알림, 익명 게시판, YouTube 공략 검색을 제공하는 Discord 봇입니다.

## 처음 사용하는 사람을 위한 설정

실행 전에 아래 설정값을 소스 코드 상단의 `사용자 설정` 영역에 입력하세요.

| 설정값 | 파일 | 입력할 값 |
| --- | --- | --- |
| `DiscordBotToken` | `MainProgram.cs` | Discord 봇 토큰 |
| `PrimaryChannelId`, `LegacyChannelId` | `MainProgram.cs` | 접속 알림·자동 공지 채널 ID |
| `YoutubeApiKey` | `MainProgram.cs` | YouTube Data API 키 |
| `YoutubePlaylistId` | `MainProgram.cs` | YouTube 재생목록 ID |
| `GuildId` | `Services/AnonymousService.cs` | 익명 게시판을 사용할 Discord 서버 ID |
| `PostChannelId` | `Services/AnonymousService.cs` | 익명 글을 게시할 채널 ID |
| `LogChannelId` | `Services/AnonymousService.cs` | 익명 글 로그를 남길 채널 ID |
| `ApiKey` | `Services/ERApiServices.cs` | Eternal Return Open API 키 |

### Discord 봇 토큰과 채널 ID

1. [Discord Developer Portal](https://discord.com/developers/applications)에서 애플리케이션을 만들고 **Bot → Reset Token**으로 토큰을 발급합니다.
2. **Bot → Privileged Gateway Intents**에서 `Message Content Intent`, `Server Members Intent`, `Presence Intent`를 켭니다.
3. Discord 사용자 설정의 **고급 → 개발자 모드**를 켭니다.
4. 서버를 우클릭해 **서버 ID 복사**, 채널을 우클릭해 **채널 ID 복사**를 누릅니다.
5. 복사한 값을 `GuildId`, `PrimaryChannelId`, `LegacyChannelId`, `PostChannelId`, `LogChannelId`에 입력합니다.

### API 키와 YouTube 재생목록 ID

- Eternal Return Open API 키는 해당 API 제공처에서 발급받아 `Services/ERApiServices.cs`의 `ApiKey`에 입력합니다.
- YouTube API 키는 [Google Cloud Console](https://console.cloud.google.com/)에서 YouTube Data API v3를 활성화한 뒤 발급받아 `MainProgram.cs`의 `YoutubeApiKey`에 입력합니다.
- YouTube 재생목록 주소가 `https://www.youtube.com/playlist?list=ABC123`이라면 `YoutubePlaylistId`에는 `ABC123`만 입력합니다.

토큰과 API 키는 절대 GitHub에 올리거나 다른 사람에게 공유하지 마세요. 이미 공개했다면 해당 서비스에서 즉시 폐기하고 새 키를 발급하세요.

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

### 전적 기능 동작 구조

전적 조회 흐름은 다음과 같습니다.

```text
Discord 메시지
  -> 닉네임/UID 확인
  -> 시즌 통계 조회
  -> 게임 기록 페이지 조회
  -> 공통 UserGame 목록 생성
       -> 최근 7일 RP 그래프
       -> 최근 20게임 순위
       -> 최근 5게임 상세 Embed
```

지원 입력:

- `!전적 닉네임`
- `!전적` — 등록된 닉네임 사용
- 지정 채널에서 닉네임만 입력
- 지정 채널에서 `.` 또는 `!` 입력

닉네임 입력 시 Eternal Return Open API로 UID를 조회하고, 등록된 닉네임 사용 시 저장된 UID를 재사용합니다. 시즌 통계에서는 MMR, 티어, 순위, 랭크 백분위, 전체 게임 수, 승리 수, Top 3 비율, 캐릭터별 통계를 표시합니다.

게임 기록은 페이지 단위로 조회하며 다음 조건에서 종료합니다.

- 랭크 게임을 20개 이상 확보하고 7일 전 기록까지 도달한 경우
- API가 다음 페이지를 반환하지 않는 경우
- 빈 페이지 또는 반복된 API 오류가 발생한 경우
- 최대 100페이지에 도달한 경우

현재 시즌이 아닌 기록은 제외합니다. 하나의 `recentGames` 목록을 최근 7일 그래프, 최근 20게임 순위, 최근 5게임 상세 Embed에서 함께 사용해 중복 API 호출을 줄입니다.

최근 7일 RP 그래프는 랭크 게임을 날짜별로 묶고, 하루에 여러 게임이 있으면 가장 최근 게임의 RP를 사용합니다. 그래프 표시용 이상치 완화와 최소 범위 보정은 원본 데이터에 영향을 주지 않습니다.

최근 20게임 순위는 최신순으로 정렬해 최대 20개를 표시합니다. 1~3위는 메달, 탈출은 출구 아이콘, 그 외 순위는 숫자로 표시합니다. 최근 5게임 상세 Embed에는 순위, 캐릭터, 시작 시각, 경기 시간, TK/K/A, DMG/VIS/H와 캐릭터 썸네일이 포함됩니다.

`!루미 설정`으로 최근 5게임 상세 출력 여부를 사용자별로 변경할 수 있습니다. 활성화하면 메인 Embed와 상세 Embed 최대 5개를 출력하고, 비활성화하면 메인 Embed만 출력합니다. 설정은 `user-registrations.json`에 저장되며 신규 사용자는 기본 활성화입니다.

닉네임을 찾지 못하거나 랭크 통계가 없을 때는 안내 메시지를 출력합니다. 최근 게임 상세를 만들 수 없어도 메인 전적 Embed는 출력하며, 캐릭터 이미지가 없으면 이미지 없이 표시합니다.

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

- [소스 코드 다운로드 (ZIP)](https://github.com/greentea0730/LumiBot/archive/refs/heads/main.zip)
- GitHub 페이지의 **Code → Download ZIP**에서도 다운로드할 수 있습니다.

## 실행 전 설정

공개 저장소에 비밀정보가 올라가지 않도록 다음 값은 플레이스홀더로 비워 두었습니다.

- `MainProgram.cs`의 `사용자 설정` 영역
- `Services/ERApiServices.cs`
  - `ApiKey`


## 요구 사항

- .NET 10 SDK
- Discord 봇 및 필요한 API 키

## 실행

```bash
dotnet restore
dotnet run
```
