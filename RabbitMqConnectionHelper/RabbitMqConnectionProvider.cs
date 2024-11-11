using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace BigTyre.RabbitMq
{
    public class RabbitMqConnectionProvider : IDisposable
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly object _connectionLock = new object();
        private readonly ILogger<RabbitMqConnectionProvider> _logger;

        private IConnection _connection;
        private bool _isDisposed;

        private static int InstanceCount = 0;

        public RabbitMqConnectionProvider(IConnectionFactory connectionFactory, ILogger<RabbitMqConnectionProvider> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger;
            InstanceCount++;
            if (InstanceCount > 1)
            {
                _logger.LogWarning($"Multiple instances of {nameof(RabbitMqConnectionProvider)} created. {nameof(RabbitMqConnectionProvider)} creates a RabbitMQ connection per instance. You can typically re-use the RabbitMQ connection throughout an application. Ensure you are not creating more connections than necessary.");
            }
        }

        private Task<IConnection> _connectionTask;

        public async Task<IModel> CreateChannelAsync()
        {
            _logger.LogTrace("Creating RabbitMQ channel.");

            var connection = await GetOrCreateConnectionAsync();

            var uid = Guid.NewGuid();

            var model = connection.CreateModel();

            if (model is null)
            {
                _logger.LogWarning("Create Channel returned a null value.");
            }

            _logger.LogDebug("RabbitMQ channel created: {uid}", uid);

            void ModelShutdownHandler(object sender, ShutdownEventArgs e)
            {
                Model_ModelShutdown(e, uid);
                model.ModelShutdown -= ModelShutdownHandler;
            }

            model.ModelShutdown += ModelShutdownHandler;

            return model;
        }

        private void Model_ModelShutdown(ShutdownEventArgs e, Guid channelId)
        {
            var reason = e.ReplyText;
            _logger.LogDebug("RabbitMQ channel shutdown {channelId}: {reason}", channelId, reason);
            
        }

        private async Task<IConnection> GetOrCreateConnectionAsync()
        {
            IConnection connection;
            lock (_connectionLock)
            {
                _connectionTask = GetOrStartConnectionTask();
            }

            connection = await _connectionTask.ConfigureAwait(false);

            if (connection is null)
            {
                throw new InvalidOperationException("Connection task completed but connection was null.");
            }

            return connection;
        }


        private async Task<IConnection> GetOrStartConnectionTask()
        {
            if (!(_connection is null))
                return _connection;

            if (!(_connectionTask is null))
                return await _connectionTask;

            return await Task.Run(async () =>
            {
                try
                {
                    var newConnection = CreateConnection();

                    newConnection.ConnectionShutdown += HandleConnectionShutdown;

                    newConnection.CallbackException += CallbackException;
                    newConnection.ConnectionBlocked += ConnectionBlocked;
                    newConnection.ConnectionUnblocked += ConnectionUnblocked;

                    lock (_connectionLock)
                    {
                        _connection = newConnection;
                    }

                    return newConnection;
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    lock (_connectionLock)
                    {
                        _connectionTask = null;
                    }
                }
            });
        }

        private void CallbackException(object sender, CallbackExceptionEventArgs e)
        {
            _logger.LogInformation("RabbitMQ connection callback exception.");
        }

        private void ConnectionBlocked(object sender, RabbitMQ.Client.Events.ConnectionBlockedEventArgs e)
        {
            _logger.LogInformation("RabbitMQ connection blocked: {reason}", e.Reason);
        }

        private void ConnectionUnblocked(object sender, EventArgs e)
        {
            _logger.LogInformation("RabbitMQ connection unblocked.");
        }

        private void HandleConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            var cause = e.ReplyText;
            _logger.LogInformation("RabbitMQ connection shutdown. {cause}", cause);
        }

        private IConnection CreateConnection()
        {
            try
            {
                _logger.LogInformation("Creating RabbitMQ connection.");

                var connection = _connectionFactory.CreateConnection();

                _logger.LogInformation("RabbitMQ connection created.");
                return connection;
            }
            catch (AuthenticationFailureException ex)
            {
                _logger.LogError("RabbitMQ authentication failed: " + ex.Message, ex);
                throw;
            }
            catch (PossibleAuthenticationFailureException ex)
            {
                _logger.LogError("RabbitMQ connection failed due, possibly due to authentication: " + ex.Message, ex);
                throw;
            }
            catch (ProtocolVersionMismatchException ex)
            {
                _logger.LogError("RabbitMQ connection failed due to protocol version mismatch: " + ex.Message, ex);
                throw;
            }
            catch (BrokerUnreachableException ex)
                when (ex.InnerException is OperationInterruptedException e && e.ShutdownReason.ReplyCode == 530)
            {
                _logger.LogError("RabbitMQ connection failed. User does not have permission to access this vhost.", ex);
                throw;
            }
            catch (BrokerUnreachableException ex)
                when (ex.InnerException is OperationInterruptedException e && e.ShutdownReason.ReplyCode == 530)
            {
                _logger.LogError(ex.Message, ex);
                throw;
                //await Task.Delay(500, stoppingToken).ConfigureAwait(false);
            }
            catch (ConnectFailureException ex)
            {
                _logger.LogError("Connection failed", ex);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to create RabbitMQ connection: " + ex.Message, ex);
                throw;
            }

        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed)
                return;

            if (isDisposing)
            {
                _connection?.Close();
                _connection?.Dispose();
                _connection = null;

                InstanceCount--;
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
