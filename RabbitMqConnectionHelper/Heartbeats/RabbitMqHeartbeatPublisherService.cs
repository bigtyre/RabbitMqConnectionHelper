using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BigTyre.RabbitMq.Heartbeats
{

    public class RabbitMqHeartbeatPublisherService : BackgroundService
    {
        private readonly IMessagePublisher messagePublisher;
        private readonly RabbitMqHeartbeatPublisherSettings settings;

        public RabbitMqHeartbeatPublisherService(
            IMessagePublisher messagePublisher,
            RabbitMqHeartbeatPublisherSettings settings
        )
        {
            this.messagePublisher = messagePublisher;
            this.settings = settings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(async () =>
            {
                var heartbeatInterval = TimeSpan.FromSeconds(settings.HeartbeatSeconds);

                while (!stoppingToken.IsCancellationRequested)
                {
                    // logger.LogTrace("Publishing heartbeat message");

                    await messagePublisher.PublishJsonMessageAsync(
                        "heartbeats",
                        new
                        {
                            settings.AppId,
                            Time = DateTimeOffset.Now.ToString("O")
                        }
                    );

                    await Task.Delay(heartbeatInterval, stoppingToken);
                }
            }, stoppingToken);
        }
    }
}