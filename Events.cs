using System.Text.Json.Serialization;

namespace MatchZy;
public class MatchZyEvent
{
    public MatchZyEvent(string eventName)
    {
        EventName = eventName;
    }

    [JsonPropertyName("event")]
    public string EventName { get; }
}

public class MatchZyMatchEvent : MatchZyEvent
{
    [JsonPropertyName("matchid")]
    public required long MatchId { get; init; }

    protected MatchZyMatchEvent(string eventName) : base(eventName)
    {
    }
}

public class MatchZyMatchTeamEvent : MatchZyMatchEvent
{
    [JsonPropertyName("team")]
    public required string Team { get; init; }

    protected MatchZyMatchTeamEvent(string eventName) : base(eventName)
    {
    }
}

public class MatchZyMapEvent : MatchZyMatchEvent
{
    [JsonPropertyName("map_number")]
    public required int MapNumber { get; init; }

    protected MatchZyMapEvent(string eventName) : base(eventName)
    {
    }
}

public class MatchZyMapTeamEvent : MatchZyMapEvent
{
    [JsonPropertyName("team_int")]
    public required int TeamNumber { get; init; }

    protected MatchZyMapTeamEvent(string eventName) : base(eventName)
    {
    }
}

public class MatchZyRoundEvent : MatchZyMapEvent
{
    [JsonPropertyName("round_number")]
    public required int RoundNumber { get; init; }

    protected MatchZyRoundEvent(string eventName) : base(eventName)
    {
    }
}

public class MatchZyTimedRoundEvent : MatchZyRoundEvent
{
    [JsonPropertyName("round_time")]
    public required int RoundTime { get; init; }

    protected MatchZyTimedRoundEvent(string eventName) : base(eventName)
    {
    }
}

public class MatchZyPlayerRoundEvent : MatchZyRoundEvent
{

    [JsonPropertyName("player")]
    public required int Player { get; init; }

    protected MatchZyPlayerRoundEvent(string eventName) : base(eventName)
    {
    }
}

public class MatchZyPlayerTimedRoundEvent : MatchZyTimedRoundEvent
{
    [JsonPropertyName("player")]
    public required int Player { get; init; }

    protected MatchZyPlayerTimedRoundEvent(string eventName) : base(eventName)
    {
    }
}

public class MatchZyPlayerConnectedEvent : MatchZyMatchEvent
{
    [JsonPropertyName("player")]
    public required MatchZyPlayerInfo Player { get; init; }

    public MatchZyPlayerConnectedEvent() : base("player_connect")
    {
    }
}

public class MatchZyPlayerDisconnectedEvent : MatchZyMatchEvent
{
    [JsonPropertyName("player")]
    public required MatchZyPlayerInfo Player { get; init; }

    public MatchZyPlayerDisconnectedEvent() : base("player_disconnect")
    {
    }
}

public class MatchZyBackupLoadedEvent : MatchZyMapEvent
{
    [JsonPropertyName("round_number")]
    public required int RoundNumber { get; init; }

    [JsonPropertyName("filename")]
    public required string FileName { get; init; }

    public MatchZyBackupLoadedEvent() : base("backup_loaded")
    {
    }
}

public class MatchZySeriesStartedEvent : MatchZyMatchEvent
{
    [JsonPropertyName("team1")]
    public required MatchZyTeamWrapper Team1 { get; init; }

    [JsonPropertyName("team2")]
    public required MatchZyTeamWrapper Team2 { get; init; }

    [JsonPropertyName("num_maps")]
    public required int NumberOfMaps { get; init; }

    public MatchZySeriesStartedEvent() : base("series_start")
    {
    }
}

public class MatchZySeriesResultEvent : MatchZyMatchEvent
{
    [JsonPropertyName("time_until_restore")]
    public required int TimeUntilRestore { get; init; }

    [JsonPropertyName("winner")]
    public required Winner Winner { get; init; }

    [JsonPropertyName("team1_series_score")]
    public required int Team1SeriesScore { get; init; }

    [JsonPropertyName("team2_series_score")]
    public required int Team2SeriesScore { get; init; }

    public MatchZySeriesResultEvent() : base("series_end")
    {
    }
}

public class GoingLiveEvent : MatchZyMapEvent
{
    public GoingLiveEvent() : base("going_live")
    {
    }
}

public class MatchZyRoundEndedEvent : MatchZyTimedRoundEvent
{

