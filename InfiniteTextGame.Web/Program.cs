using InfiniteTextGame.Lib;
using InfiniteTextGame.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace InfiniteTextGame.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ���÷���
            builder.Logging.AddConsole(options => options.TimestampFormat = "[yyyy-MM-dd hh:mm:ss] ");

            builder.Services.AddSqlite<ITGDbContext>(builder.Configuration.GetConnectionString("SQLiteDb"));

            var openAIApiKey = builder.Configuration.GetValue<string>("OpenAIApiKey");
            var openAIWebProxy = builder.Configuration.GetValue<string>("OpenAIWebProxy");
            if (string.IsNullOrEmpty(openAIApiKey)) { throw new InvalidOperationException("no openai api key in configuration"); }

            builder.Services.AddScoped<IAIClient, AIClient>(serverProvider =>
                new AIClient(openAIApiKey, openAIWebProxy)
            );

            builder.Services.AddRazorPages();

            // ���ò������������
            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapRazorPages();

            app.Run();
        }
    }
}