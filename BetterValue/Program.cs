using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BetterValue;
using BetterValue.Service;

namespace BetterValue
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddScoped<ICsvParserService, CsvParserService>();
            builder.Services.AddScoped<ITeamStatisticsAggregator, TeamStatisticsAggregator>();
            builder.Services.AddScoped<IPoissonService, PoissonService>();


            await builder.Build().RunAsync();
        }
    }
}
