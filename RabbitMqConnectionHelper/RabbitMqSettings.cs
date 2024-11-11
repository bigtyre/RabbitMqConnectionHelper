using RabbitMQ.Client;
using System;
using System.Configuration;

namespace BigTyre.RabbitMq
{
    public class RabbitMqSettings
    {
        public Uri Uri { get; set; }
        public string ClientProvidedName { get; set; }

        public void Validate()
        {
            if (Uri == null) throw new Exception($"{nameof(Uri)} has not been set.");
        }

        public IConnectionFactory CreateConnectionFactory(bool asyncConsumers = false)
        {
            Validate();

            return new ConnectionFactory()
            {
                Uri = Uri,
                ClientProvidedName = ClientProvidedName,
                AutomaticRecoveryEnabled = true,
                DispatchConsumersAsync = asyncConsumers
            };
        }
    }
}
