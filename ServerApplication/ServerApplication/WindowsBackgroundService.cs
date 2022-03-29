using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ServerApplication
{
    public sealed class WindowsBackgroundService : BackgroundService
    {
        private readonly ChatService _chatService;
        private readonly ILogger<WindowsBackgroundService> _logger;

        public WindowsBackgroundService(
            ChatService chatService,
            ILogger<WindowsBackgroundService> logger) =>
            (_chatService, _logger) = (chatService, logger);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _chatService.GetChatAsync(stoppingToken);
        }


    }
}
