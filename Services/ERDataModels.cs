using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LumiBot.Services
{
    // 1. 유저 기본 정보 응답
    public class ERUserResponse
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("user")] public UserInfo User { get; set; }
    }

    public class UserInfo
    {
        [JsonPropertyName("userId")] public string UserId { get; set; }
        [JsonPropertyName("nickname")] public string Nickname { get; set; }
    }

    // 2. 유저 통계 정보 응답
    public class ERStatsResponse
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("userStats")] public List<UserStat> UserStats { get; set; }
    }

    public class ERUserGamesResponse
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("userGames")] public List<UserGame> UserGames { get; set; } = new();
        [JsonPropertyName("next")] public int? Next { get; set; }
    }

    public class UserGame
    {
        [JsonPropertyName("matchingMode")] public int MatchingMode { get; set; }
        [JsonPropertyName("mmrBefore")] public int? MmrBefore { get; set; }
        [JsonPropertyName("mmrGain")] public int? MmrGain { get; set; }
        [JsonPropertyName("mmrAfter")] public int? MmrAfter { get; set; }
        [JsonPropertyName("startDtm")] public string? StartDtm { get; set; }
        [JsonPropertyName("gameRank")] public int? GameRank { get; set; }
        [JsonPropertyName("escapeState")] public int? EscapeState { get; set; }
        [JsonPropertyName("characterNum")] public int? CharacterNum { get; set; }
        [JsonPropertyName("teamKill")] public int? TeamKill { get; set; }
        [JsonPropertyName("playerKill")] public int? PlayerKill { get; set; }
        [JsonPropertyName("playerAssistant")] public int? PlayerAssistant { get; set; }
        [JsonPropertyName("damageToPlayer")] public int? DamageToPlayer { get; set; }
        [JsonPropertyName("viewContribution")] public int? ViewContribution { get; set; }
        [JsonPropertyName("monsterKill")] public int? MonsterKill { get; set; }
        [JsonPropertyName("totalTripleKill")] public int? TotalTripleKill { get; set; }
        [JsonPropertyName("totalQuadraKill")] public int? TotalQuadraKill { get; set; }
        [JsonPropertyName("duration")] public int? Duration { get; set; }
    }

    public sealed record RecentGameDetail(
        int? Rank,
        bool IsEscape,
        int? CharacterCode,
        string PlayedAt,
        string Duration,
        string CharacterName,
        int TeamKill,
        int PlayerKill,
        int PlayerAssistant,
        int Damage,
        int Vision,
        int MonsterKill);

    public class UserStat
    {
        [JsonPropertyName("mmr")] public int Mmr { get; set; }
        [JsonPropertyName("rank")] public int Rank { get; set; } // 등수 (이터니티 확인용)
        [JsonPropertyName("totalGames")] public int TotalGames { get; set; }
        [JsonPropertyName("totalWins")] public int TotalWins { get; set; }
        [JsonPropertyName("nickname")] public string Nickname { get; set; }
        [JsonPropertyName("rankPercent")] public double RankPercent { get; set; }
        [JsonPropertyName("top3")] public double Top3 { get; set; }
        
        // 캐릭터별 통계 추가
        [JsonPropertyName("characterStats")] 
        public List<CharacterStat> CharacterStats { get; set; }
    }

    public class CharacterStat
    {
        [JsonPropertyName("characterCode")] public int CharacterCode { get; set; }
        [JsonPropertyName("totalGames")] public int TotalGames { get; set; }
        [JsonPropertyName("wins")] public int Wins { get; set; }
    }

    // 3. 캐릭터 메타데이터 응답
    public class ERCharacterResponse
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("data")] public List<CharacterData> Data { get; set; }
    }

    public class CharacterData
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
    }

    public class ERLocalizationResponse
    {
        [JsonPropertyName("code")] public int Code { get; set; }
        [JsonPropertyName("data")] public LocalizationData? Data { get; set; }
    }

    public class LocalizationData
    {
        [JsonPropertyName("l10Path")] public string? L10nPath { get; set; }
    }
}