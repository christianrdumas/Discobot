﻿using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.ComponentModel;
using System.Configuration;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Linq;
using Discobot.Utilities;
using System.Collections.Generic;
using Discobot.Modules;
using Mono.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
//https://stackexchange.github.io/StackExchange.Redis/Basics
//https://github.com/Yortw/tweetmoasharp
namespace Discobot
{
    class Program
    {

        static BackgroundWorker Worker = new BackgroundWorker();
        string token = ConfigurationManager.AppSettings["apiKey"];

        private CommandService commands = new CommandService();
        private DiscordSocketClient client = new DiscordSocketClient();
        private IServiceProvider services = new ServiceCollection().BuildServiceProvider();
        //private ServiceCollection servicesProvider = new ServiceCollection();

        static void Main(string[] args) => new Program().Start().GetAwaiter().GetResult();

        public async Task Start()
        {
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            await InstallCommands();
            var c = commands;
            await Task.Delay(-1);
        }

        public async Task InstallCommands()
        {
            client.MessageReceived += HandleCommands;
            await commands.AddModulesAsync(Assembly.GetEntryAssembly());

        }

        public async Task HandleCommands(SocketMessage messageParam)
        {
            var message = messageParam as SocketUserMessage;

            var userName = message.Author.Username;

            if (message == null) return;

            int argPos = 0;

            var context = new CommandContext(client, message);


            #region command_or_at
            if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos)) && !overRideDefaultAllowances(message))
            {
                return;
            }
            #endregion

            string channelFrom = message.Channel.Name;
            //move to config
            List<string> allowedChannels = new List<string>(new string[] { "bot-channel" });

            Tuple< ModuleUtilities.ImageLocations,string> imageWhere = CheckForImage(message);

            if (imageWhere.Item1 != ModuleUtilities.ImageLocations.None && false) //disable this for the time being it doesn't totally work and presents issues of its own
            {
                new Thread(async () =>
                {
                    var typingOnReplaceImage = context.Channel.EnterTypingState();
                    var gif = GifUtilities.DoFaceReplace(imageWhere.Item2, "lampreyme.png");
                    //lets do some dubious reflection
                    //PropertyInfo propInfo = typeof(SocketUserMessage).GetProperty("Attachments");
                    //FieldInfo contentField = propInfo.GetBackingField();
                    //contentField.SetValue(message, gif);
                    if (imageWhere.Item1 == ModuleUtilities.ImageLocations.Message)
                    {
                        await context.Channel.SendMessageAsync(gif);
                    }
                    else
                    {
                        await context.Channel.SendFileAsync(gif);
                    }
                    typingOnReplaceImage.Dispose();
                    return;
                    ;
                    //typingOnReplaceImage.Dispose();

                }).Start();
            }

            #region wrong_channel
            if (!allowedChannels.Exists(s => string.Equals(s, channelFrom, StringComparison.OrdinalIgnoreCase)) && !overRideDefaultAllowances(message))
            {
                var wrongContext = new CommandContext(client, message);
                await wrongContext.Channel.SendMessageAsync(("WRONG CHANNEL"));
                return;
            }
            #endregion

            #region intercept_text
            if (overRideDefaultAllowances(message) && !message.HasCharPrefix('!', ref argPos))
            {
                PropertyInfo propInfo = typeof(SocketUserMessage).GetProperty("Content");
                FieldInfo contentField = propInfo.GetBackingField();
                argPos++;
                Random rand = new Random();
                int pick = rand.Next(0, 2);
                if(pick == 0)
                {
                    contentField.SetValue(message, "!scrustspeak 4 " + message.Content);
                }
                else
                {
                    contentField.SetValue(message, "!muddlespeak " + message.Content);
                }
            }
            #endregion


            var typing = context.Channel.EnterTypingState();
            new Thread(async () =>
            {
                var result = await commands.ExecuteAsync(context, argPos, services);
                typing.Dispose();
                if (!result.IsSuccess)
                {
                    await context.Channel.SendMessageAsync(result.ErrorReason);
                }
                typing.Dispose();

            }).Start();
            typing.Dispose();

        }

        public bool overRideDefaultAllowances(SocketUserMessage message)
        {
            List<string> interuptUsers = ConfigurationManager.AppSettings["overrideUsers"].Split(new string[] {", " }, StringSplitOptions.None).ToList();
            var userName = message.Author.Username;

            if (interuptUsers.Exists(u => string.Equals(u, userName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            return false;
        }

        //change return
        public Tuple<ModuleUtilities.ImageLocations, string> CheckForImage(SocketMessage message)
        {
            //shore up regex so i can just return th file matches
            Regex matchFiles = new Regex("gif|png|jpg");

            if (matchFiles.Match(message.Content).Success)
            {
                return Tuple.Create(ModuleUtilities.ImageLocations.Message, message.Content);
            }

            if ((message.Attachments.Count() > 0 && matchFiles.Match(message.Attachments.ToList()[0].Url).Success))
            {
                return Tuple.Create(ModuleUtilities.ImageLocations.Attachment, message.Attachments.ToList()[0].Url);
            }

            if (message.Embeds.Count >0 && matchFiles.Match(message.Embeds.First().Thumbnail.ToString()).Success)
            {
                return Tuple.Create(ModuleUtilities.ImageLocations.Preview,message.Embeds.First().Thumbnail.ToString());

            }
            return Tuple.Create(ModuleUtilities.ImageLocations.None,"none");

        }

        public SocketMessage ModifyMessageImage(SocketMessage message)
        {
            return message;
        }
    }

}
