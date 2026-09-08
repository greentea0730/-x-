using System.Text.Json;

namespace LumiBot.Services
{
    public sealed class RegisteredUser
    {
        public string Nickname { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public bool RecentGamesEnabled { get; set; } = true;
    }

    public sealed class UserRegistrationService
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private Dictionary<ulong, RegisteredUser> _registrations = new();
        private bool _loaded;

        public UserRegistrationService(string filePath)
        {
            _filePath = filePath;
        }

        public async Task<RegisteredUser?> GetAsync(ulong discordUserId)
        {
            await EnsureLoadedAsync();
            return _registrations.TryGetValue(discordUserId, out var registration) &&
                   !string.IsNullOrWhiteSpace(registration.Nickname) &&
                   !string.IsNullOrWhiteSpace(registration.UserId)
                ? registration
                : null;
        }

        public async Task SetAsync(ulong discordUserId, string nickname, string userId)
        {
            await EnsureLoadedAsync();
            await _fileLock.WaitAsync();
            try
            {
                _registrations[discordUserId] = new RegisteredUser
                {
                    Nickname = nickname,
                    UserId = userId
                };

                var json = JsonSerializer.Serialize(_registrations, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(_filePath, json);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<bool> GetRecentGamesEnabledAsync(ulong discordUserId)
        {
            await EnsureLoadedAsync();
            return _registrations.TryGetValue(discordUserId, out var registration)
                ? registration.RecentGamesEnabled
                : true;
        }

        public async Task SetRecentGamesEnabledAsync(ulong discordUserId, bool enabled)
        {
            await EnsureLoadedAsync();
            await _fileLock.WaitAsync();
            try
            {
                if (!_registrations.TryGetValue(discordUserId, out var registration))
                {
                    registration = new RegisteredUser();
                    _registrations[discordUserId] = registration;
                }

                registration.RecentGamesEnabled = enabled;
                var json = JsonSerializer.Serialize(_registrations, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(_filePath, json);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private async Task EnsureLoadedAsync()
        {
            if (_loaded)
                return;

            await _fileLock.WaitAsync();
            try
            {
                if (_loaded)
                    return;

                if (File.Exists(_filePath))
                {
                    var json = await File.ReadAllTextAsync(_filePath);
                    _registrations = JsonSerializer.Deserialize<Dictionary<ulong, RegisteredUser>>(json)
                        ?? new Dictionary<ulong, RegisteredUser>();
                }

                _loaded = true;
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}
