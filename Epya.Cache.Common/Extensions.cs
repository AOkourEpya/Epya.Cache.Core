using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Epya.Cache.Common
{
    /// <summary>
    /// Container for the cache extension methods.
    /// </summary>
    /// <remarks>
    /// An ASP.NET Core host wires the cache up with:
    /// <code>
    /// builder.Services.AddEpyaCache(builder.Configuration);
    /// ...
    /// app.UseEpyaCache();
    /// </code>
    /// and supplies the section in appsettings.json:
    /// <code>
    /// "CacheSettings": {
    ///   "IsEnabled": true,
    ///   "ServerAddress": "127.0.0.1",
    ///   "ReadPort": 5001,
    ///   "OthersPort": 5000,
    ///   "LogFilePath": "C:\\Logs\\CacheClient\\log.txt"
    /// }
    /// </code>
    /// </remarks>
    public static class Extensions
    {
        /// <summary>
        /// The configuration published by the last <see cref="AddEpyaCache"/> or
        /// <see cref="UseEpyaCache"/> call. Call sites that cannot take a constructor
        /// dependency - attributes, WinForms forms, static log helpers - reach it through
        /// <see cref="GetCacheConfigs"/> rather than reading this directly.
        /// </summary>
        public static CacheConfigDto Current { get; private set; }

        /// <summary>
        /// Reads the cache section out of the application configuration, maps it to
        /// <see cref="CacheConfigDto"/> and registers it as a singleton.
        /// </summary>
        public static IServiceCollection AddEpyaCache(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = CacheConfigDto.SectionName)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.TryAddSingleton(_Bind(configuration, sectionName));
            return services;
        }

        /// <summary>
        /// Activates the cache for the running application.
        /// </summary>
        /// <remarks>
        /// <see cref="IApplicationBuilder.ApplicationServices"/> is an already-built
        /// container, so a singleton can only be <i>taken</i> from it here -
        /// <see cref="AddEpyaCache"/> is what puts it there. When a host skipped that call
        /// the section is bound from <see cref="IConfiguration"/> instead, so that a
        /// Use-only setup still resolves through <see cref="GetCacheConfigs"/>.
        /// </remarks>
        public static IApplicationBuilder UseEpyaCache(
            this IApplicationBuilder app,
            string sectionName = CacheConfigDto.SectionName)
        {
            ArgumentNullException.ThrowIfNull(app);

            CacheConfigDto configs = app.ApplicationServices.GetService<CacheConfigDto>();

            if (configs == null)
            {
                IConfiguration configuration = app.ApplicationServices.GetService<IConfiguration>()
                    ?? throw new InvalidOperationException(
                        "No IConfiguration is available. Call services.AddEpyaCache(configuration) during startup.");

                configs = _Bind(configuration, sectionName);
            }

            Current = configs;
            return app;
        }

        /// <summary>
        /// Resolves the cache configuration from the container, falling back to the
        /// configuration published at startup when the provider has no registration
        /// (or when there is no provider at all).
        /// </summary>
        public static CacheConfigDto GetCacheConfigs(this IServiceProvider provider)
        {
            return provider?.GetService<CacheConfigDto>() ?? Current;
        }

        private static CacheConfigDto _Bind(IConfiguration configuration, string sectionName)
        {
            CacheConfigDto configs =
                configuration.GetSection(sectionName).Get<CacheConfigDto>() ?? new CacheConfigDto();

            Current = configs;
            return configs;
        }
    }
}
