namespace AntaresWalletApi.Common.Configuration
{
    public class RabbitMqConfig
    {
        public RabbitMqConnection Candles { get; set; }
    }

    public class RabbitMqConnection
    {
        public string ConnectionString { get; set; }
        public string ExchangeName { get; set; }
    }
}
