using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;


namespace MatchZy
{
    public partial class MatchZy
    {
        public async Task SendEventAsync(MatchZyEvent @event)
        {
            try
            {
                if (string.IsNullOrEmpty(matchConfig.RemoteLogURL)) return;

                Log($"[SendEventAsync] Sending Event: {@event.EventName} for matchId: {liveMatchId} mapNumber: {matchConfig.CurrentMapNumber} on {matchConfig.RemoteLogURL}");
                
                // Print to server console for visibility
                Server.NextFrame(() => {
                    Server.PrintToConsole($"[MatchZy Events] Sending '{@event.EventName}' to {matchConfig.RemoteLogURL}");
                });

                using var httpClient = new HttpClient();
                using var jsonContent = new StringContent(JsonSerializer.Serialize(@event, @event.GetType()), Encoding.UTF8, "application/json");

                string jsonString = await jsonContent.ReadAsStringAsync();

                Log($"[SendEventAsync] SENDING DATA: {jsonString}");

                if (!string.IsNullOrEmpty(matchConfig.RemoteLogHeaderKey) && !string.IsNullOrEmpty(matchConfig.RemoteLogHeaderValue))
                {
                    httpClient.DefaultRequestHeaders.Add(matchConfig.RemoteLogHeaderKey, matchConfig.RemoteLogHeaderValue);
                }

                var httpResponseMessage = await httpClient.PostAsync(matchConfig.RemoteLogURL, jsonContent);

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    Log($"[SendEventAsync] Sending {@event.EventName} for matchId: {liveMatchId} mapNumber: {matchConfig.CurrentMapNumber} successful with status code: {httpResponseMessage.StatusCode}");
                    
                    // Print success to console
                    Server.NextFrame(() => {
                        Server.PrintToConsole($"[MatchZy Events] ✓ '{@event.EventName}' sent successfully ({httpResponseMessage.StatusCode})");
                    });
                }
                else
                {
                    string errorContent = await httpResponseMessage.Content.ReadAsStringAsync();
                    Log($"[SendEventAsync] Sending {@event.EventName} for matchId: {liveMatchId} mapNumber: {matchConfig.CurrentMapNumber} failed with status code: {httpResponseMessage.StatusCode}, ResponseContent: {errorContent}");
                    
                    // Print error to console
                    Server.NextFrame(() => {
                        Server.PrintToConsole($"[MatchZy Events] ✗ FAILED to send '{@event.EventName}' (HTTP {httpResponseMessage.StatusCode})");
                        Server.PrintToConsole($"[MatchZy Events] Error: {errorContent}");
                    });
                }
            }
            catch (Exception e)
            {
                Log($"[SendEventAsync FATAL] An error occurred: {e.Message}");
                
                // Print exception to console
                Server.NextFrame(() => {
                    Server.PrintToConsole($"[MatchZy Events] ✗ EXCEPTION sending '{@event.EventName}': {e.Message}");
                });
            }
        }
    }
}
