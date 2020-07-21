namespace AntaresWalletApi.Common.Configuration
{
    public class AppConfig
    {
        public ServicesConfig Services { get; set; }
        public MyNoSqlConfig MyNoSqlServer { get; set; }
        public RabbitMqConfig RabbitMq { get; set; }
        public MeConfig MatchingEngine { get; set; }
        public WalletApiConfig WalletApi { get; set; }
        public RedisConfig Redis { get; set; }
    }
}
