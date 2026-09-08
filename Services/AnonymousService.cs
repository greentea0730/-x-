using System;
using System.Threading.Tasks;
using System.Net.Http;
using Discord;
using Discord.WebSocket;




namespace LumiBot.Services
{
    public class AnonymousService
    {
        private readonly DiscordSocketClient _client;

        public AnonymousService(DiscordSocketClient client)
        {
            _client = client;
        }

        public async Task HandleMessageAsync(SocketMessage message)
        {
            // 1. DM인지 확인
            if (message.Channel is IPrivateChannel && !message.Author.IsBot)
            {
                // !익명 명령어로 시작하는지 체크
                if (message.Content.StartsWith("!익명 "))
                {
                    string content = message.Content.Replace("!익명 ", "");

                    //서버찾기
                    var guild = _client.GetGuild(0); // 서버 ID를 사용하여 서버 가져오기
                    if (guild == null)
                    {
                        throw new InvalidOperationException("익명 게시판 서버를 찾을 수 없습니다. 서버 ID와 봇의 서버 참가 상태를 확인하세요.");
                    }

                    var postChannel = guild.GetTextChannel(0) as ITextChannel
                        ?? guild.GetTextChannel(0) as ITextChannel;
                    if (postChannel == null)
                    {
                        throw new InvalidOperationException("익명 게시판 채널을 찾을 수 없습니다. 채널 ID와 봇 권한을 확인하세요.");
                    }

                    var embed = new EmbedBuilder()
                        .WithTitle(content)
                        .WithColor(Color.Green)
                        .WithFooter(footer => footer.Text = $"익명으로 글이 도착했습니다")
                        .Build();

                    await postChannel.SendMessageAsync(embed: embed);

                    var logChannel = guild.GetTextChannel(0) as ITextChannel
                        ?? guild.GetTextChannel(0) as ITextChannel;
                    if (logChannel == null)
                    {
                        throw new InvalidOperationException("익명 게시판 로그 채널을 찾을 수 없습니다. 채널 ID와 봇 권한을 확인하세요.");
                    }
                    await logChannel.SendMessageAsync($"[로그] 익명 제보자: {message.Author.Mention} ({message.Author.Id})\n내용: {content}");
                }
            }
        }
    }
}