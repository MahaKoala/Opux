﻿using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using EveLibCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Opux
{
    public class Program
    {
        public static DiscordSocketClient Client { get; private set; }
        public static CommandService Commands { get; private set; }
        public static EveLib EveLib { get; private set; }
        public static String ApplicationBase { get; private set; }
        public static IServiceProvider ServiceCollection { get; private set; }
        public static IConfigurationRoot Settings { get; private set; }
        internal static readonly HttpClient _httpClient = new HttpClient();
        internal static bool quit = false;
        internal static bool debug = false;

        static AutoResetEvent autoEvent = new AutoResetEvent(true);

        static Timer stateTimer = new Timer(Functions.RunTick, autoEvent, 100, 100);

        public static void Main(string[] args)
        {

            ApplicationBase = Path.GetDirectoryName(new Uri(Assembly.GetEntryAssembly().CodeBase).LocalPath);
            if (!File.Exists(Path.Combine(Program.ApplicationBase, "Opux.db")))
                File.Copy(ApplicationBase + "/Opux.def.db", Path.Combine(Program.ApplicationBase, "Opux.db"));

            UpdateSettings();

            Client = new DiscordSocketClient(new DiscordSocketConfig() { });
            Commands = new CommandService();
            EveLib = new EveLib();
            MainAsync(args).GetAwaiter().GetResult();

            while (!quit)
            {
                var command = Console.ReadLine();
                switch (command.Split(" ")[0])
                {
                    case "quit":
                        Console.WriteLine($"Quitting Opux");
                        quit = true;
                        break;
                    case "debug":
                        if (!debug)
                        {
                            Console.WriteLine($"Debug mode Active");
                            debug = true;
                        }
                        else
                        {
                            Console.WriteLine($"Debug mode Disabled");
                            debug = false;
                        }
                        break;
                    case "admin":
                        var guild = Client.GetGuild(Convert.ToUInt64(Settings.GetSection("config")["guildId"]));
                        var rolesToAdd = new List<SocketRole>();
                        var GuildRoles = guild.Roles;
                        guild.GetUser(Convert.ToUInt64(command.Split(" ")[2])).AddRoleAsync(GuildRoles.FirstOrDefault(x => x.Name == command.Split(" ")[1]));
                        break;
                }
            }
            Client.StopAsync();

        }

        internal static async Task LoggerAsync(Exception args)
        {
            await Functions.Client_Log(new LogMessage(LogSeverity.Error, "Main", args.Message, args));
        }

        internal static async Task MainAsync(string[] args)
        {
            Client.Log += Functions.Client_Log;
            Client.UserJoined += Functions.Event_UserJoined;
            Client.Ready += Functions.Ready;

            try
            {
                await Functions.InstallCommands();
                await Client.LoginAsync(TokenType.Bot, Settings.GetSection("config")["token"]);
                await Client.StartAsync();
            }
            catch (HttpException ex)
            {
                if (ex.Reason.Contains("401"))
                {
                    await Functions.Client_Log(new LogMessage(LogSeverity.Error, "Discord", $"Check your Token: {ex.Reason}"));
                }
            }
            catch (Exception ex)
            {
                await Functions.Client_Log(new LogMessage(LogSeverity.Error, "Main", ex.Message, ex));
            }
        }

        public static Task UpdateSettings()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "OpuxBot");
                Settings = new ConfigurationBuilder()
                .SetBasePath(ApplicationBase)
                .AddJsonFile("settings.json", optional: true, reloadOnChange: true).Build();
                if (Convert.ToBoolean(Program.Settings.GetSection("config")["notificationFeed"]))
                    Functions._nextNotificationCheck = DateTime.Parse(Functions.SQLiteDataQuery("cacheData", "data", "nextNotificationCheck").GetAwaiter().GetResult());
            }
            catch (Exception ex)
            {
                var debug = ex.Message;
            }
            return Task.CompletedTask;
        }
    }
}
