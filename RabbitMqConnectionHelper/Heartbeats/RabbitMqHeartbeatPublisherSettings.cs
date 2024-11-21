namespace BigTyre.RabbitMq.Heartbeats
{
    public readonly struct RabbitMqHeartbeatPublisherSettings
    {
        public RabbitMqHeartbeatPublisherSettings(string appId, uint heartbeatSeconds = 2)
        {
            AppId = appId;
            HeartbeatSeconds = heartbeatSeconds;
        }

        public string AppId { get; }
        public uint HeartbeatSeconds { get; }
    }
}
