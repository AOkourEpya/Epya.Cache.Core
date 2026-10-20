using Epya.Cache.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Epya.Cache.Server
{
    internal class Program
    {

        static void Main()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            using ServiceProvider services = new ServiceCollection()
                .AddEpyaCache(configuration)
                .AddSingleton(sp => new Engine(sp.GetRequiredService<CacheConfigDto>()))
                .BuildServiceProvider();

            Engine engine = services.GetRequiredService<Engine>();

            engine.StartServer();

            while (true)
            {
                string command = Console.ReadLine();
                if (command?.ToLower() == "stop")
                {
                    engine.StopServer();
                    break;
                }
            }
        }

    }
}
