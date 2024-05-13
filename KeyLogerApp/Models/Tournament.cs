using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyLogerApp.Models
{
    public class Tournament
    {
        public int TotalRounds { get; set; }
        public int CurrentRound { get; set; } = 1; // Начинаем с 1
        public Dictionary<int, int> PlayerScores { get; set; } = new Dictionary<int, int>();

        public Tournament(int totalRounds)
        {
            TotalRounds = totalRounds;
        }

        public void NextRound()
        {
            CurrentRound++;
        }

        public void UpdateScore(int clientId, int score)
        {
            if (PlayerScores.ContainsKey(clientId))
            {
                PlayerScores[clientId] += score;
            }
            else
            {
                PlayerScores[clientId] = score;
            }
        }
    }
}
