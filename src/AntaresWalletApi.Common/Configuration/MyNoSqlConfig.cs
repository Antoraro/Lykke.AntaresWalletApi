namespace AntaresWalletApi.Common.Configuration
{
    public class MyNoSqlConfig
    {
        public string ReaderServiceUrl { get; set; }
        public string WriterServiceUrl { get; set; }
        public string PricesTableName { get; set; }
        public string CandlesTableName { get; set; }
        public string TickersTableName { get; set; }
    }
}