    [JsonPropertyName("reason")]
    public required int Reason { get; init; }

    [JsonPropertyName("winner")]
    public required Winner Winner { get; init; }

    [JsonPropertyName("team1")]
    public required MatchZyStatsTeam StatsTeam1 { get; init; }

    [JsonPropertyName("team2")]
    public required MatchZyStatsTeam StatsTeam2 { get; init; }

    public MatchZyRoundEndedEvent() : base("round_end")
    {
    }
}

public class MapResultEvent : MatchZyMapEvent
{
    [JsonPropertyName("winner")]
    public required Winner Winner { get; init; }

    [JsonPropertyName("team1")]
    public required MatchZyStatsTeam StatsTeam1 { get; init; }

    [JsonPropertyName("team2")]
    public required MatchZyStatsTeam StatsTeam2 { get; init; }

    public MapResultEvent() : base("map_result")
    {
    }
}

public class MatchZyMapSelectionEvent : MatchZyMatchTeamEvent
{
    [JsonPropertyName("map_name")]
    public required string MapName { get; init; }

    protected MatchZyMapSelectionEvent(string eventName) : base(eventName)
    {
    }
}

public class MatchZyMapPickedEvent : MatchZyMapSelectionEvent
{
    [JsonPropertyName("map_number")]
    public required int MapNumber { get; init; }

    public MatchZyMapPickedEvent() : base("map_picked")
    {
    }
}

public class MatchZyMapVetoedEvent : MatchZyMapSelectionEvent
{
    public MatchZyMapVetoedEvent() : base("map_vetoed")
    {
    }
}

public class MatchZySidePickedEvent : MatchZyMapSelectionEvent
{
    [JsonPropertyName("map_number")]
    public required int MapNumber { get; init; }

    [JsonPropertyName("side")]
    public required string Side { get; init; }

    public MatchZySidePickedEvent() : base("side_picked")
    {
    }
}

public class MatchZyDemoUploadedEvent : MatchZyMatchEvent
{
    [JsonPropertyName("map_number")]
    public required int MapNumber { get; init; }

    [JsonPropertyName("filename")]
    public required string FileName { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    public MatchZyDemoUploadedEvent() : base("demo_upload_ended")
    {
    }
}

// Player Ready System Events
public class MatchZyPlayerReadyEvent : MatchZyMatchEvent
{
    [JsonPropertyName("player")]
    public required MatchZyPlayerInfo Player { get; init; }

    [JsonPropertyName("team")]
    public required string Team { get; init; }

    [JsonPropertyName("ready_count_team1")]
    public required int ReadyCountTeam1 { get; init; }

    [JsonPropertyName("ready_count_team2")]
    public required int ReadyCountTeam2 { get; init; }

    [JsonPropertyName("total_ready")]
    public required int TotalReady { get; init; }

    [JsonPropertyName("expected_total")]
    public required int ExpectedTotal { get; init; }

    public MatchZyPlayerReadyEvent() : base("player_ready")
    {
    }
}

public class MatchZyPlayerUnreadyEvent : MatchZyMatchEvent
{
    [JsonPropertyName("player")]
    public required MatchZyPlayerInfo Player { get; init; }

    [JsonPropertyName("team")]
    public required string Team { get; init; }

    [JsonPropertyName("ready_count_team1")]
    public required int ReadyCountTeam1 { get; init; }

    [JsonPropertyName("ready_count_team2")]
    public required int ReadyCountTeam2 { get; init; }

    [JsonPropertyName("total_ready")]
    public required int TotalReady { get; init; }

    [JsonPropertyName("expected_total")]
    public required int ExpectedTotal { get; init; }

    public MatchZyPlayerUnreadyEvent() : base("player_unready")
    {
    }
}

public class MatchZyTeamReadyEvent : MatchZyMatchEvent
{
    [JsonPropertyName("team")]
    public required string Team { get; init; }

    [JsonPropertyName("ready_count")]
    public required int ReadyCount { get; init; }

    [JsonPropertyName("total_ready")]
    public required int TotalReady { get; init; }

    [JsonPropertyName("expected_total")]
    public required int ExpectedTotal { get; init; }

    public MatchZyTeamReadyEvent() : base("team_ready")
    {
    }
}

public class MatchZyAllPlayersReadyEvent : MatchZyMatchEvent
{
    [JsonPropertyName("ready_count_team1")]
    public required int ReadyCountTeam1 { get; init; }

