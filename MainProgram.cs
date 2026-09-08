using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Discord.Rest;
using Microsoft.VisualBasic; 
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Google.Apis.YouTube.v3.Data;
using LumiBot.Services;

public class Program
{   
    private const ulong PrimaryChannelId = 0;
    private const ulong LegacyChannelId = 0;

    private readonly Dictionary<ulong, DateTime> _lastNotificationTimes = new Dictionary<ulong, DateTime>();
    private readonly HashSet<ulong> _pendingRecentGamesSettings = new();
    private readonly TimeSpan _cooldownTime = TimeSpan.FromMinutes(1); // 쿨타임 설정 (1분)


    private ERApiService _erApiService;//전적검색
    private UserRegistrationService _userRegistrationService;

    
    private DiscordSocketClient? _client;
    private AnonymousService _anonymousService;
    private readonly HttpClient _noticeHttpClient = new();
    private readonly SemaphoreSlim _noticeCheckLock = new(1, 1);
    private readonly string _noticeStatePath = Path.Combine(AppContext.BaseDirectory, "news-state.json");
    private Task? _noticeMonitorTask;
    private HashSet<string> _sentNoticeIds = new();

    public static Task Main(string[] args) => new Program().MainAsync();

    public async Task MainAsync()
{ 
    // 모든 필요한 권한(Intents)을 한 번에 더해줍니다.
    var config = new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.AllUnprivileged 
                         | GatewayIntents.GuildPresences  // 게임 상태 감시용
                         | GatewayIntents.GuildMembers    // 유저 정보 가져오기용
                         | GatewayIntents.MessageContent  
    };
    //이거 그거임 그 익명게시판ㅇㅇ
    
    // 클라이언트는 생성(1회용)
    _client = new DiscordSocketClient(config);
    
    _anonymousService = new AnonymousService(_client);
    
    
    _client.Log += Log;
    _client.MessageReceived += MessageReceived;
    _client.PresenceUpdated += OnPresenceUpdated; // 접속 알림 이벤트
    _client.Ready += OnReady;
    _client.Disconnected += exception =>
    {
        Console.WriteLine($"[Discord 연결 종료] {exception?.Message ?? "알 수 없는 원인"}");
        return Task.CompletedTask;
    };
    _erApiService = new ERApiService(new HttpClient());
    _userRegistrationService = new UserRegistrationService(
        Path.Combine(AppContext.BaseDirectory, "user-registrations.json"));
    
        var token = "YOUR_DISCORD_BOT_TOKEN";

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
        await _erApiService.LoadCharacterDataAsync();
        
