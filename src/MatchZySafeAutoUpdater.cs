using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;

namespace MatchZy;

/// <summary>
/// MatchZy-aware CS2 auto-updater that NEVER restarts while a MatchZy match is in progress.
/// It only restarts when matchzy_tournament_status is in a safe state (idle/postgame/error).
/// Implemented as part of the main MatchZy plugin.
/// </summary>
public partial class MatchZy
{

    private const string SteamApiEndpoint =
        "https://api.steampowered.com/ISteamApps/UpToDateCheck/v0001/?appid=730&version={0}";

    // Timings (seconds)
    private const float DefaultUpdateCheckIntervalSeconds = 300f; // 5 minutes
    private const float ShutdownRetryDelaySeconds = 60f;          // 1 minute

    // State
    private static double _updateFoundTime;
    private static bool _updateAvailable;
    private static bool _restartRequired;
    private static int _requiredVersion;
    private static double _nextUpdateCheckAllowedTime;
    private static bool _offlineWarningLogged;

    // Cvars we care about from MatchZy
    private static ConVar? _matchzyTournamentStatus;
    private static ConVar? _matchzyTournamentMatch;

    /// <summary>
    /// Initialize the MatchZy-safe auto-updater. Called from MatchZy.Load().
    /// </summary>
    private void InitializeMatchZySafeAutoUpdater()
    {
        _matchzyTournamentStatus = ConVar.Find("matchzy_tournament_status");
        _matchzyTournamentMatch = ConVar.Find("matchzy_tournament_match");

        RegisterListener<Listeners.OnGameServerSteamAPIActivated>(OnGameServerSteamAPIActivated);

        // Periodic check for updates
        AddTimer(DefaultUpdateCheckIntervalSeconds, CheckServerVersionTimer, TimerFlags.REPEAT);
    }

    private void OnGameServerSteamAPIActivated()
    {
        Logger.LogInformation("[MatchZySafeAutoUpdater] Steam API activated. MatchZy-safe update checks enabled.");
        _offlineWarningLogged = false;
    }

