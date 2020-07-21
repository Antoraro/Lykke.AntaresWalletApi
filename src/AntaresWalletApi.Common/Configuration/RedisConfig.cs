namespace AntaresWalletApi.Common.Configuration
{
    public class RedisConfig
    {
        public string RedisConfiguration { get; set; }
        public string InstanceName { get; set; }
        public string OrderBooksCacheKeyPattern { get; set; }
    }
}
