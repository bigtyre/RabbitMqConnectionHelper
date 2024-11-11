using System.Threading.Tasks;

namespace BigTyre.RabbitMq
{
    public interface IMessagePublisher
    {
        Task PublishJsonMessageAsync(string topic, object message, string type = null);
    }
}