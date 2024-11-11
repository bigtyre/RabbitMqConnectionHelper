using RabbitMQ.Client;

namespace BigTyre.RabbitMq
{
    public interface IMessageClient
    {
        void BindQueues(IModel channel);
    }
}