    [JsonPropertyName("ready_count_team2")]
    public required int ReadyCountTeam2 { get; init; }

    [JsonPropertyName("total_ready")]
    public required int TotalReady { get; init; }

    [JsonPropertyName("countdown_started")]
    public required bool CountdownStarted { get; init; }

    public MatchZyAllPlayersReadyEvent() : base("all_players_ready")
    {
    }
}

// Match Phase Change Events
public class MatchZyWarmupEndedEvent : MatchZyMapEvent
{
    public MatchZyWarmupEndedEvent() : base("warmup_ended")
    {
    }
}

public class MatchZyKnifeRoundStartedEvent : MatchZyMapEvent
{
    public MatchZyKnifeRoundStartedEvent() : base("knife_round_started")
    {
    }
}

public class MatchZyKnifeRoundEndedEvent : MatchZyMapEvent
{
    [JsonPropertyName("winner")]
    public required string Winner { get; init; }

    public MatchZyKnifeRoundEndedEvent() : base("knife_round_ended")
    {
    }
}

// Pause System Events
public class MatchZyPauseRequestedEvent : MatchZyMapEvent
{
    [JsonPropertyName("requested_by")]
    public required MatchZyPlayerInfo RequestedBy { get; init; }

    [JsonPropertyName("is_tactical")]
    public required bool IsTactical { get; init; }

    [JsonPropertyName("is_admin")]
    public required bool IsAdmin { get; init; }

    public MatchZyPauseRequestedEvent() : base("pause_requested")
    {
    }
}

public class MatchZyMatchPausedEvent : MatchZyMapEvent
{
    [JsonPropertyName("paused_by")]
    public required MatchZyPlayerInfo PausedBy { get; init; }

    [JsonPropertyName("is_tactical")]
    public required bool IsTactical { get; init; }

    [JsonPropertyName("is_admin")]
    public required bool IsAdmin { get; init; }

    [JsonPropertyName("pause_time")]
    public required long PauseTime { get; init; }

    public MatchZyMatchPausedEvent() : base("match_paused")
    {
    }
}

public class MatchZyUnpauseRequestedEvent : MatchZyMapEvent
{
    [JsonPropertyName("team")]
    public required string Team { get; init; }

    [JsonPropertyName("teams_ready")]
    public required int TeamsReady { get; init; }

    [JsonPropertyName("teams_needed")]
    public required int TeamsNeeded { get; init; }

    public MatchZyUnpauseRequestedEvent() : base("unpause_requested")
    {
    }
}

public class MatchZyMatchUnpausedEvent : MatchZyMapEvent
{
    [JsonPropertyName("pause_duration")]
    public required long PauseDuration { get; init; }

    public MatchZyMatchUnpausedEvent() : base("match_unpaused")
    {
    }
}

// Round and Game State Events
public class MatchZyRoundStartedEvent : MatchZyRoundEvent
{
    [JsonPropertyName("team1_score")]
    public required int Team1Score { get; init; }

    [JsonPropertyName("team2_score")]
    public required int Team2Score { get; init; }

    public MatchZyRoundStartedEvent() : base("round_started")
    {
    }
}

public class MatchZyHalftimeStartedEvent : MatchZyMapEvent
{
    [JsonPropertyName("team1_score")]
    public required int Team1Score { get; init; }

    [JsonPropertyName("team2_score")]
    public required int Team2Score { get; init; }

    public MatchZyHalftimeStartedEvent() : base("halftime_started")
    {
    }
}

public class MatchZyOvertimeStartedEvent : MatchZyMapEvent
{
    [JsonPropertyName("overtime_number")]
    public required int OvertimeNumber { get; init; }

    public MatchZyOvertimeStartedEvent() : base("overtime_started")
    {
    }
}

public class MatchZySideSwapEvent : MatchZyMapEvent
{
    [JsonPropertyName("team1_side")]
    public required string Team1Side { get; init; }

    [JsonPropertyName("team2_side")]
    public required string Team2Side { get; init; }

    public MatchZySideSwapEvent() : base("side_swap")
    {
    }
}

// Test Event
public class MatchZyTestEvent : MatchZyMatchEvent
{
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("timestamp")]
    public required long Timestamp { get; init; }

    [JsonPropertyName("triggered_by")]
    public required string TriggeredBy { get; init; }

    public MatchZyTestEvent() : base("test_event")
    {
    }
}