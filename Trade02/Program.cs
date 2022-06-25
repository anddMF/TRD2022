using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Trade02.Business.services;
using Trade02.Infra.Cross;
using Trade02.Models.CrossCutting;

namespace Trade02
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    IConfiguration configuration = hostContext.Configuration;
                    AppSettings options = configuration.GetSection("AppConfiguration").Get<AppSettings>();

                    services.AddSingleton(options);
                    services.AddTransient<IEventsOutput, EventsOutput>();
                    services.AddTransient<IPortfolioService, PortfolioService>();

                    services.AddHttpClient("coinMarket", c =>
                    {
                        c.BaseAddress = new Uri("https://pro-api.coinmarketcap.com/");
                    });
                    services.AddHostedService<Worker>();
                });
    }
}
