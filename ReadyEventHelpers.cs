using CounterStrikeSharp.API.Core;

namespace MatchZy;

public partial class MatchZy
{
    private void SendPlayerReadyEvent(CCSPlayerController player, bool isReady)
    {
        Log($"[SendPlayerReadyEvent] Called - isMatchSetup: {isMatchSetup}, RemoteLogURL: {matchConfig.RemoteLogURL}, isReady: {isReady}");
        
        if (!isMatchSetup)
        {
            Log($"[SendPlayerReadyEvent] Skipping - Match not setup");
            return;
        }
        
        if (string.IsNullOrEmpty(matchConfig.RemoteLogURL))
        {
            Log($"[SendPlayerReadyEvent] Skipping - RemoteLogURL not configured");
            return;
        }
        
        if (!player.UserId.HasValue)
        {
            Log($"[SendPlayerReadyEvent] Skipping - Player UserId is null");
            return;
        }

        // Get ready counts
        (int team1PlayerCount, int team1ReadyCount) = GetTeamPlayerCount((int)CounterStrikeSharp.API.Modules.Utils.CsTeam.CounterTerrorist, false);
        (int team2PlayerCount, int team2ReadyCount) = GetTeamPlayerCount((int)CounterStrikeSharp.API.Modules.Utils.CsTeam.Terrorist, false);

        // Expected total is players per team * 2
        int expectedTotal = matchConfig.PlayersPerTeam * 2;

        // Determine which team this player is on
        string teamName = "none";
        if (reverseTeamSides.ContainsKey("CT") && player.TeamNum == 3)
        {
            teamName = reverseTeamSides["CT"].teamName;
        }
        else if (reverseTeamSides.ContainsKey("TERRORIST") && player.TeamNum == 2)
        {
            teamName = reverseTeamSides["TERRORIST"].teamName;
        }

        var playerInfo = new MatchZyPlayerInfo(
            player.SteamID.ToString(),
            player.PlayerName,
            teamName
        );

        if (isReady)
        {
            Log($"[SendPlayerReadyEvent] Creating player_ready event for {player.PlayerName}");
            
            var readyEvent = new MatchZyPlayerReadyEvent
            {
                MatchId = liveMatchId,
                Player = playerInfo,
                Team = teamName,
                ReadyCountTeam1 = reverseTeamSides.ContainsKey("CT") && reverseTeamSides["CT"] == matchzyTeam1 ? team1ReadyCount : team2ReadyCount,
                ReadyCountTeam2 = reverseTeamSides.ContainsKey("CT") && reverseTeamSides["CT"] == matchzyTeam2 ? team1ReadyCount : team2ReadyCount,
                TotalReady = team1ReadyCount + team2ReadyCount,
                ExpectedTotal = expectedTotal
            };

            Log($"[SendPlayerReadyEvent] Sending player_ready event to remote URL");
            Task.Run(async () => {
                await SendEventAsync(readyEvent);
            });

            // Check if team is now ready
            CheckAndSendTeamReadyEvent();
        }
        else
        {
            Log($"[SendPlayerReadyEvent] Creating player_unready event for {player.PlayerName}");
            
            var unreadyEvent = new MatchZyPlayerUnreadyEvent
            {
                MatchId = liveMatchId,
                Player = playerInfo,
                Team = teamName,
                ReadyCountTeam1 = reverseTeamSides.ContainsKey("CT") && reverseTeamSides["CT"] == matchzyTeam1 ? team1ReadyCount : team2ReadyCount,
                ReadyCountTeam2 = reverseTeamSides.ContainsKey("CT") && reverseTeamSides["CT"] == matchzyTeam2 ? team1ReadyCount : team2ReadyCount,
                TotalReady = team1ReadyCount + team2ReadyCount,
                ExpectedTotal = expectedTotal
            };

            Log($"[SendPlayerReadyEvent] Sending player_unready event to remote URL");
            Task.Run(async () => {
                await SendEventAsync(unreadyEvent);
            });
        }
    }

    private void CheckAndSendTeamReadyEvent()
    {
        Log($"[CheckAndSendTeamReadyEvent] Called - isMatchSetup: {isMatchSetup}, RemoteLogURL configured: {!string.IsNullOrEmpty(matchConfig.RemoteLogURL)}");
        
        if (!isMatchSetup || string.IsNullOrEmpty(matchConfig.RemoteLogURL)) return;

        bool team1Ready = IsTeamReady((int)CounterStrikeSharp.API.Modules.Utils.CsTeam.CounterTerrorist);
        bool team2Ready = IsTeamReady((int)CounterStrikeSharp.API.Modules.Utils.CsTeam.Terrorist);

        (int team1PlayerCount, int team1ReadyCount) = GetTeamPlayerCount((int)CounterStrikeSharp.API.Modules.Utils.CsTeam.CounterTerrorist, false);
        (int team2PlayerCount, int team2ReadyCount) = GetTeamPlayerCount((int)CounterStrikeSharp.API.Modules.Utils.CsTeam.Terrorist, false);

        int expectedTotal = matchConfig.PlayersPerTeam * 2;
        int totalReady = team1ReadyCount + team2ReadyCount;

        // Send team_ready event for CT team if ready
        if (team1Ready && reverseTeamSides.ContainsKey("CT"))
        {
            Log($"[CheckAndSendTeamReadyEvent] CT team is ready, sending team_ready event");
            
            var teamReadyEvent = new MatchZyTeamReadyEvent
            {
                MatchId = liveMatchId,
                Team = reverseTeamSides["CT"] == matchzyTeam1 ? "team1" : "team2",
                ReadyCount = team1ReadyCount,
                TotalReady = totalReady,
                ExpectedTotal = expectedTotal
            };

            Task.Run(async () => {
                await SendEventAsync(teamReadyEvent);
            });
        }

        // Send team_ready event for T team if ready
        if (team2Ready && reverseTeamSides.ContainsKey("TERRORIST"))
        {
            Log($"[CheckAndSendTeamReadyEvent] T team is ready, sending team_ready event");
            
            var teamReadyEvent = new MatchZyTeamReadyEvent
            {
                MatchId = liveMatchId,
                Team = reverseTeamSides["TERRORIST"] == matchzyTeam1 ? "team1" : "team2",
                ReadyCount = team2ReadyCount,
                TotalReady = totalReady,
                ExpectedTotal = expectedTotal
            };

            Task.Run(async () => {
                await SendEventAsync(teamReadyEvent);
            });
        }

        // Send all_players_ready event if both teams are ready
        if (team1Ready && team2Ready)
        {
            Log($"[CheckAndSendTeamReadyEvent] Both teams ready, sending all_players_ready event");
            
            var allPlayersReadyEvent = new MatchZyAllPlayersReadyEvent
            {
                MatchId = liveMatchId,
                ReadyCountTeam1 = team1ReadyCount,
                ReadyCountTeam2 = team2ReadyCount,
                TotalReady = totalReady,
                CountdownStarted = true
            };

            Task.Run(async () => {
                await SendEventAsync(allPlayersReadyEvent);
            });
        }
    }
}