    /// <summary>
    /// Timer callback that kicks off an async update check.
    /// </summary>
    private void CheckServerVersionTimer()
    {
        try
        {
            if (!safeAutoUpdaterEnabled.Value)
            {
                return;
            }

            // In warn-only mode, once we've detected an update we stop polling until restart.
            // This avoids hammering Steam continuously while waiting for an external server
            // manager/admin to perform the update.
            try
            {
                string action = (safeAutoUpdaterAction.Value ?? "warn_only").Trim().ToLowerInvariant();
                if (action != "restart" && _updateAvailable)
                {
                    return;
                }
            }
            catch
            {
                // best-effort
            }

            // Never perform update checks while a MatchZy match is in progress; this keeps
            // all Steam API polling and restart decisions strictly outside live matches.
            string status = GetMatchZyStatus();
            if (IsMatchInProgress(status))
            {
                return;
            }

            if (_restartRequired)
            {
                // Already committed to restarting; no need to keep hammering the Steam API.
                return;
            }

            // Backoff window (offline/DNS failures).
            if (Server.CurrentTime < _nextUpdateCheckAllowedTime)
            {
                return;
            }

            _ = CheckServerVersionAndMaybeScheduleShutdownAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError("[MatchZySafeAutoUpdater] Error scheduling update check: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Performs the actual Steam UpToDateCheck and, if an update is available, schedules a
    /// shutdown that respects MatchZy's tournament status.
    /// </summary>
    private async Task CheckServerVersionAndMaybeScheduleShutdownAsync()
    {
        try
        {
            bool hasUpdate = await IsUpdateAvailableAsync();
            if (!hasUpdate)
            {
                return;
            }

            Server.NextFrame(ManageServerUpdate);
        }
        catch (Exception ex) when (IsTransientNetworkOrDnsFailure(ex))
        {
            ApplyOfflineBackoff(ex);
        }
        catch (Exception ex)
        {
            Logger.LogError("[MatchZySafeAutoUpdater] Error while checking for updates: {Message}", ex.Message);
        }
    }

    private void ManageServerUpdate()
    {
        if (!_updateAvailable)
        {
            _updateFoundTime = Server.CurrentTime;
            _updateAvailable = true;

            // Log a clear, machine-parseable marker for external server managers.
            // Your manager can watch for this exact string:
            //   [MATCHZY_UPDATE_AVAILABLE] required_version=<number>
            Logger.LogInformation("[MatchZySafeAutoUpdater] New CS2 update released (Required version: {Version})", _requiredVersion);
            Logger.LogInformation("[MATCHZY_UPDATE_AVAILABLE] required_version={Version}", _requiredVersion);

            // Notify remote API (if configured) so admins get a clear warning.
            try
            {
                if (!string.IsNullOrEmpty(matchConfig.RemoteLogURL) && !string.IsNullOrEmpty(matchReportServerId.Value))
                {
                    var ev = new MatchZyCs2UpdateRequiredEvent
                    {
                        MatchId = -1,
                        ServerId = matchReportServerId.Value,
                        RequiredVersion = _requiredVersion,
                        Phase = "available",
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    };
                    Task.Run(async () => { await SendEventAsync(ev); });
                }
            }
            catch
            {
                // Best effort; never block shutdown logic on webhook failures.
            }
        }

        string action = "warn_only";
        try
        {
            action = (safeAutoUpdaterAction.Value ?? "warn_only").Trim().ToLowerInvariant();
        }
        catch
        {
            action = "warn_only";
        }

        if (action != "restart")
        {
            // Default for MAT setups: do not quit the server automatically.
            return;
        }

        _restartRequired = true;

        // Try to shut down, but respect MatchZy’s status.
        TryShutdownRespectingMatchZy();
    }

    /// <summary>
    /// Attempts to shut down the server. If MatchZy reports a live/active match,
    /// we defer and reschedule instead of quitting.
    /// </summary>
    private void TryShutdownRespectingMatchZy()
    {
        if (!_restartRequired)
        {
            return;
        }

        string status = GetMatchZyStatus();
        string matchSlug = GetMatchZyMatchSlug();

        if (IsMatchInProgress(status))
        {
            Logger.LogInformation(
                "[MatchZySafeAutoUpdater] Update available (version {Version}), but MatchZy status is '{Status}' for match '{MatchSlug}'. Deferring shutdown.",
                _requiredVersion, status, string.IsNullOrEmpty(matchSlug) ? "<none>" : matchSlug
            );

            // Reschedule another check after a delay; we keep doing this until status is safe.
            AddTimer(ShutdownRetryDelaySeconds, TryShutdownRespectingMatchZy, TimerFlags.STOP_ON_MAPCHANGE);
            return;
        }

        Logger.LogInformation(
            "[MatchZySafeAutoUpdater] MatchZy status is '{Status}' (safe). Preparing server shutdown for CS2 update {Version}.",
            status, _requiredVersion
        );

        PrepareServerShutdown();
    }

    private string GetMatchZyStatus()
    {
        try
        {
            return _matchzyTournamentStatus?.GetPrimitiveValue<string>() ?? "idle";
        }
        catch
        {
            return "idle";
        }
    }

    private string GetMatchZyMatchSlug()
    {
        try
        {
            return _matchzyTournamentMatch?.GetPrimitiveValue<string>() ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Treat these MatchZy statuses as "match in progress" and never restart during them.
    /// </summary>
    private static bool IsMatchInProgress(string status)
    {
        if (string.IsNullOrEmpty(status))
        {
            return false;
        }

        status = status.ToLowerInvariant();
        return status is "loading"
                     or "warmup"
                     or "knife"
                     or "live"
                     or "playing"
                     or "paused"
                     or "halftime";
        // "postgame", "idle", "error" are considered safe to restart.
    }

    /// <summary>
    /// Kicks all human players and then runs "quit".
    /// </summary>
    private void PrepareServerShutdown()
    {
        if (!IsServerSafeToShutdownNow())
        {
            Logger.LogWarning("[MatchZySafeAutoUpdater] Shutdown aborted: MatchZy internal state is not safe yet. Will retry.");
            AddTimer(ShutdownRetryDelaySeconds, TryShutdownRespectingMatchZy, TimerFlags.STOP_ON_MAPCHANGE);
            return;
        }

        var players = Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false })
            .ToList();

        foreach (var player in players)
        {
            try
            {
                switch (player.Connected)
                {
                    case PlayerConnectedState.PlayerConnected:
                    case PlayerConnectedState.PlayerConnecting:
                    case PlayerConnectedState.PlayerReconnecting:
                        Server.ExecuteCommand(
                            $"kickid {player.UserId} Due to the game update (Version: {_requiredVersion}), the server is now restarting.");
                        break;
                }
            }
            catch
            {
                // Best effort; ignore failures for individual players.
            }
        }

        AddTimer(1.0f, ShutdownServer);
    }

    private void ShutdownServer()
    {
        if (!IsServerSafeToShutdownNow())
        {
            Logger.LogWarning("[MatchZySafeAutoUpdater] Final shutdown step aborted: server no longer in a safe state. Deferring.");
            AddTimer(ShutdownRetryDelaySeconds, TryShutdownRespectingMatchZy, TimerFlags.STOP_ON_MAPCHANGE);
            return;
        }

        // Second machine-parseable marker indicating that we are actually quitting now:
        //   [MATCHZY_UPDATE_SHUTDOWN] required_version=<number>
        Logger.LogInformation("[MatchZySafeAutoUpdater] Initiating server shutdown for CS2 update {Version}.", _requiredVersion);
        Logger.LogInformation("[MATCHZY_UPDATE_SHUTDOWN] required_version={Version}", _requiredVersion);

        // Notify remote API (if configured) that shutdown is imminent.
        try
        {
            if (!string.IsNullOrEmpty(matchConfig.RemoteLogURL) && !string.IsNullOrEmpty(matchReportServerId.Value))
            {
                var ev = new MatchZyCs2UpdateRequiredEvent
                {
                    MatchId = -1,
                    ServerId = matchReportServerId.Value,
                    RequiredVersion = _requiredVersion,
                    Phase = "shutdown",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                };
                Task.Run(async () => { await SendEventAsync(ev); });
            }
        }
        catch
        {
            // Best effort only.
        }
        Server.ExecuteCommand("quit");
    }

    private bool IsServerSafeToShutdownNow()
    {
        try
        {
            string status = GetMatchZyStatus();
            if (IsMatchInProgress(status))
            {
                return false;
            }

            if (isMatchSetup || readyAvailable || isWarmup || isKnifeRound || isMatchLive || matchStarted || isPaused)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Console command: manually check if the server is up to date and print the result.
    /// Does NOT schedule a restart; purely informational.
    /// </summary>
    [ConsoleCommand("matchzy_check_for_updates", "Check whether this CS2 server is up to date according to Steam.")]
    public void MatchZyCheckForUpdates(CCSPlayerController? player, CommandInfo command)
    {
        // Run the check on the next frame to keep the command handler light.
        Server.NextFrame(async () =>
        {
            string prefix = "[MatchZyUpToDate]";

            try
            {
                if (!safeAutoUpdaterEnabled.Value)
                {
                    string disabled = $"{prefix} Update checks are disabled (matchzy_safeautoupdater_enabled 0).";
                    if (player != null && player.IsValid)
                    {
                        player.PrintToChat($" {disabled}");
                    }
                    else
                    {
                        Logger.LogInformation(disabled);
                    }
                    return;
                }

                (bool upToDate, int requiredVersion) = await GetUpdateStatusAsync();

                string msg = upToDate
                    ? $"{prefix} Server is up to date."
                    : $"{prefix} Update available. Required version: {requiredVersion}. The auto-updater will restart once MatchZy is idle/postgame.";

                if (player != null && player.IsValid)
                {
                    player.PrintToChat($" {msg}");
                }
                else
                {
                    Logger.LogInformation(msg);
                }
            }
            catch (Exception ex) when (IsTransientNetworkOrDnsFailure(ex))
            {
                // Avoid printing a scary "error" in offline environments.
                string err = $"{prefix} Failed to check for updates (DNS/network/offline): {ex.Message}";
                if (player != null && player.IsValid)
                {
                    player.PrintToChat($" {err}");
                }
                else
                {
                    Logger.LogWarning(err);
                }
            }
            catch (Exception ex)
            {
                string err = $"{prefix} Failed to check for updates: {ex.Message}";
                if (player != null && player.IsValid)
                {
                    player.PrintToChat($" {err}");
                }
                else
                {
                    Logger.LogError(err);
                }
            }
        });
    }

    /// <summary>
    /// Returns (upToDate, requiredVersion). Does NOT mutate restart state.
    /// </summary>
    private async Task<(bool upToDate, int requiredVersion)> GetUpdateStatusAsync()
    {
        string steamInfPatchVersion = await GetSteamInfPatchVersionAsync();

        if (string.IsNullOrWhiteSpace(steamInfPatchVersion))
        {
            throw new InvalidOperationException("steam.inf patch version could not be determined.");
        }

        using HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        var response = await httpClient.GetAsync(string.Format(SteamApiEndpoint, steamInfPatchVersion));

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Steam UpToDateCheck request failed with status {response.StatusCode}.");
        }

        var upToDateCheckResponse = await response.Content.ReadFromJsonAsync<UpToDateCheckResponse>();

        if (upToDateCheckResponse?.Response is not { Success: true } resp)
        {
            // If Steam says Success=false, treat it as "cannot determine".
            throw new InvalidOperationException("Steam UpToDateCheck did not return a successful response.");
        }

        return (resp.UpToDate, resp.RequiredVersion);
    }

    /// <summary>
    /// Checks for an update and updates internal state if one is found.
    /// </summary>
    private async Task<bool> IsUpdateAvailableAsync()
    {
        (bool upToDate, int requiredVersion) = await GetUpdateStatusAsync();

        if (upToDate)
        {
            return false;
        }

        _requiredVersion = requiredVersion;
        return true;
    }

    private async Task<string> GetSteamInfPatchVersionAsync()
    {
        string steamInfPath = Path.Combine(Server.GameDirectory, "csgo", "steam.inf");

        if (!File.Exists(steamInfPath))
        {
            Logger.LogError("[MatchZySafeAutoUpdater] steam.inf not found at {Path}.", steamInfPath);
            return string.Empty;
        }

        try
        {
            string steamInfContents = await File.ReadAllTextAsync(steamInfPath);
            Match match = PatchVersionRegex().Match(steamInfContents);

            if (match.Success)
            {
                return match.Groups["version"].Value;
            }

            Logger.LogError("[MatchZySafeAutoUpdater] Could not find PatchVersion key in {Path}.", steamInfPath);
            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogError("[MatchZySafeAutoUpdater] Error reading steam.inf: {Message}", ex.Message);
            return string.Empty;
        }
    }

    [GeneratedRegex(@"PatchVersion=(?<version>[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex PatchVersionRegex();

    private void ApplyOfflineBackoff(Exception ex)
    {
        int backoffSeconds = Math.Max(30, safeAutoUpdaterOfflineBackoffSeconds.Value);
        _nextUpdateCheckAllowedTime = Server.CurrentTime + backoffSeconds;

        // Log only once per "offline period" to avoid spamming server consoles.
        if (!_offlineWarningLogged)
        {
            _offlineWarningLogged = true;
            Logger.LogWarning(
                "[MatchZySafeAutoUpdater] Steam update check failed (DNS/network/offline): {Message}. Backing off for {BackoffSeconds}s. (Disable with matchzy_safeautoupdater_enabled 0)",
                ex.Message,
                backoffSeconds
            );
        }
    }

    private static bool IsTransientNetworkOrDnsFailure(Exception ex)
    {
        // HttpClient failures typically arrive as HttpRequestException with an inner SocketException.
        // In many server environments, DNS is intentionally unavailable or outbound traffic is blocked.
        if (ex is HttpRequestException hre)
        {
            if (hre.InnerException is SocketException)
            {
                return true;
            }

            // Sometimes the SocketException is nested deeper.
            Exception? inner = hre.InnerException;
            while (inner != null)
            {
                if (inner is SocketException)
                {
                    return true;
                }
                inner = inner.InnerException;
            }
        }

        // Timeout / cancellation should also be treated as transient network failure.
        return ex is TaskCanceledException;
    }
}

// --- Steam UpToDateCheck DTOs ---

public sealed class UpToDateCheckResponse
{
    [JsonPropertyName("response")]
    public UpToDateCheckInnerResponse? Response { get; set; }
}

public sealed class UpToDateCheckInnerResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("up_to_date")]
    public bool UpToDate { get; set; }

    [JsonPropertyName("required_version")]
    public int RequiredVersion { get; set; }
}

