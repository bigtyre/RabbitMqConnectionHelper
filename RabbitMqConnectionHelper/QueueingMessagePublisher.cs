using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BigTyre.RabbitMq
{
    public class QueueingMessagePublisher : IDisposable, IMessagePublisher
    {
        private bool _isDisposed;
        private readonly IMessagePublisher _messagePublisher;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _cancellationToken;
        private readonly ConcurrentQueue<QueuedMessage> _messageQueue = new ConcurrentQueue<QueuedMessage>();

        public int MaxQueuedMessages { get; }

        public QueueingMessagePublisher(IMessagePublisher messagePublisher, int maxQueuedMessages)
        {
            _messagePublisher = messagePublisher;
            MaxQueuedMessages = maxQueuedMessages;
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;

            Task.Run(() => ProcessMessageQueueUntilCancelled(_cancellationToken));
        }

        private async void ProcessMessageQueueUntilCancelled(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        while (_messageQueue.TryDequeue(out var msg))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // TODO - Make this re-attempt to publish a failed message?
                            await TryPublishMessageAsync(msg);
                        }
                        await Task.Delay(100, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task<bool> TryPublishMessageAsync(QueuedMessage msg)
        {
            try
            {
                await _messagePublisher.PublishJsonMessageAsync(msg.Topic, msg.Message);
            }
            catch
            {
                return false;
            }
            return true;
        }

        public void EnqueueMessage(string topic, object message)
        {
            if (_messageQueue.Count >= MaxQueuedMessages)
            {
                // TODO - Log a warning? Throw an exception?
                return;
            }

            var msg = new QueuedMessage(topic, message);
            _messageQueue.Enqueue(msg);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed)
                return;
            
            if (isDisposing)
            {
                try
                {
                    _cancellationTokenSource.Dispose();
                }
                catch (ObjectDisposedException)
                {

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

        public Task PublishJsonMessageAsync(string topic, object message, string type = null)
        {
            EnqueueMessage(topic, message);
            return Task.CompletedTask;
        }
    }
}
