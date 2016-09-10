﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Services;
using NadekoBot.Services.Database;
using NadekoBot.Services.Impl;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace NadekoBot
{
    public class NadekoBot
    {
        private Logger _log;

        public static CommandService Commands { get; private set; }
        public static CommandHandler CommandHandler { get; private set; }
        public static DiscordSocketClient Client { get; private set; }
        public static Localization Localizer { get; private set; }
        public static BotCredentials Credentials { get; private set; }

        public static GoogleApiService Google { get; private set; }
        public static StatsService Stats { get; private set; }

        public async Task RunAsync(string[] args)
        {
            SetupLogger();

            for (int i = 0; i < 100; i++)
            {
                var rnd = new NadekoRandom();
                Console.WriteLine(rnd.Next(-10, -1));
            }

            //create client
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                AudioMode = Discord.Audio.AudioMode.Outgoing,
                MessageCacheSize = 10,
                LogLevel = LogSeverity.Warning,
            });

            //initialize Services
            Credentials = new BotCredentials();
            Commands = new CommandService();
            Localizer = new Localization();
            Google = new GoogleApiService();
            CommandHandler = new CommandHandler(Client, Commands);
            Stats = new StatsService(Client, CommandHandler);
            _log = LogManager.GetCurrentClassLogger();

            //init db
            using (var context = DbHandler.Instance.GetDbContext())
            {
                context.EnsureSeedData();
            }

            //setup DI
            var depMap = new DependencyMap();
            depMap.Add<ILocalization>(Localizer);
            depMap.Add<DiscordSocketClient>(Client);
            depMap.Add<CommandService>(Commands);
            depMap.Add<IGoogleApiService>(Google);

            //connect
            await Client.LoginAsync(TokenType.Bot, Credentials.Token).ConfigureAwait(false);
            await Client.ConnectAsync().ConfigureAwait(false);
            await Client.DownloadAllUsersAsync().ConfigureAwait(false);

            _log.Info("Connected");

            //load commands
            await Commands.LoadAssembly(Assembly.GetEntryAssembly(), depMap).ConfigureAwait(false);

            Console.WriteLine(await Stats.Print().ConfigureAwait(false));

            await Task.Delay(-1);
        }

        private void SetupLogger()
        {
            try
            {
                var logConfig = new LoggingConfiguration();
                var consoleTarget = new ColoredConsoleTarget();

                consoleTarget.Layout = @"${date:format=HH\:mm\:ss} ${logger} | ${message}";

                logConfig.AddTarget("Console", consoleTarget);

                logConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));

                LogManager.Configuration = logConfig;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}