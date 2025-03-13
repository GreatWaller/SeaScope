
using SeaScope.Hubs;
using SeaScope.Services;

namespace SeaScope
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 添加服务

            //builder.Services.AddCors(options =>
            //{
            //    options.AddDefaultPolicy(policy =>
            //    {
            //        policy.AllowAnyOrigin()
            //              .AllowAnyMethod()
            //              .AllowAnyHeader();
            //    });
            //});
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    builder =>
                    {
                        builder.WithOrigins("http://127.0.0.1:5500",
                                "http://localhost:5500",
                                "http://localhost")
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    });
            });
            builder.Services.AddSingleton<IKafkaConsumerService, KafkaConsumerService>();
            builder.Services.AddSingleton<IProjectionService, ProjectionService>();

            builder.Services.AddSignalR();
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();
            app.UseCors();
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();
            app.MapHub<CameraHub>("/cameraHub");

            // 启动服务
            var kafkaService = app.Services.GetRequiredService<IKafkaConsumerService>();
            var projectionService = app.Services.GetRequiredService<IProjectionService>();
            var cts = new CancellationTokenSource();
            // 启动服务任务（不阻塞主线程）
            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStarted.Register(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.WhenAll(
                            kafkaService.StartAsync(cts.Token),
                            projectionService.StartAsync(cts.Token)
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Service startup failed: {ex.Message}");
                        cts.Cancel();
                    }
                });
            });

            // 优雅关闭
            lifetime.ApplicationStopping.Register(() =>
            {
                cts.Cancel();
            });

            app.Run();
        }
    }
}
