using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace LumiBot.Services
{
    public enum RankedStatsStatus
    {
        Found,
        NoRankedGames,
        Error
    }

    public sealed record RankedStatsResult(RankedStatsStatus Status, UserStat? Stats = null);

    public class ERApiService
    {
        private readonly HttpClient _http;
        private const string BaseUrl = "https://open-api.bser.io";
        private const string ApiKey = "YOUR_ER_API_KEY";
        private static readonly DateTime CurrentSeasonStart = new(2026, 8, 6);

        // 캐릭터 코드와 이름을 매칭할 사전
        public Dictionary<int, string> CharacterMap { get; private set; } = new();

        private static readonly int[] CharacterSheetOrder =
        {
            1, 2, 7, 4, 3, 6, 5, 8, 9, 10, 11, 12, 13, 15, 14, 17, 18, 16,
            19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34,
            35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50,
            51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66,
            67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82,
            83, 84, 85, 86, 87, 88, 89, 90
        };

        public string? GetCharacterImagePath(int characterCode)
        {
            var sheetIndex = Array.IndexOf(CharacterSheetOrder, characterCode) + 1;
            return sheetIndex == 0
                ? null
                : Path.Combine(AppContext.BaseDirectory, "assets", "characters", $"{sheetIndex}.png");
        }

        public ERApiService(HttpClient http)
        {
            _http = http;
            if (!_http.DefaultRequestHeaders.Contains("x-api-key"))
            {
                _http.DefaultRequestHeaders.Add("x-api-key", ApiKey);
            }
        }

        // 캐릭터 데이터 초기 로드 (봇 시작 시 호출 권장)
        public async Task LoadCharacterDataAsync()
        {
            try
            {
                var res = await _http.GetFromJsonAsync<ERCharacterResponse>($"{BaseUrl}/v2/data/Character");
                if (res?.Code == 200)
                {
                    foreach (var item in res.Data)
                    {
                        CharacterMap[item.Code] = item.Name;
                    }
                }

                var localization = await _http.GetFromJsonAsync<ERLocalizationResponse>(
                    $"{BaseUrl}/v1/l10n/Korean");

                if (localization?.Code == 200 &&
                    !string.IsNullOrWhiteSpace(localization.Data?.L10nPath))
                {
                    var koreanData = await _http.GetStringAsync(localization.Data.L10nPath);
                    foreach (var line in koreanData.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        const string keyPrefix = "Character/Name/";
                        var separatorIndex = line.IndexOf('┃');
                        if (separatorIndex <= keyPrefix.Length ||
                            !line.StartsWith(keyPrefix, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (int.TryParse(line[keyPrefix.Length..separatorIndex], out var code))
                        {
                            CharacterMap[code] = line[(separatorIndex + 1)..].TrimEnd('\r');
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Char Load Error: {ex.Message}"); }
        }

        public async Task<string> GetUserUidAsync(string nickname)
        {
            try
            {
                string encodedName = WebUtility.UrlEncode(nickname);
                string url = $"{BaseUrl}/v1/user/nickname?query={encodedName}";
                var res = await _http.GetFromJsonAsync<ERUserResponse>(url);
                return res?.Code == 200 ? res.User?.UserId : null;
            }
            catch { return null; }
        }

        public async Task<RankedStatsResult> GetUsStatsAsync(string userId)
        {
            try
            {
                int seasonId = 41;
                string url = $"{BaseUrl}/v2/user/stats/uid/{userId}/{seasonId}/3";
                
                
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var res = await _http.GetFromJsonAsync<ERStatsResponse>(url, options);

                if (res?.Code == 200)
                {
                    // 200 응답이어도 랭크 전적이 없으면 빈 통계 객체가 반환될 수 있다.
                    var rankedStats = res.UserStats?
                        .FirstOrDefault(stat => stat.TotalGames > 0);
                    return rankedStats == null
                        ? new RankedStatsResult(RankedStatsStatus.NoRankedGames)
                        : new RankedStatsResult(RankedStatsStatus.Found, rankedStats);
                }
            }
            catch (Exception ex) { Console.WriteLine($"Stats Error: {ex.Message}"); }
            return new RankedStatsResult(RankedStatsStatus.Error);
        }

        public string? GetRecentRankSummary(IReadOnlyList<UserGame> games)
        {
            try
            {
                var rankedGames = games
                    .Where(game => game.MatchingMode == 3)
                    .OrderByDescending(game => TryParseGameDate(game.StartDtm, out var parsed) ? parsed : DateTime.MinValue)
                    .Take(20)
                    .ToList();

                if (rankedGames.Count == 0)
                    return null;

                var formattedRanks = rankedGames
                    .Select(FormatRecentRankToken)
                    .ToList();

                var lines = formattedRanks
                    .Chunk(10)
                    .Select(chunk => string.Join(" | ", chunk.Select(value => value == "—" ? "—" : value)))
                    .ToList();

                return $"```\n{string.Join("\n\n", lines)}\n```";
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Recent rank summary error: {ex.Message}");
                return null;
            }
        }

        public IReadOnlyList<RecentGameDetail>? GetRecentGameDetails(IReadOnlyList<UserGame> games)
        {
            try
            {
                var recentGames = games
                    .Where(game => game.MatchingMode == 3)
                    .OrderByDescending(game => TryParseGameDate(game.StartDtm, out var parsed) ? parsed : DateTime.MinValue)
                    .Take(5)
                    .ToList();

                if (recentGames.Count == 0)
                    return null;

                var details = recentGames.Select(game =>
                {
                    var characterName = game.CharacterNum.HasValue &&
                        CharacterMap.TryGetValue(game.CharacterNum.Value, out var name)
                        ? name
                        : "알 수 없음";
                    var playedAt = TryParseGameDate(game.StartDtm, out var playedDate)
                        ? playedDate.ToString("M월 d일 H시 mm분")
                        : "시간 정보 없음";
                    var duration = game.Duration.HasValue
                        ? $"{game.Duration.Value / 60}:{game.Duration.Value % 60:00}"
                        : "시간 정보 없음";
                    return new RecentGameDetail(
                        game.GameRank,
                        game.EscapeState == 3,
                        game.CharacterNum,
                        playedAt,
                        duration,
                        characterName,
                        game.TeamKill ?? 0,
                        game.PlayerKill ?? 0,
                        game.PlayerAssistant ?? 0,
                        game.DamageToPlayer ?? 0,
                        game.ViewContribution ?? 0,
                        game.MonsterKill ?? 0);
                }).ToList();

                return details;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Recent game detail summary error: {ex.Message}");
                return null;
            }
        }

        private static string FormatRecentRankToken(UserGame game)
        {
            var escapeState = game.EscapeState;
            if (escapeState == 3)
                return "🚪";

            var rank = game.GameRank;
            if (!rank.HasValue || rank.Value <= 0)
                return "—";

            var value = rank.Value;
            if (value == 1) return "🥇";
            if (value == 2) return "🥈";
            if (value == 3) return "🥉";
            return value.ToString();
        }

        public string? GetWeeklyRankPointGraph(IReadOnlyList<UserGame> games)
        {
            try
            {
                const int rankedMode = 3;
                if (games.Count == 0)
                    return null;

                var dailyScores = games
                    .Where(game => game.MatchingMode == rankedMode && IsCurrentSeasonGame(game.StartDtm))
                    .Select(game =>
                    {
                        if (!TryParseGameDate(game.StartDtm, out var playedDate))
                            return (Date: (DateTime?)null, Score: (int?)null, StartDtm: game.StartDtm);

                        return (Date: playedDate.Date, Score: GetGameScore(game), StartDtm: game.StartDtm);
                    })
                    .Where(item => item.Date.HasValue && item.Score.HasValue)
                    .GroupBy(item => item.Date!.Value)
                    .ToDictionary(group => group.Key, group => group
                        .OrderByDescending(item => item.StartDtm, StringComparer.Ordinal)
                        .First().Score!.Value);

                if (dailyScores.Count == 0)
                    return null;

                // 게임한 날 기준 최근 7일 (또는 전체 일수가 7일 미만인 경우)
                var uniqueDates = dailyScores.Keys.OrderByDescending(d => d).Take(7).OrderBy(d => d).ToList();
                var scores = uniqueDates.Select(date => dailyScores[date]).ToList();

                var displayScores = ClampOutliersForGraph(scores, out var outliersClamped);
                var displayScoreByDate = uniqueDates
                    .Select((date, index) => (date, score: displayScores[index]))
                    .ToDictionary(item => item.date, item => item.score);
                var min = displayScores.Min();
                var max = displayScores.Max();

                // 점수값을 50, 100, 500 단위로 맞추기
                var roundedMin = (int)Math.Floor(min / 50.0) * 50;
                var roundedMax = (int)Math.Ceiling(max / 50.0) * 50;
                const int minimumGraphRange = 500;
                if (roundedMax - roundedMin < minimumGraphRange)
                {
                    var center = (roundedMin + roundedMax) / 2.0;
                    roundedMin = (int)Math.Floor((center - minimumGraphRange / 2.0) / 50.0) * 50;
                    roundedMax = roundedMin + minimumGraphRange;
                }
                
                var thresholds = GenerateThresholds(roundedMin, roundedMax);
                
                var lines = new List<string> { "```", "최근 7일 RP" };
                const int columnWidth = 4;
                
                foreach (var threshold in thresholds.OrderByDescending(t => t))
                {
                    var row = string.Concat(uniqueDates.Select(date =>
                        (displayScoreByDate[date] >= threshold ? "█" : " ")
                            .PadRight(columnWidth)));
                    lines.Add($"{threshold,5} | {row}");
                }

                lines.Add($"      +{new string('-', columnWidth * uniqueDates.Count)}");
                lines.Add("        " + string.Concat(uniqueDates.Select(date =>
                    date.ToString("dd").PadRight(columnWidth))));
                lines.Add("```");
                return string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Weekly RP Graph Error: {ex.Message}");
                return null;
            }
        }

        private static List<int> ClampOutliersForGraph(IReadOnlyList<int> scores, out bool outliersClamped)
        {
            outliersClamped = false;
            if (scores.Count < 4)
                return scores.ToList();

            var sorted = scores.OrderBy(score => score).ToArray();
            var lowerQuartile = InterpolatePercentile(sorted, 0.25);
            var upperQuartile = InterpolatePercentile(sorted, 0.75);
            var interquartileRange = upperQuartile - lowerQuartile;

            if (interquartileRange <= 0)
                return scores.ToList();

            var lowerFence = lowerQuartile - 1.5 * interquartileRange;
            var upperFence = upperQuartile + 1.5 * interquartileRange;
            var lowerBound = (int)Math.Ceiling(lowerFence);
            var upperBound = (int)Math.Floor(upperFence);
            var clampedScores = new List<int>(scores.Count);
            foreach (var score in scores)
            {
                var clampedScore = Math.Clamp(score, lowerBound, upperBound);
                if (clampedScore != score)
                    outliersClamped = true;
                clampedScores.Add(clampedScore);
            }

            return clampedScores;
        }

        private static double InterpolatePercentile(IReadOnlyList<int> sortedValues, double percentile)
        {
            var position = (sortedValues.Count - 1) * percentile;
            var lowerIndex = (int)Math.Floor(position);
            var upperIndex = (int)Math.Ceiling(position);
            if (lowerIndex == upperIndex)
                return sortedValues[lowerIndex];

            var fraction = position - lowerIndex;
            return sortedValues[lowerIndex] +
                   (sortedValues[upperIndex] - sortedValues[lowerIndex]) * fraction;
        }

        public async Task<List<UserGame>> GetRecentGamesAsync(string userId)
        {
           var games = new List<UserGame>();
           int? next = null;
           var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
           var sevenDayCutoff = DateTime.Now.Date.AddDays(-6);

           // 하루에 많은 게임을 플레이한 유저는 7일치 기록이 여러 페이지에 걸칠 수 있다.
           const int maxHistoryPages = 100;
           for (var page = 0; page < maxHistoryPages; page++)
           {
               var url = $"{BaseUrl}/v1/user/games/uid/{userId}";
               if (next.HasValue)
                   url += $"?next={next.Value}";

               ERUserGamesResponse? response = null;
               var retryCount = 0;

               while (retryCount < 3)
               {
                   try
                   {
                       response = await _http.GetFromJsonAsync<ERUserGamesResponse>(url, options);
                       if (response != null && response.Code == 200)
                           break;

                       if (response != null && (response.Code == 429 || response.Code >= 500))
                       {
                           retryCount++;
                           await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount - 1)));
                           continue;
                       }

                       break;
                   }
                   catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests ||
                                                      ex.StatusCode == HttpStatusCode.InternalServerError ||
                                                      ex.StatusCode == HttpStatusCode.BadGateway ||
                                                      ex.StatusCode == HttpStatusCode.ServiceUnavailable ||
                                                      ex.StatusCode == HttpStatusCode.GatewayTimeout)
                   {
                       retryCount++;
                       if (retryCount >= 3)
                       {
                           Console.WriteLine($"Game history page {page + 1} failed after retries: {ex.Message}");
                           break;
                       }

                       await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount - 1)));
                   }
                   catch (Exception ex)
                   {
                       Console.WriteLine($"Game history page {page + 1} error: {ex.Message}");
                       break;
                   }
               }

               if (response == null || response.Code != 200)
                   break;

               if (response.UserGames.Count == 0)
                   break;

               var filteredPageGames = response.UserGames
                   .Where(game => IsCurrentSeasonGame(game.StartDtm))
                   .ToList();

               if (filteredPageGames.Count == 0)
                   break;

               games.AddRange(filteredPageGames);

               var rankedGameCount = games.Count(game => game.MatchingMode == 3);
               var hasSevenDayHistory = games.Any(game =>
                   game.MatchingMode == 3 &&
                   TryParseGameDate(game.StartDtm, out var playedDate) &&
                   playedDate.Date <= sevenDayCutoff);
               if (rankedGameCount >= 20 && hasSevenDayHistory)
                   break;

               if (!response.Next.HasValue || response.Next.Value == 0)
                   break;

               if (response.Next == next)
                   break;

               next = response.Next;
           }

           return games;
        }

        private static int? GetGameScore(UserGame game)
        {
            if (game.MmrAfter.HasValue)
                return game.MmrAfter.Value;

            if (game.MmrBefore.HasValue && game.MmrGain.HasValue)
                return game.MmrBefore.Value + game.MmrGain.Value;

            return null;
        }

        private static bool IsCurrentSeasonGame(string? startDtm)
        {
            if (!TryParseGameDate(startDtm, out var playedDate))
                return false;

            return playedDate.Date >= CurrentSeasonStart.Date;
        }

        private static bool TryParseGameDate(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return DateTime.TryParse(value, out date);
        }

        private static List<int> GenerateThresholds(int min, int max)
        {
            var thresholds = new List<int>();
            var range = max - min;

            // 범위에 따라 적절한 단위 선택
            int step;
            if (range >= 5000)
                step = 1000;
            else if (range >= 1000)
                step = 500;
            else
                step = 100;

            for (var value = min; value <= max; value += step)
                thresholds.Add(value);

            // 최댓값이 포함되지 않으면 추가
            if (!thresholds.Contains(max))
                thresholds.Add(max);

            return thresholds;
        }

        // 티어 판정 함수
          public string GetTierName(int mmr, int rank)
          {
              // 1. 최상위권 (8300점 이상 + 순위 조건)
              if (mmr >= 8300)
              {
                  if (rank > 0 && rank <= 300) return "🔴 이터니티";
                  if (rank > 0 && rank <= 1000) return "⚪ 데미갓";
              }

              // 2. 미스릴 (7600점 이상)
              if (mmr >= 7600) return "🔷 미스릴";

              // 3. 메테오라이트 (6400 ~ 7599)
              if (mmr >= 7300) return "🔵 메테오라이트 I";
              if (mmr >= 7000) return "🔵 메테오라이트 II";
              if (mmr >= 6700) return "🔵 메테오라이트 III";
              if (mmr >= 6400) return "🔵 메테오라이트 IV";

              // 4. 다이아몬드 (5000 ~ 6399)
              if (mmr >= 6050) return "🔷 다이아몬드 I";
              if (mmr >= 5700) return "🔷 다이아몬드 II";
              if (mmr >= 5350) return "🔷 다이아몬드 III";
              if (mmr >= 5000) return "🔷 다이아몬드 IV";

              // 5. 플래티넘 (3600 ~ 4999)
              if (mmr >= 4650) return "🟢 플래티넘 I";
              if (mmr >= 4300) return "🟢 플래티넘 II";
              if (mmr >= 3950) return "🟢 플래티넘 III";
              if (mmr >= 3600) return "🟢 플래티넘 IV";

              // 6. 골드 (2400 ~ 3599)
              if (mmr >= 3300) return "🟡 골드 I";
              if (mmr >= 3000) return "🟡 골드 II";
              if (mmr >= 2700) return "🟡 골드 III";
              if (mmr >= 2400) return "🟡 골드 IV";

              // 7. 실버 (1400 ~ 2399)
              if (mmr >= 2150) return "⚪ 실버 I";
              if (mmr >= 1900) return "⚪ 실버 II";
              if (mmr >= 1650) return "⚪ 실버 III";
              if (mmr >= 1400) return "⚪ 실버 IV";

              // 8. 브론즈 (600 ~ 1399)
              if (mmr >= 1200) return "🟤 브론즈 I";
              if (mmr >= 1000) return "🟤 브론즈 II";
              if (mmr >= 800) return "🟤 브론즈 III";
              if (mmr >= 600) return "🟤 브론즈 IV";

              // 9. 아이언 (0 ~ 599)
              if (mmr >= 450) return "⚪ 아이언 I";
              if (mmr >= 300) return "⚪ 아이언 II";
              if (mmr >= 150) return "⚪ 아이언 III";
              return "⚪ 아이언 IV";
        }

          public string GetTierDisplayName(int mmr, int rank)
          {
              var tierName = GetTierName(mmr, rank);
              if (rank > 0 && (
                  tierName.Contains("이터니티", StringComparison.Ordinal) ||
                  tierName.Contains("데미갓", StringComparison.Ordinal) ||
                  tierName.Contains("미스릴", StringComparison.Ordinal)))
              {
                  return $"{tierName} ({rank}위)";
              }

              return tierName;
          }

          public string GetNextTierRemainingText(int mmr, int rank)
          {
              if (mmr >= 8300)
                  return "최고 티어 도달";

              var thresholds = new[]
              {
                  (Threshold: 150, Name: "아이언 III"),
                  (Threshold: 300, Name: "아이언 II"),
                  (Threshold: 450, Name: "아이언 I"),
                  (Threshold: 600, Name: "브론즈 IV"),
                  (Threshold: 800, Name: "브론즈 III"),
                  (Threshold: 1000, Name: "브론즈 II"),
                  (Threshold: 1200, Name: "브론즈 I"),
                  (Threshold: 1400, Name: "실버 IV"),
                  (Threshold: 1650, Name: "실버 III"),
                  (Threshold: 1900, Name: "실버 II"),
                  (Threshold: 2150, Name: "실버 I"),
                  (Threshold: 2400, Name: "골드 IV"),
                  (Threshold: 2700, Name: "골드 III"),
                  (Threshold: 3000, Name: "골드 II"),
                  (Threshold: 3300, Name: "골드 I"),
                  (Threshold: 3600, Name: "플래티넘 IV"),
                  (Threshold: 3950, Name: "플래티넘 III"),
                  (Threshold: 4300, Name: "플래티넘 II"),
                  (Threshold: 4650, Name: "플래티넘 I"),
                  (Threshold: 5000, Name: "다이아몬드 IV"),
                  (Threshold: 5350, Name: "다이아몬드 III"),
                  (Threshold: 5700, Name: "다이아몬드 II"),
                  (Threshold: 6050, Name: "다이아몬드 I"),
                  (Threshold: 6400, Name: "메테오라이트 IV"),
                  (Threshold: 6700, Name: "메테오라이트 III"),
                  (Threshold: 7000, Name: "메테오라이트 II"),
                  (Threshold: 7300, Name: "메테오라이트 I"),
                  (Threshold: 7600, Name: "미스릴"),
                  (Threshold: 8300, Name: "이터니티")
              };

              foreach (var (threshold, name) in thresholds)
              {
                  if (threshold > mmr)
                      return $"다음 구간까지 {threshold - mmr}점 남았어요 ({name})";
              }

              return "최고 구간 도달";
          }
      }
}
