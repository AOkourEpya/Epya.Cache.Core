namespace Epya.Cache.Common
{
    /// <summary>
    /// Strongly typed view of the cache settings section in appsettings.json.
    /// Replaces the &lt;cacheSettings&gt; NameValueCollection section that used to be
    /// read through ConfigurationManager.
    /// </summary>
    public class CacheConfigDto
    {
        /// <summary>Default appsettings.json section name.</summary>
        public const string SectionName = "CacheSettings";

        public bool IsEnabled { get; set; }
        public string ServerAddress { get; set; }
        public int ReadPort { get; set; }
        public int OthersPort { get; set; }
        public string LogFilePath { get; set; }

        /// <summary>True once every value needed to reach the cache server is present.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ServerAddress) && ReadPort > 0 && OthersPort > 0;

        public bool IsEnabledAndConfigured => IsEnabled && IsConfigured;

        public bool IsLoggingEnabled => !string.IsNullOrWhiteSpace(LogFilePath);
    }
}
