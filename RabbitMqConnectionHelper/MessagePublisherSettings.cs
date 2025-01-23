using System;

namespace BigTyre.RabbitMq
{
    public class MessagePublisherSettings 
    {
        /// <summary>
        /// Name of the exchange that messages will be published to.
        /// </summary>
        public string ExchangeName { get; set; } = "default";

        /// <summary>
        /// Amount of time to allow for Channel creation before treating it as a timeout.
        /// </summary>
        public TimeSpan ChannelSetupTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
