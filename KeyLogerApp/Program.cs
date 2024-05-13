using KeyLogerApp;

GameServer server = new GameServer("127.0.0.1", 8888, 5);
Task serverTask = server.StartAsync();
Task httpTask = server.StartHttpListener();

await Task.WhenAll(serverTask, httpTask);