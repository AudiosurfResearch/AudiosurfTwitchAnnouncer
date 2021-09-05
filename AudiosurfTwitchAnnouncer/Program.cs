using System;
using System.Threading;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using WebSocketSharp;
using IniParser;
using IniParser.Model;

namespace AudiosurfTwitchAnnouncer
{
    class Program
    {
        static Bot bot;
        public static IniData configData;

        static void Main(string[] args) {
            string artistName = string.Empty;
            string songName = string.Empty;
            int score = 0;

            var parser = new FileIniDataParser();
            configData = parser.ReadFile("config.ini");

            string songStartMessage = Program.configData["Announcements"]["SongStartMessage"];
            string songFinishMessage = Program.configData["Announcements"]["SongFinishMessage"];

            bot = new Bot();
            Console.WriteLine("AudiosurfTwitchAnnouncer by AudiosurfResearch");

            WebSocket ws = new WebSocket("ws://127.0.0.1:1502");
            ws.OnOpen += (sender, e) => {
                Console.WriteLine("Connected to WebSocket server!");
            };
            ws.OnError += (sender, e) => {
                Console.WriteLine("An error occurred: " + e.Message);
            };
            ws.OnClose += (sender, e) => {
                Console.WriteLine($"Disconnected from WebSocket server ({e.Reason})");
                return;
            };
            ws.OnMessage += (sender, e) => {
                Console.WriteLine("WS message received: " + e.Data);

                string command = e.Data;
                if (e.Data.Length > 4) {
                    command = e.Data.Remove(4);
                }
                
                string data = e.Data.Remove(0, 4);
                
                switch (command) {
                    case "SNAR":
                        artistName = data;
                        break;
                    case "SNST":
                        songName = data;
                        break;
                    case "SONG":
                        var startMsg = songStartMessage;
                        startMsg = startMsg.Replace("%ARTIST%", artistName);
                        startMsg = startMsg.Replace("%SONG%", songName);
                        bot.SendMessage(startMsg);
                        break;
                    case "FNSC":
                        score = int.Parse(data);
                        break;
                    case "RSLT":
                        var finishMsg = songFinishMessage;
                        finishMsg = finishMsg.Replace("%ARTIST%", artistName);
                        finishMsg = finishMsg.Replace("%SONG%", songName);
                        finishMsg = finishMsg.Replace("%SCORE%", score.ToString());
                        bot.SendMessage(finishMsg);
                        break;
                    default:
                        Console.WriteLine("Unknown command " + command);
                        break;
                }
            };

            while (!ws.IsAlive) {
                ws.Connect();
                Thread.Sleep(1000);
            }
            Console.ReadLine();
        }
    }

    class Bot
    {
        JoinedChannel channel;
        TwitchClient client;
        string userName;
        string accessToken;

        public Bot() {
            userName = Program.configData["Twitch"]["UserName"];
            accessToken = Program.configData["Twitch"]["AccessToken"];

            ConnectionCredentials credentials = new ConnectionCredentials(userName, accessToken);
            var clientOptions = new ClientOptions {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);
            client.Initialize(credentials, userName);

            //client.OnLog += Client_OnLog;
            client.OnJoinedChannel += Client_OnJoinedChannel;
            client.OnConnected += Client_OnConnected;
            client.OnIncorrectLogin += Client_OnIncorrectLogin;

            client.Connect();
            client.JoinChannel(userName);
        }

        private void Client_OnLog(object sender, OnLogArgs e) {
            Console.WriteLine($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e) {
            Console.WriteLine($"Signed in as {e.BotUsername}");
        }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e) {
            Console.WriteLine("Twitch channel joined!");
            channel = client.GetJoinedChannel(userName);
        }

        public void Client_OnIncorrectLogin(object sender, OnIncorrectLoginArgs e) {
            Console.WriteLine("Login failed! Please make sure the credentials in the config.ini file are correct.");
        }

        public void SendMessage(string msg) {
            client.SendMessage(channel, msg);
        }
    }
}
