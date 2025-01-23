using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BigTyre.RabbitMq.Heartbeats
{

    public class RabbitMqHeartbeatPublisherService : BackgroundService
    {
        private readonly IMessagePublisher _messagePublisher;
        private readonly RabbitMqHeartbeatPublisherSettings _settings;
        private readonly ILogger<RabbitMqHeartbeatPublisherService> _logger;

        public RabbitMqHeartbeatPublisherService(
            IMessagePublisher messagePublisher,
            RabbitMqHeartbeatPublisherSettings settings,
            ILogger<RabbitMqHeartbeatPublisherService> logger = null
        )
        {
            _messagePublisher = messagePublisher;
            _settings = settings;
            _logger = logger ?? new NullLogger<RabbitMqHeartbeatPublisherService>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var heartbeatSeconds = _settings.HeartbeatSeconds;

            var heartbeatInterval = TimeSpan.FromSeconds(heartbeatSeconds);

            _logger.LogInformation("{serviceName} started. Heartbeats will be published every {intervalSec} sec", nameof(RabbitMqHeartbeatPublisherService), heartbeatSeconds);

            try
            {
                await Task.Run(async () =>
                {

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            _logger.LogTrace("Publishing heartbeat message");

                            await _messagePublisher.PublishJsonMessageAsync(
                                "heartbeats",
                                new
                                {
                                    _settings.AppId,
                                    Time = DateTimeOffset.Now.ToString("O")
                                }
                            );

                            _logger.LogTrace("Successfully published heartbeat message");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to publish heartbeat message: {error}", ex.Message);
                        }

                        await Task.Delay(heartbeatInterval, stoppingToken);
                    }
                }, stoppingToken);
            }
            finally
            {
                _logger.LogInformation("{serviceName} stopped.", nameof(RabbitMqHeartbeatPublisherService));
            }
        }
    }
}