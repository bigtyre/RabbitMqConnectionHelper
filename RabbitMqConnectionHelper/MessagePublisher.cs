using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Text;
using System.Threading.Tasks;

namespace BigTyre.RabbitMq
{
    public class MessagePublisher : IMessagePublisher, IDisposable
    {
        private bool disposedValue;
        private Task<IModel> _channelTask;
        protected const string ExchangeName = "default";

        protected IModel Channel { get; private set; }

        private readonly RabbitMqConnectionProvider _connectionProvider;
        private readonly ILogger<MessagePublisher> _logger;

        public MessagePublisher(RabbitMqConnectionProvider connectionProvider, ILogger<MessagePublisher> logger)
        {
            if (connectionProvider is null) throw new ArgumentNullException(nameof(connectionProvider));
            _connectionProvider = connectionProvider;
            _logger = logger;
        }

        private void PublishJsonMessage(IModel channel, string topic, object message, string type = null)
        {
            if (channel is null) throw new ArgumentNullException(nameof(channel));
            if (message is null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException($"'{nameof(topic)}' cannot be null or whitespace", nameof(topic));

            string result = JsonConvert.SerializeObject(message);
            byte[] content = Encoding.UTF8.GetBytes(result);

            type = type ?? topic;

            IBasicProperties props = channel.CreateBasicProperties();

            try
            {
                _logger.LogDebug($"Publishing JSON message of type {type} to topic {topic} on exchange {ExchangeName}");

                props.Type = type;

                lock (channel)
                {
                    channel.BasicPublish(
                        ExchangeName,
                        topic,
                        props,
                        body: content
                    );
                }


                _logger.LogDebug($"Successfully published JSON message of type {type} to topic {topic} on exchange {ExchangeName}.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to publish message: {message}", ex.Message);
                throw;
            }
        }

        private readonly object ChannelLock = new object();

        public async Task PublishJsonMessageAsync(string topic, object message, string type = null)
        {
            Task<IModel> channelTask = GetOrCreateChannelAsync();

            var channel = await channelTask ?? throw new InvalidOperationException("RabbitMQ channel task completed, but channel was null.");

            if (channel.IsClosed)
                throw new InvalidOperationException("Cannot publish message. RabbitMQ channel is closed.");

            PublishJsonMessage(channel, topic, message, type);
        }

        private Task<IModel> GetOrCreateChannelAsync()
        {
            lock (ChannelLock)
            {
                var existingChannel = Channel;

                if (existingChannel != null)
                {
                    _logger.LogDebug("Using existing message channel");
                    return Task.FromResult(existingChannel);
                }

                var task = _channelTask;
                if (task != null)
                {
                    _logger.LogDebug("Found existing channel generation task. awaiting it.");
                    return task;
                }

                _logger.LogDebug("Started a new task to open a RabbitMQ channel");

                task = Task.Run(CreateAndAssignChannel);

                _channelTask = task;

                return task;
            }
        }

        private async Task<IModel> CreateAndAssignChannel()
        {
            try
            {
                _logger.LogDebug("Creating RabbitMQ channel.");

                var channelTask = _connectionProvider.CreateChannelAsync();

                var connectionTimeout = TimeSpan.FromSeconds(10);

                var timeoutTask = Task.Delay(connectionTimeout);

                await Task.WhenAny(timeoutTask, channelTask);

                if (channelTask.IsCompleted is false && timeoutTask.IsCompleted)
                {
                    throw new TimeoutException($"Timed out while waiting for RabbitMQ channel to be established. Timeout duration was {connectionTimeout.TotalSeconds} sec.");
                }

                var channel = await channelTask;
                channel.ExchangeDeclarePassive(ExchangeName);

                if (channel is null)
                {
                    _logger.LogDebug("Create channel task completed but channel returned is null");
                    throw new Exception("Failed to create channel. Connection provider returned a null channel.");
                }

                _logger.LogDebug("RabbitMQ channel created. Assigning it to MessagePublisher and returning it.");

                lock (ChannelLock)
                {
                    Channel = channel;
                }
                return channel;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create RabbitMQ channel: {ex}", ex.Message);
                throw;
            }
            finally
            {
                _channelTask = null;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    lock (ChannelLock)
                    {
                        if (Channel?.IsOpen == true) Channel?.Close();
                        Channel?.Dispose();
                    }
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
