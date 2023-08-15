using InfiniteTextGame.Lib;
using InfiniteTextGame.Lib.Migrations;
using InfiniteTextGame.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;

namespace InfiniteTextGame.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 配置服务
            builder.Logging.AddConsole(options => options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ");

            builder.Services.AddSqlite<ITGDbContext>(builder.Configuration.GetConnectionString("SQLiteDb"));

            var openAIApiKey = builder.Configuration.GetValue<string>("OpenAIApiKey");
            var openAIWebProxy = builder.Configuration.GetValue<string>("OpenAIWebProxy");
            if (string.IsNullOrEmpty(openAIApiKey)) { throw new InvalidOperationException("no openai api key in configuration"); }

            builder.Services.AddScoped<IAIClient, AIClient>(serverProvider =>
                new AIClient(openAIApiKey, openAIWebProxy)
            );

            builder.Services.AddRazorPages();

            // 配置并启动服务管线
            var app = builder.Build();
            app.Logger.LogInformation($"use openai api key: {openAIApiKey.Substring(0, 3)}...{openAIApiKey.Substring(openAIApiKey.Length - 3)}");
            if (!string.IsNullOrEmpty(openAIWebProxy))
                app.Logger.LogInformation($"use proxy: {openAIWebProxy}");

            //启动时迁移数据库（不建议正式使用）
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ITGDbContext>();
                dbContext.Database.Migrate();
            }

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