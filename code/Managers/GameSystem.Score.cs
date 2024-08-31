using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Services;
using Sandbox.Utility;

namespace Vidya;


public struct Score
{
    public long Rank { get; set; }
    public string Name { get; set; }
    public double Level { get; set; }
    public bool Me { get; set; }
	public long SteamId { get; set; }
}


public partial class GameSystem : Component
{

    public static bool ShowLeaderboard { get; set; } = true;

    public static List<Score> Scores { get; set; } = new();
    public string LeaderboardStat { get; set; } = "lb_v1_stat";


    public async Task GetScores()
    {
        var board = Leaderboards.GetFromStat( LeaderboardStat );
		board.CenterOnMe();
        board.MaxEntries = 15;

        await board.Refresh();

        // Populate a list of scores from entries.
        Scores = new List<Score>();

        foreach ( var e in board.Entries )
        {
            // Set the medal from the time.
            var score = new Score
            {
                Rank = e.Rank,
                Name = e.DisplayName,
                Level = (float)e.Value,
                Me = e.SteamId == (long)Steam.SteamId,
				SteamId = e.SteamId
            };

            Scores.Add( score );
        }
    }

    public async void SendScore()
    {
		if ( StartServer )
			return;

        Stats.SetValue( LeaderboardStat, Level );
        await Stats.FlushAndWaitAsync();
        await GetScores();
    }

}
