﻿// Developed by Softeq Development Corporation
// http://www.softeq.com

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Softeq.NetKit.Chat.Common.Configuration;

namespace Softeq.NetKit.Chat.SignalRClient.Sample
{
    class Program
    {
        private const string EnvironmentVariableName = "ASPNETCORE_ENVIRONMENT";

        static void Main(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
                .Build();

            var authMicroserviceConfiguration = GetAuthMicroserviceConfiguration(configuration);
            var chatMicroserviceConfiguration = GetChatMicroserviceConfiguration(configuration);

            if (authMicroserviceConfiguration != null && chatMicroserviceConfiguration != null)
            {
                var signalRClient = new SignalRClient(chatMicroserviceConfiguration.ChatUrl);
                var manualResetEventSlim = new ManualResetEventSlim();

                var runningClientTask = RunClientAsync(authMicroserviceConfiguration, signalRClient, manualResetEventSlim);
                runningClientTask.Wait();

                manualResetEventSlim.Wait();
            }
        }

        private static AuthMicroserviceConfiguration GetAuthMicroserviceConfiguration(IConfiguration configuration)
        {
            return new AuthMicroserviceConfiguration
            {
                AuthUrl = configuration[ConfigurationSettings.AuthUrl],
                UserName = configuration[ConfigurationSettings.AuthUserName],
                Password = configuration[ConfigurationSettings.AuthPassword],
                InvitedUserName = configuration[ConfigurationSettings.AuthInvitedUserName]
            };
        }

        private static ChatMicroserviceConfiguration GetChatMicroserviceConfiguration(IConfiguration configuration)
        {
            return new ChatMicroserviceConfiguration
            {
                ChatUrl = configuration[ConfigurationSettings.ChatUrl]
            };
        }

        private static async Task RunClientAsync(AuthMicroserviceConfiguration authMicroserviceConfiguration, SignalRClient signalRClient, ManualResetEventSlim wh)
        {
            try
            {
                while (true)
                {
                    Console.WriteLine("Choose required option for testing from the list below:");
                    Console.WriteLine("1 - Channels management");
                    Console.WriteLine("2 - Messages management");
                    Console.WriteLine("3 - Members management");
                    Console.WriteLine("0 - Close");
                    bool isParsed = int.TryParse(Console.ReadLine(), out int choiceNumber);
                    if (isParsed)
                    {
                        await HandleChoiceNumberAsync(choiceNumber, signalRClient, authMicroserviceConfiguration);
                    }
                    else
                    {
                        Console.WriteLine("Error. Choose a valid digit!");
                    }

                    if (isParsed && choiceNumber == 0)
                    {
                        break;
                    }
                }
                
                await signalRClient.Disconnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.GetBaseException().Message);
            }
            finally
            {
                wh.Set();
            }
        }

        private static async Task HandleChoiceNumberAsync(int choiceNumber, SignalRClient signalRClient, AuthMicroserviceConfiguration authMicroserviceConfiguration)
        {
            switch (choiceNumber)
            {
                case 0:
                    break;
                case 1:
                    await ExecuteCallManagementLogic(signalRClient, authMicroserviceConfiguration);
                    Console.WriteLine("Testing has passed successfully.");
                    break;
                case 2:
                    await ExecuteMessageManagementLogic(signalRClient, authMicroserviceConfiguration);
                    Console.WriteLine("Testing has passed successfully.");
                    break;
                case 3:
                    await ExecuteMembersManagementLogic(signalRClient, authMicroserviceConfiguration);
                    Console.WriteLine("Testing has passed successfully.");
                    break;
                default:
                    Console.WriteLine("Error. Choose a valid digit!");
                    break;
            }
        }

        private static async Task ExecuteCallManagementLogic(SignalRClient signalRClient, AuthMicroserviceConfiguration authMicroserviceConfiguration)
        {
            await ConnectAsync(authMicroserviceConfiguration, signalRClient);
            var client = await HubCommands.GetClientAsync(signalRClient);
            var channel = await HubCommands.CreateChannelAsync(signalRClient);
            var secondChannel = await HubCommands.CreateChannelAsync(signalRClient);
            await HubCommands.UpdateChannelAsync(signalRClient, channel.Id);
            await HubCommands.MuteChannelAsync(signalRClient, channel.Id);
            await HubCommands.PinChannelAsync(signalRClient, channel.Id);
            await HubCommands.CloseChannelAsync(signalRClient, channel.Id);

            // switch user
            authMicroserviceConfiguration.UserName = authMicroserviceConfiguration.InvitedUserName;
            await ConnectAsync(authMicroserviceConfiguration, signalRClient);
            await HubCommands.JoinToChannelAsync(signalRClient, secondChannel.Id);
            await HubCommands.LeaveChannelAsync(signalRClient, secondChannel.Id);
            await HubCommands.CreateDirectChannelAsync(signalRClient, client.MemberId);
        }

        private static async Task ExecuteMessageManagementLogic(SignalRClient signalRClient, AuthMicroserviceConfiguration authMicroserviceConfiguration)
        {
            await ConnectAsync(authMicroserviceConfiguration, signalRClient);
            var channel = await HubCommands.CreateChannelAsync(signalRClient);
            var message = await HubCommands.AddMessageAsync(signalRClient, channel.Id);
            await HubCommands.SetLastReadMessageAsync(signalRClient, channel.Id, message.Id);
            await HubCommands.UpdateMessageAsync(signalRClient, message.Id);
            await HubCommands.DeleteMessageAsync(signalRClient, channel.Id, message.Id);
        }

        private static async Task ExecuteMembersManagementLogic(SignalRClient signalRClient, AuthMicroserviceConfiguration authMicroserviceConfiguration)
        {
            var userName = authMicroserviceConfiguration.UserName;
            var invitedUserName = authMicroserviceConfiguration.InvitedUserName;
            authMicroserviceConfiguration.UserName = invitedUserName;
            await ConnectAsync(authMicroserviceConfiguration, signalRClient);
            var client = await HubCommands.GetClientAsync(signalRClient);

            // switch user
            authMicroserviceConfiguration.UserName = userName;
            await ConnectAsync(authMicroserviceConfiguration, signalRClient);
            var channel = await HubCommands.CreateChannelAsync(signalRClient);
            await HubCommands.InviteMemberAsync(signalRClient, channel.Id, client.MemberId);
            await HubCommands.DeleteMemberAsync(signalRClient, channel.Id, client.MemberId);
            await HubCommands.InviteMultipleMembersAsync(signalRClient, channel.Id, client.MemberId);
        }

        private static async Task ConnectAsync(AuthMicroserviceConfiguration authMicroserviceConfiguration, SignalRClient client)
        {
            var token = await GetJwtTokenAsync(authMicroserviceConfiguration.Password, authMicroserviceConfiguration.UserName, authMicroserviceConfiguration.AuthUrl);
            await client.ConnectAsync(token);

            Console.WriteLine("Logged on successfully.");
            Console.WriteLine();
        }

        private static async Task<string> GetJwtTokenAsync(string password, string userName, string authServerUrl)
        {
            var httpClient = new HttpClient();

            var values = new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "password", password },
                { "username", userName },
                { "scope", "api" },
                { "client_id", "ro.client" },
                { "client_secret", "secret" }
            };

            var content = new FormUrlEncodedContent(values);

            var response = await httpClient.PostAsync($"{authServerUrl}/connect/token", content);

            var responseString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseString);
            var accessToken = json.Value<string>("access_token");

            return accessToken;
        }
    }
}
