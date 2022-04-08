using Inviter.Server.Services;

namespace Inviter.Server.Workers
{
    public class PlayerWorker : BackgroundService
    {
        private readonly PlayerService _playerService;

        public PlayerWorker(PlayerService playerService)
        {
            _playerService = playerService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _playerService.Poll();
                await Task.Delay(100, stoppingToken); // The server polls at 100 ms
            }
        }
    }
}