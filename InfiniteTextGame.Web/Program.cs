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
            var type = builder.Configuration.GetValue<string>("Type");
            switch (type.ToLower())
            {
                case "openai":
                    var openAIApiKey = builder.Configuration.GetValue<string>("OpenAIApiKey");
                    var openAIWebProxy = builder.Configuration.GetValue<string>("OpenAIWebProxy");
                    if (string.IsNullOrEmpty(openAIApiKey)) { throw new InvalidOperationException("no openai api key in configuration"); }

                    builder.Services.AddScoped(serverProvider =>
                        new AIClient(openAIApiKey, openAIWebProxy)
                    );
                    break;
                case "azure":
                    var azureApiKey = builder.Configuration.GetValue<string>("AzureApiKey");
                    var resourceName = builder.Configuration.GetValue<string>("ResourceName");
                    var deploymentId = builder.Configuration.GetValue<string>("DeploymentId");
                    if (string.IsNullOrEmpty(azureApiKey)) { throw new InvalidOperationException("no azure api key in configuration"); }
                    if (string.IsNullOrEmpty(resourceName)) { throw new InvalidOperationException("no resource name in configuration"); }
                    if (string.IsNullOrEmpty(deploymentId)) { throw new InvalidOperationException("no deployment id in configuration"); }

                    builder.Services.AddScoped(serverProvider =>
                        new AIClient(azureApiKey, resourceName, deploymentId)
                    );
                    break;
                default:
                    throw new InvalidOperationException("type must be OpenAI or Azure");
            }

            builder.Services.AddSqlite<ITGDbContext>(builder.Configuration.GetConnectionString("SQLiteDb"));

            builder.Services.AddRazorPages();

            // 配置并启动服务管线
            var app = builder.Build();
            app.Logger.LogInformation($"starting {type} service");

            if (!app.Environment.IsDevelopment())
            {
                //启动时迁移数据库（建议仅在测试版使用）
                using (var scope = app.Services.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ITGDbContext>();
                    dbContext.Database.Migrate();
                    app.Logger.LogInformation($"database migrated");
                }

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