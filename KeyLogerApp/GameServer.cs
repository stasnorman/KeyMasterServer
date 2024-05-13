using System;
using System.Collections.Generic;  // Для хранения клиентов
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;
using Timer = System.Timers.Timer;
using System.Threading.Tasks;
using System.Linq;  // Для использования Linq
using KeyLogerApp.Models;
using System.Xml.Linq;
using System.Text.Json;
using System.Net.NetworkInformation;

namespace KeyLogerApp
{
    public class GameServer
    {
        private Tournament tournament;
        private Random random = new Random();
        DateTime restartTime = DateTime.Now.AddMinutes(10);
        private List<ClientInfo> connectedClients = new List<ClientInfo>();

        private bool acceptingReconnections = false; // Флаг для управления повторным подключением
        private string ipString { get; set; }
        private TcpListener listener { get; set; }
        private bool isRunning { get; set; }
        private string secretCode { get; set; }
        private Timer gameTimer { get; set; }
        private Timer countdownTimer { get; set; }
        private int timeLeft { get; set; }
        private int requiredClients { get; set; }
        private int nextClientId { get; set; } = 1;


        public GameServer(string ipAddress, int port, int clientCount, int totalRounds)  // Добавлен параметр totalRounds
        {
            ipString = ipAddress;
            listener = new TcpListener(IPAddress.Parse(ipAddress), port);
            requiredClients = clientCount;
            tournament = new Tournament(totalRounds);  // Инициализация турнира
            timeLeft = 30; // Установка времени таймера
            gameTimer = new Timer(timeLeft * 1000);
            gameTimer.Elapsed += OnTimeElapsed;
            gameTimer.AutoReset = false;
            countdownTimer = new Timer(1000);
            countdownTimer.Elapsed += OnCountdown;
            countdownTimer.AutoReset = true;
            secretCode = Convert.ToBase64String(Encoding.UTF8.GetBytes(GenerateSecretCode(5))); // Генерация и шифрование кода
        }


        private void OnTimeElapsed(object sender, ElapsedEventArgs e)
        {
            gameTimer.Stop();
            countdownTimer.Stop();
            SaveResultsToXml();
            Console.WriteLine("Время вышло. Результаты сохранены.");

            if (tournament.CurrentRound < tournament.TotalRounds)
            {
                tournament.NextRound();
                ResetGame();
                Console.WriteLine($"Начало раунда {tournament.CurrentRound} из {tournament.TotalRounds}.");
            }
            else
            {
                Console.WriteLine("Турнир окончен. Итоговые очки:");
                foreach (var score in tournament.PlayerScores)
                {
                    Console.WriteLine($"Игрок {score.Key}: {score.Value} очков");
                }
                isRunning = false; // Остановка сервера после завершения турнира
            }
        }


        private void OnCountdown(object sender, ElapsedEventArgs e)
        {
            if (timeLeft > 0)
            {
                timeLeft--;
                Console.WriteLine($"Time is over: {timeLeft / 60}:{timeLeft % 60:00}");
            }
            else
            {
                countdownTimer.Stop();
            }
        }

