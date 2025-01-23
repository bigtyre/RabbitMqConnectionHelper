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
        private bool _isDisposed;
        private Task<IModel> _channelTask;

        protected IModel Channel { get; private set; }

        private readonly string _exchangeName;
        private readonly TimeSpan _channelSetupTimeout;
        private readonly RabbitMqConnectionProvider _connectionProvider;
        private readonly ILogger<MessagePublisher> _logger;

        public MessagePublisher(
            RabbitMqConnectionProvider connectionProvider, 
            ILogger<MessagePublisher> logger,
            MessagePublisherSettings settings = null
        )
        {
            var config = settings ?? new MessagePublisherSettings();
            _channelSetupTimeout = config.ChannelSetupTimeout;
            _exchangeName = config.ExchangeName;

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
                _logger.LogDebug($"Publishing JSON message of type {type} to topic {topic} on exchange {_exchangeName}");

                props.Type = type;

                lock (channel)
                {
                    channel.BasicPublish(
                        _exchangeName,
                        topic,
                        props,
                        body: content
                    );
                }


                _logger.LogDebug($"Successfully published JSON message of type {type} to topic {topic} on exchange {_exchangeName}.");
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

                var timeoutTask = Task.Delay(_channelSetupTimeout);

                await Task.WhenAny(timeoutTask, channelTask);

                if (channelTask.IsCompleted is false && timeoutTask.IsCompleted)
                {
                    throw new TimeoutException($"Timed out while waiting for RabbitMQ channel to be established. Timeout duration was {_channelSetupTimeout.TotalSeconds} sec.");
                }

                var channel = await channelTask;
                channel.ExchangeDeclarePassive(_exchangeName);

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

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed)
                return;
            
            if (isDisposing)
            {
                lock (ChannelLock)
                {
                    if (Channel?.IsOpen == true) Channel?.Close();
                    Channel?.Dispose();
                }
            }

            _isDisposed = true;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(isDisposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
