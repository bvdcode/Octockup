namespace Octockup.Server.Database
{
    public class DatabaseSettings
    {
        public int Port { get; set; }
        public string Host { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; 
    }
}