        await Task.Delay(-1);
    }

    private Task OnReady()
    {
        _noticeMonitorTask ??= Task.Run(MonitorNoticesAsync);
        return Task.CompletedTask;
    }

    private IMessageChannel? GetPreferredChannel(params ulong[] channelIds)
    {
        foreach (var channelId in channelIds)
        {
            var channel = _client?.GetChannel(channelId) as IMessageChannel;
            if (channel != null)
                return channel;
        }

        return null;
    }

    private async Task MonitorNoticesAsync()
    {
        _sentNoticeIds = await LoadNoticeStateAsync();

        while (true)
        {
            try
            {
                var channel = GetPreferredChannel(PrimaryChannelId, LegacyChannelId);
                if (channel == null)
                    Console.WriteLine("[자동공지] 지정된 채널을 찾을 수 없습니다.");
                else
                    await CheckForNewNotice(channel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[자동공지 오류] {ex}");
            }

            await Task.Delay(TimeSpan.FromMinutes(5));
        }
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
   
    private async Task MessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        try
        {
           if (_pendingRecentGamesSettings.Remove(message.Author.Id))
           {
               if (message.Content.Trim() == "ㅇㅇ")
               {
                   var enabled = await _userRegistrationService.GetRecentGamesEnabledAsync(message.Author.Id);
                   await _userRegistrationService.SetRecentGamesEnabledAsync(message.Author.Id, !enabled);
                   await message.Channel.SendMessageAsync(
                       !enabled
                           ? "최근 5개게임 출력기능을 켰어요."
                           : "최근 5개게임 출력기능을 껐어요.");
               }
               else
               {
                   await message.Channel.SendMessageAsync("설정이 취소되었어요.");
               }

               return;
           }

           if (message.Content == "!공지테스트")
           {
               await CheckForNewNotice(message.Channel, true);
               return;
           }

           var content = message.Content.Split(' ');
           if (content[0] == "!공략")
           {
               string keyword = string.Join(" ", content.Skip(1));
               if (string.IsNullOrEmpty(keyword)) return;

               string result = await GetYoutubeGuideAsync(keyword);
               await message.Channel.SendMessageAsync(result);
               return;
           }
           if (content[0] == "!루미")
           {
               if (content.Length < 2)
               {
                   await message.Channel.SendMessageAsync("루미예요><");
                   return;
               }

               string command = content[1];
               switch (command)
               {                   case "주사위":
                       int number = new Random().Next(1, 7);
                       await message.Channel.SendMessageAsync($" 주사위를 던져 {number}이(가) 나왔어요!");
                       break;

                   case "공지":
                       var embed = new EmbedBuilder()
                       .WithTitle("🎮 이터널 리턴 공식 홈페이지")
                       .WithDescription("최신 소식과 가이드를 확인하려면 아래 링크를 클릭하세요!")
                       .WithUrl("https://playeternalreturn.com/")
                       .WithThumbnailUrl("https://cdn.playeternalreturn.com/img/favicon.png")
                       .WithColor(Color.Blue)
                       .AddField("패치노트및 뉴스", "[바로가기](https://playeternalreturn.com/posts/news)", true)
                       .AddField("이벤트", "[바로가기](https://playeternalreturn.com/rank/user)", true)
                       .WithCurrentTimestamp()
                       .Build();

                       await message.Channel.SendMessageAsync(embed: embed);
                       break;

                    

                   case "안녕":
                       string[] greetings =
                       {
                           $"{message.Author.Username}님, 반가워요! 오늘도 루미아 섬에서 1등 하실 거라 믿어요! ✨",
                           "앗, 실험 시작 전인가요? 루미가 옆에서 응원해 드릴게요! 🔥",
                           "루미 등장! 오늘따라 눈빛이 예리하신데요? 우승 각인가요? 💎",
                           " 실험 준비는 잘 되어가고 계신가요? 화이팅! 🚀"
                       };

                       var random = new Random();
                       int index = random.Next(greetings.Length);
                       await message.Channel.SendMessageAsync(greetings[index]);
                       break;

                    case "설정":
                       _pendingRecentGamesSettings.Add(message.Author.Id);
                       var recentGamesEnabled =
                           await _userRegistrationService.GetRecentGamesEnabledAsync(message.Author.Id);
                       await message.Channel.SendMessageAsync(
                           recentGamesEnabled
                               ? "최근 5개게임 출력기능을 끌까요? 끄려면 `ㅇㅇ`, 취소하려면 `ㄴㄴ`로 답해주세요."
                               : "최근 5개게임 출력기능을 킬까요? 켜려면 `ㅇㅇ`, 취소하려면 `ㄴㄴ`로 답해주세요.");
                       break;

                   case "도움말":
                       var helpEmbed = new EmbedBuilder()
                       .WithTitle("✨ LUMI Bot 명령어 가이드")
                       .WithDescription("루미봇을 이용해주셔서 감사해요! ><\n사용 가능한 명령어들은 아래와 같아요.")
                       .WithColor(new Color(0x77CDD6))
                       .AddField("📢 공지사항",
                           "`!루미 공지` : 최신 이터널 리턴 소식을 확인해요.\n", false)
                       .AddField("🎮 게임 정보",
                           "`!공략 실험체` : 해당 실험체의 가이드 영상을 가져와요.\n" +
                           "`!등록 게임닉네임` : 내 게임 닉네임을 등록해요.\n" +
                           "`!전적 (닉네임)` : 해당 사용자의 전적을 확인해요.", false)
                       .AddField("🕵️ 익명 메시지",
                           "DM으로 `!익명 (메시지내용)`을 보내 익명 메시지를 전달해요.", false)
                       .AddField("🛠️ 봇 정보",
                           "`!루미 정보` : 봇의 버전과 정보를 확인해요.\n" +
                           "`!루미 안녕` : 루미와 인사를 나눠요.\n" +
                           "`!루미 설정` : 최근 5개게임 출력 여부를 설정해요.", false)
                       .WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl() ?? _client.CurrentUser.GetDefaultAvatarUrl())
                       .WithImageUrl("https://playeternalreturn.com/img/main_visual_pc.jpg")
                       .WithFooter(footer =>
                       {
                           footer.Text = "LUMI Bot v1.0| Eternal Return Supporter";
                           footer.IconUrl = "https://cdn.playeternalreturn.com/img/favicon.png";
                       })
                       .WithCurrentTimestamp()
                       .Build();

                       await message.Channel.SendMessageAsync(embed: helpEmbed);
                       break;

                   case "정보":
                       await message.Channel.SendMessageAsync("LUMIbot.v.l.0 \nDiscord.Net v3.18.0/.NET 8.0 (LTS)/C# 12.0");
                       break;

                   default:
                       await message.Channel.SendMessageAsync($"'{command}'은(는) 제가 모르는 명령어예요.");
                       break;
               }
               return;
           }

           // 기존 익명 서비스
           await _anonymousService.HandleMessageAsync(message);

           var commandParts = message.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
           if (commandParts.Length > 0 && commandParts[0] == "!등록")
           {
               if (commandParts.Length < 2)
               {
                   await message.Channel.SendMessageAsync("사용법: `!등록 본인게임닉네임`이예요.");
                   return;
               }

               string nickname = string.Join(" ", commandParts.Skip(1));
               string? userId = await _erApiService.GetUserUidAsync(nickname);
               if (string.IsNullOrEmpty(userId))
               {
                   await message.Channel.SendMessageAsync("❌ 해당 게임 닉네임을 찾을 수 없어요.");
                   return;
               }

               await _userRegistrationService.SetAsync(message.Author.Id, nickname, userId);
               await message.Channel.SendMessageAsync(
                   $"✅ **{nickname}** 님으로 등록했어요. 이제 `!전적`만 입력해 전적을 확인할 수 있어요.");
               return;
           }

           if (commandParts.Length > 0 && commandParts[0] == "!전적")
           {
               string? userId;
               string nickname;

               if (commandParts.Length > 1)
               {
                   nickname = string.Join(" ", commandParts.Skip(1));
                   userId = await _erApiService.GetUserUidAsync(nickname);
               }
               else
               {
                   var registration = await _userRegistrationService.GetAsync(message.Author.Id);
                   if (registration == null)
                   {
                       await message.Channel.SendMessageAsync(
                           "❌ 등록된 게임 닉네임이 없어요. 먼저 `!등록 본인게임닉네임`을 입력해 주세요.");
                       return;
                   }

                   nickname = registration.Nickname;
                   userId = registration.UserId;
               }

               if (string.IsNullOrEmpty(userId))
               {
                   await message.Channel.SendMessageAsync("❌ 유저를 찾을 수 없어요.");
                   return;
               }

               await SendUserStatsAsync(message, nickname, userId);
               return;
           }

           if (message.Channel.Id == PrimaryChannelId && !string.IsNullOrWhiteSpace(message.Content))
           {
               var trimmed = message.Content.Trim();
               if (trimmed == "." || trimmed == "!")
               {
                   var registration = await _userRegistrationService.GetAsync(message.Author.Id);
                   if (registration == null)
                   {
                       await message.Channel.SendMessageAsync("❌ 등록된 게임 닉네임이 없어요. 먼저 `!등록 본인게임닉네임`을 입력해 주세요.");
                       return;
                   }

                   await SendUserStatsAsync(message, registration.Nickname, registration.UserId);
                   return;
               }

               if (!trimmed.StartsWith("!"))
               {
                   var userId = await _erApiService.GetUserUidAsync(trimmed);
                   if (string.IsNullOrEmpty(userId))
                   {
                       return;
                   }

                   await SendUserStatsAsync(message, trimmed, userId);
                   return;
               }
           }
       }
       catch (Exception ex)
       {
           Console.WriteLine($"[메시지 처리 오류] 채널 {message.Channel.Id}: {ex}");
       }
   }

   private async Task SendUserStatsAsync(SocketMessage message, string nickname, string userId)
   {
       var statsResult = await _erApiService.GetUsStatsAsync(userId);
       if (statsResult.Status == RankedStatsStatus.NoRankedGames)
       {
           await message.Channel.SendMessageAsync("ℹ️ 이 유저는 랭크게임을 플레이한 기록이 없어요.");
           return;
       }

       if (statsResult.Status == RankedStatsStatus.Error || statsResult.Stats == null)
       {
           await message.Channel.SendMessageAsync("❌ 전적을 조회하는 중 오류가 발생했어요.");
           return;
       }

       var stats = statsResult.Stats;
       var displayNickname = string.IsNullOrWhiteSpace(stats.Nickname) ? nickname : stats.Nickname;
       var recentGames = await _erApiService.GetRecentGamesAsync(userId);
       var weeklyRankPointGraph = _erApiService.GetWeeklyRankPointGraph(recentGames);
       var recentRankSummary = _erApiService.GetRecentRankSummary(recentGames);
       var recentGameDetails = _erApiService.GetRecentGameDetails(recentGames);
       string tierName = _erApiService.GetTierDisplayName(stats.Mmr, stats.Rank);
       string nextTierText = _erApiService.GetNextTierRemainingText(stats.Mmr, stats.Rank);

       var rankPercentValue = stats.RankPercent;
       string rankPercentText = rankPercentValue > 0
           ? $"상위 {rankPercentValue * 100:0}%"
           : stats.Rank > 0
               ? $"{stats.Rank}위"
               : "상위 0%";

       var topChars = stats.CharacterStats?
           .OrderByDescending(c => c.TotalGames)
           .Take(5)
           .Select(c =>
           {
               _erApiService.CharacterMap.TryGetValue(c.CharacterCode, out var name);
               name ??= $"Char {c.CharacterCode}";
               double wr = c.TotalGames > 0 ? (double)c.Wins / c.TotalGames * 100 : 0;
               return $"• **{name}** : {c.TotalGames}판 ({wr:F1}%)";
           });

       var embedBuilder = new EmbedBuilder()
           .WithTitle($"📊 {displayNickname} 님의 전적 리포트")
           .WithColor(new Color(0x3498db))
           .AddField("🏆 현재 티어", $"`{tierName}`", true)
           .AddField("📈 현재 점수", $"`{stats.Mmr} LP` ({rankPercentText})\n`{nextTierText}`", true)
           .AddField("🎮 게임 요약", $"총 `{stats.TotalGames}`판 | 승률 `{(stats.TotalGames > 0 ? (double)stats.TotalWins / stats.TotalGames * 100 : 0):F1}%` | Top3 `{(stats.Top3 * 100):F1}%`", false)
           .AddField("🔝 모스트 3 캐릭터", topChars != null ? string.Join("\n", topChars) : "데이터 없음")
           .AddField("📊 최근 7일 RP 그래프", weeklyRankPointGraph ?? "최근 7일 랭크게임 기록이 없어요.")
           .AddField("📋 최근 20게임 순위", recentRankSummary ?? "최근 20게임 랭크기록이 없어요.")
           .WithDescription("시즌 12 랭크 데이터\n!루미 설정으로 최근 5게임의 상세기록을 켜거나 끌 수 있어요.")
           .WithFooter("Eternal Return • Powered by ERApi")
           .WithCurrentTimestamp();

       await message.Channel.SendMessageAsync(embed: embedBuilder.Build());

       var recentGamesEnabled = await _userRegistrationService.GetRecentGamesEnabledAsync(message.Author.Id);
       if (recentGamesEnabled && recentGameDetails != null)
       {
           foreach (var detail in recentGameDetails)
           {
               var gameEmbedBuilder = new EmbedBuilder()
                   .WithColor(GetRecentGameColor(detail));
               var characterName = detail.CharacterName;
               var description =
                   $"```text\n" +
                   $"{GetRecentGameRankLabel(detail)} · {characterName,-8}{detail.PlayedAt,16} · {detail.Duration}\n" +
                   "TK /  K  /  A            DMG / VIS /  H\n" +
                   $"{detail.TeamKill,2} / {detail.PlayerKill,2} / {detail.PlayerAssistant,3}          " +
                   $"{detail.Damage,5} / {detail.Vision,3} / {detail.MonsterKill,3}\n" +
                   "```";

               var imagePath = detail.CharacterCode.HasValue
                   ? _erApiService.GetCharacterImagePath(detail.CharacterCode.Value)
                   : null;
               if (imagePath != null && File.Exists(imagePath))
               {
                   var fileName = $"recent-character-{detail.CharacterCode.Value}.png";
                   var attachment = new FileAttachment(imagePath, fileName);
                   gameEmbedBuilder
                       .WithThumbnailUrl(attachment.GetAttachmentUrl())
                       .WithDescription(description);
                   await message.Channel.SendFileAsync(
                       attachment,
                       embed: gameEmbedBuilder.Build());
               }
               else
               {
                   gameEmbedBuilder.WithDescription(description);
                   await message.Channel.SendMessageAsync(embed: gameEmbedBuilder.Build());
               }
           }
       }
   }

   private static Color GetRecentGameColor(RecentGameDetail detail)
   {
       if (detail.IsEscape)
           return new Color(0x191970);

       return detail.Rank switch
       {
           1 => new Color(0x90EE90),
           2 => new Color(0x4169E1),
           3 => new Color(0x87CEEB),
           _ => new Color(0x808080)
       };
   }

   private static string GetRecentGameRankLabel(RecentGameDetail detail)
   {
       if (detail.IsEscape)
           return "탈출";

       return detail.Rank.HasValue && detail.Rank.Value > 0
           ? $"{detail.Rank.Value}위"
           : "—";
   }
        

       
    private async Task CheckForNewNotice(IMessageChannel channel, bool forceSend = false)
{
    await _noticeCheckLock.WaitAsync();
    try
    {
        _noticeHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LumiBot/1.0");
        var response = await _noticeHttpClient.GetStringAsync(
            "https://playeternalreturn.com/api/v1/posts/news?page=1");
        var articles = JObject.Parse(response)["articles"]?.Children().ToList();
        if (articles == null || articles.Count == 0) return;

        if (forceSend)
        {
            await SendNoticeAsync(channel, articles[0]);
            return;
        }

        if (_sentNoticeIds.Count == 0)
        {
            var latestId = articles[0]["id"]?.ToString();
            if (!string.IsNullOrWhiteSpace(latestId))
                _sentNoticeIds.Add(latestId);
            await SaveNoticeStateAsync();
            Console.WriteLine($"[자동공지] 시작 기준점 저장: {latestId}");
            return;
        }

        foreach (var article in articles.AsEnumerable().Reverse())
        {
            var id = article["id"]?.ToString();
            if (string.IsNullOrWhiteSpace(id) || _sentNoticeIds.Contains(id))
                continue;

            await SendNoticeAsync(channel, article);
            _sentNoticeIds.Add(id);
            await SaveNoticeStateAsync();
        }
    }
    finally
    {
        _noticeCheckLock.Release();
    }
}

    private async Task SendNoticeAsync(IMessageChannel channel, JToken article)
    {
        var id = article["id"]?.ToString() ?? "unknown";
        var localized = article["i18ns"]?["ko_KR"] ?? article["i18ns"]?["en_US"];
        var title = localized?["title"]?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = localized?["summary"]?.ToString()?.Trim() ?? "새로운 소식이 도착했습니다!";

        var url = article["url"]?.ToString() ??
                  $"https://playeternalreturn.com/posts/news/{id}";
        var imageUrl = article["thumbnail_url"]?.ToString();
        var embed = new EmbedBuilder()
            .WithTitle("🔥 이터널 리턴 최신 소식")
            .WithDescription(title)
            .WithUrl(url)
            .WithColor(Color.Red)
            .WithFooter($"공지 번호: {id}")
            .WithCurrentTimestamp();

        if (!string.IsNullOrWhiteSpace(imageUrl))
            embed.WithImageUrl(imageUrl);

        await channel.SendMessageAsync(embed: embed.Build());
    }

    private async Task<HashSet<string>> LoadNoticeStateAsync()
    {
        if (!File.Exists(_noticeStatePath))
            return new HashSet<string>();

        var json = await File.ReadAllTextAsync(_noticeStatePath);
        return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
    }

    private async Task SaveNoticeStateAsync()
    {
        var json = JsonSerializer.Serialize(_sentNoticeIds);
        await File.WriteAllTextAsync(_noticeStatePath, json);
    }

    private async Task OnPresenceUpdated(SocketUser user, SocketPresence oldPresence, SocketPresence newPresence)
    {
        var oldGame = oldPresence.Activities.FirstOrDefault(x => x.Name == "Eternal Return");
        var newGame = newPresence.Activities.FirstOrDefault(x => x.Name == "Eternal Return");

        // 1. 일단 이리를 새로 켰는지 확인
        if (oldGame == null && newGame != null)
        {
            ulong userId = user.Id;
            DateTime now = DateTime.Now;

            
            if (_lastNotificationTimes.ContainsKey(userId) && (now - _lastNotificationTimes[userId]) < _cooldownTime)
            {
                return; 
            }

            // 알림 전송
            var channel = GetPreferredChannel(PrimaryChannelId, LegacyChannelId);
            if (channel != null)
            {
                //  현재 시간 업데이트
                _lastNotificationTimes[userId] = now;

                string nickname = (user as IGuildUser)?.Nickname ?? user.Username;
                await channel.SendMessageAsync($"🎮 **{nickname}**님이 루미아 섬에 입국했습니다!");
            }
        }
    }

    private async Task<string> GetYoutubeGuideAsync(string query)
{
    var youtubeService = new YouTubeService(new BaseClientService.Initializer()
    {
        ApiKey = "YOUR_YOUTUBE_API_KEY",
        ApplicationName = "LumiBot"
    });

    string myPlaylistId = "YOUR_YOUTUBE_PLAYLIST_ID";
    string nextPageToken = "";
    string lowerQuery = query.ToLower();

    // 더 이상 가져올 영상이 없을 때까지 반복
    while (nextPageToken != null)
    {
        var playlistRequest = youtubeService.PlaylistItems.List("snippet");
        playlistRequest.PlaylistId = myPlaylistId;
        playlistRequest.MaxResults = 80;
        playlistRequest.PageToken = nextPageToken; // 다음 페이지 정보를 넣음

        var playlistResponse = await playlistRequest.ExecuteAsync();

        //에서 키워드 포함 여부 확인
        var foundItem = playlistResponse.Items.FirstOrDefault(item => 
            item.Snippet.Title.ToLower().Contains(lowerQuery) || 
            item.Snippet.Description.ToLower().Contains(lowerQuery));

        if (foundItem != null)
        {
            return $"📌 **요청하신 자료를 드릴게요.** https://youtu.be/{foundItem.Snippet.ResourceId.VideoId}";
        }

        // 다음 페이지가 있는지 확인
        nextPageToken = playlistResponse.NextPageToken;
    }

    // 모든 페이지를 뒤졌는데도 없을 경우
    string encodedQuery = Uri.EscapeDataString(query);
    return $"🔍 재생목록에 없네요. 대신 유튜브 전체 검색 링크를 드릴게요!\n🔗 https://www.youtube.com/results?search_query=이리%{encodedQuery}";
}
public async Task HandleAnonymousMessage(SocketMessage message)
{
    
    await _anonymousService.HandleMessageAsync(message);
    //익명게시판 작동파일에 따로 빼둠
    
}


}   