        private string GenerateSecretCode(int length)
        {
            string code = "";
            for (int i = 0; i < length; i++)
            {
                code += random.Next(500000).ToString();
            }
            return code;
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            var clientInfo = new ClientInfo(client, nextClientId++);
            connectedClients.Add(clientInfo);

            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    byte[] message = Encoding.ASCII.GetBytes($"Player ID: {clientInfo.ClientId}\n");
                    await stream.WriteAsync(message, 0, message.Length);
                    byte[] buffer = new byte[1024];
                    int bytes;

                    while ((bytes = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0 && isRunning)
                    {
                        string data = Encoding.ASCII.GetString(buffer, 0, bytes).Trim();
                        clientInfo.CommandCount++;
                        clientInfo.LastActivity = DateTime.Now;

                        if (data == secretCode)
                        {
                            clientInfo.CorrectAnswerTime = DateTime.Now;
                            byte[] response = Encoding.ASCII.GetBytes("Congratuation!.");
                            await stream.WriteAsync(response, 0, response.Length);
                            tournament.UpdateScore(clientInfo.ClientId, 100); // Например, добавление 100 очков за правильный ответ
                            break;
                        }
                        else
                        {
                            string feedback = AnalyzeInput(data, secretCode);
                            byte[] response = Encoding.ASCII.GetBytes($"Feedback: {feedback}\nTime is off: {timeLeft / 60} minutes {timeLeft % 60} sec.\n");
                            await stream.WriteAsync(response, 0, response.Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Client {clientInfo.ClientId} is disconected: {ex.Message}");
                }
                finally
                {
                    // Удаление клиента из списка активных подключений
                    connectedClients.Remove(clientInfo);
                    Console.WriteLine($"Client {clientInfo.ClientId} disconnected. Total connected clients: {connectedClients.Count}");
                    client.Close();
                }
            }
        }

        // Анализирует ввод пользователя и сравнивает его с секретным кодом
        private string AnalyzeInput(string userInput, string secretCode)
        {
            int blackMarkers = 0;
            int whiteMarkers = 0;
            var symbolMatches = new Dictionary<char, List<int>>();  // Словарь для совпадающих символов и их позиций
            var secretFrequency = new Dictionary<char, List<int>>();

            // Создаем словарь частот для секретного кода с позициями каждого символа
            for (int i = 0; i < secretCode.Length; i++)
            {
                char ch = secretCode[i];
                if (!secretFrequency.ContainsKey(ch))
                    secretFrequency[ch] = new List<int>();
                secretFrequency[ch].Add(i);
            }

            // Первый проход для черных маркеров
            for (int i = 0; i < Math.Min(userInput.Length, secretCode.Length); i++)
            {
                char userChar = userInput[i];
                if (userChar == secretCode[i])
                {
                    blackMarkers++;
                    secretFrequency[userChar].Remove(i);
                    if (!symbolMatches.ContainsKey(userChar))
                        symbolMatches[userChar] = new List<int>();
                    symbolMatches[userChar].Add(i);
                }
            }

            // Второй проход для белых маркеров
            for (int i = 0; i < userInput.Length; i++)
            {
                char userChar = userInput[i];
                if (userChar != secretCode[i] && secretFrequency.ContainsKey(userChar) && secretFrequency[userChar].Count > 0)
                {
                    whiteMarkers++;
                    int pos = secretFrequency[userChar][0];  // Берем первую доступную позицию
                    secretFrequency[userChar].RemoveAt(0);
                    if (!symbolMatches.ContainsKey(userChar))
                        symbolMatches[userChar] = new List<int>();
                    symbolMatches[userChar].Add(pos);
                }
            }

            var response = new
            {
                BlackMarkers = blackMarkers,
                WhiteMarkers = whiteMarkers,
                Matches = symbolMatches.Select(kvp => new { Character = kvp.Key, Positions = kvp.Value }).ToList()
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }


        public async Task StartAsync()
        {
            try
            {
                listener.Start();
                Console.WriteLine("Server started...");

                while (true)
                {
                    isRunning = true;
                    connectedClients.Clear();
                    nextClientId = 1;
                    acceptingReconnections = false;  // Изначально повторное подключение не разрешено
                    gameTimer.Stop();
                    countdownTimer.Stop();
                    secretCode = Convert.ToBase64String(Encoding.UTF8.GetBytes(GenerateSecretCode(5)));
                    Console.WriteLine($"New game started with secret code: {secretCode}");

                    await AcceptInitialClientsAsync(); // Принимаем начальное количество клиентов

                    // Запуск таймеров, когда все игроки подключены
                    gameTimer.Start();
                    countdownTimer.Start();
                    acceptingReconnections = true;  // Разрешаем повторные подключения после старта таймера

                    // Ожидание завершения игрового таймера
                    while (gameTimer.Enabled)
                    {
                        if (acceptingReconnections && connectedClients.Count < requiredClients)
                        {
                            Console.WriteLine("Reconnecting disconnected players...");
                            await AcceptClientsAsync();  // Переподключение клиентов
                        }
                        await Task.Delay(1000);
                    }


                    acceptingReconnections = false; // Отключаем повторные подключения после завершения таймера

                    SaveResultsToXml();
                    Console.WriteLine("Game round is over. Results saved.");

                    ResetGame();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                isRunning = false;
            }
        }

        private async Task AcceptInitialClientsAsync()
        {
            while (isRunning && connectedClients.Count < requiredClients)
            {
                Console.WriteLine("Waiting for players...");
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("Player connected!");
                _ = HandleClientAsync(client);
            }
        }


        private async Task AcceptClientsAsync()
        {
            while (isRunning && connectedClients.Count < requiredClients)
            {
                Console.WriteLine("Waiting for players...");
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine($"Player connected, ID: {nextClientId}");
                _ = HandleClientAsync(client);
            }
        }



        private void ResetGame()
        {
            connectedClients.Clear();
            nextClientId = 1;
            secretCode = Convert.ToBase64String(Encoding.UTF8.GetBytes(GenerateSecretCode(5)));
            timeLeft = 30;
            Console.WriteLine("The game has been reset. I am ready for new players. Next restart in" + DateTime.Now.AddMinutes(10).ToString("HH:mm:ss"));
        }

        private void SaveResultsToXml()
        {
            Thread.Sleep(1000);
            var xDoc = new XDocument();
            var xRoot = new XElement("Clients");

            foreach (var client in connectedClients)
            {
                double successRate = (double)(client.TotalBlackMarkers + client.TotalWhiteMarkers) / client.CommandCount * 100;
                double errorRate = 100 - successRate;

                var xClient = new XElement("Client",
                    new XAttribute("ID", client.ClientId),
                    new XElement("CommandsSent", client.CommandCount),
                    new XElement("SuccessRate", successRate.ToString("F2") + "%"),
                    new XElement("ErrorRate", errorRate.ToString("F2") + "%"),
                    new XElement("BlackMarkers", client.TotalBlackMarkers),
                    new XElement("WhiteMarkers", client.TotalWhiteMarkers),
                    new XElement("LastActivity", client.LastActivity.ToString("o")),
                    new XElement("CorrectAnswerTime", client.CorrectAnswerTime.HasValue ? client.CorrectAnswerTime.Value.ToString("o") : "No correct answer")
                );
                xRoot.Add(xClient);
                Console.WriteLine("10% [||||||.................]");
            }
            Thread.Sleep(1000);
            Console.WriteLine("45% [|||||||||||||..........]");
            xDoc.Add(xRoot);
            Thread.Sleep(2000);
            Console.WriteLine("65% [||||||||||||||||.......]");
            long unixTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            string fileName = $"ClientResults_{unixTime}.xml";
            Thread.Sleep(1000);
            Console.WriteLine("85% [||||||||||||||||||||...]");
            xDoc.Save(fileName);
            Thread.Sleep(1000);
            Console.WriteLine("100% [||||||||||||||||||||||]");
            Console.WriteLine($"Results saved to {fileName}. XML content: {xDoc}");
        }


        public async Task StartHttpListener()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://{ipString}:8080/"); // Указать нужный адрес
            listener.Start();
            Console.WriteLine($"HTTP Server started on http://{ipString}:8080/");

            while (true)
            {
                try
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;
                    response.ContentType = "application/json";  // Установка типа содержимого ответа

                    if (request.HttpMethod == "GET")
                    {
                        string rawUrl = request.RawUrl;
                        if (rawUrl == "/results")
                        {
                            string[] xmlFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.xml");

                            string jsonResponse = JsonSerializer.Serialize(xmlFiles);
                            byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                            response.ContentLength64 = buffer.Length;
                            using (System.IO.Stream output = response.OutputStream)
                            {
                                await output.WriteAsync(buffer, 0, buffer.Length);
                            }
                        }
                        else
                        {
                            int numberConnectClients = requiredClients - connectedClients.Count;
                            string jsonResponse;
                            if (numberConnectClients > 0)
                            {
                                jsonResponse = JsonSerializer.Serialize(new
                                {
                                    message = "Players needed to start the next round.",
                                    players_needed = numberConnectClients
                                });
                            }
                            else
                            {
                                jsonResponse = JsonSerializer.Serialize(new
                                {
                                    message = "All players are connected.",
                                    next_restart = restartTime.ToString("yyyy-MM-ddTHH:mm:ss")
                                });
                            }
                            byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                            response.ContentLength64 = buffer.Length;
                            using (System.IO.Stream output = response.OutputStream)
                            {
                                await output.WriteAsync(buffer, 0, buffer.Length);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("HTTP Server Error: " + ex.Message);
                }
            }
        }

    }
}
