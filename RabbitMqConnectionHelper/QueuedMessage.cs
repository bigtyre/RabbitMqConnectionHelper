namespace BigTyre.RabbitMq
{
    public class QueuedMessage
    {
        public QueuedMessage(string topic, object message)
        {
            Topic = topic;
            Message = message;
        }

        public string Topic { get; }
        public object Message { get; }
    }
}
