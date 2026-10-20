using Epya.Cache.Common;

namespace ClientTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddEpyaCache(builder.Configuration);
            builder.Services.AddControllers();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseEpyaCache();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
