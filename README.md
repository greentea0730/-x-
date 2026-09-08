# LumiBot

Eternal Return 전적 검색, Discord 알림, 익명 게시판, YouTube 공략 검색을 제공하는 Discord 봇입니다.

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

각 서비스에서 발급받은 값으로 교체한 뒤 실행하세요. 토큰과 API 키는 다른 사람에게 공개하지 마세요.

## 요구 사항

- .NET 10 SDK
- Discord 봇 및 필요한 API 키

## 실행

```bash
dotnet restore
dotnet run
```
