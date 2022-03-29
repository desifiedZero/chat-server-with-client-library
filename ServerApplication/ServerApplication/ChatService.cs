using System;
using System.Configuration;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ServerApplication
{
    public class ChatService
    {
        private readonly ChatServer _chatClient;

        public ChatService() { _chatClient = new(); }

        public async Task GetChatAsync(CancellationToken token)
        {

            await Task.Run(() =>
            {
                string ip = ConfigurationManager.AppSettings["IPAddress"];
                int port = int.Parse(ConfigurationManager.AppSettings["Port"]);

                try
                {
                    _chatClient.Init(IPAddress.Parse(ip), port);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }, token);
        }
    }
}