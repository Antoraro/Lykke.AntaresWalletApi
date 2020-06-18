namespace AntaresWalletApi.Common.Configuration
{
    public class AppConfig
    {
        public ServicesConfig Services { get; set; }
        public TokenConfig Token { get; set; }
    }

    public class TokenConfig
    {
        public string Auth { get; set; }
    }
}
