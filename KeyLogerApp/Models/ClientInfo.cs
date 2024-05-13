using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace KeyLogerApp.Models
{
    public class ClientInfo
    {
        public TcpClient TcpClient { get; set; }
        public int ClientId { get; set; }
        public int CommandCount { get; set; }
        public int TotalBlackMarkers { get; set; }
        public int TotalWhiteMarkers { get; set; }
        public DateTime LastActivity { get; set; }
        public DateTime? CorrectAnswerTime { get; set; }

        public ClientInfo(TcpClient client, int clientId)
        {
            TcpClient = client;
            ClientId = clientId;
            CommandCount = 0;
            TotalBlackMarkers = 0;
            TotalWhiteMarkers = 0;
        }

        public void AddMarkers(int blackMarkers, int whiteMarkers)
        {
            TotalBlackMarkers += blackMarkers;
            TotalWhiteMarkers += whiteMarkers;
            CommandCount++;  // Each call represents a command
        }
    }



}
