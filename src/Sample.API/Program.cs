using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Sample.API.Behaviors;
using Sample.API.Consumers;
using Sample.API.Data;
using Sample.API.Models.Command;
using Sample.API.Outbox;
using Sample.API.Services;
using Sample.API.Services.Interfaces;
using Sample.API.Settings;
using Sample.API.Validators;

namespace Sample.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();

        // Configuration
        builder.Services.AddOptions();
        builder.Services.Configure<RabbitConfig>(builder.Configuration.GetSection("RabbitMQ"));
        builder.Services.Configure<OutboxConfig>(builder.Configuration.GetSection("Outbox"));

        // EF Core contexts (SQL Server)
        builder.Services.AddDbContext<WriteDbContext>(opts => opts.UseSqlServer(builder.Configuration.GetConnectionString("WriteDb")));
        builder.Services.AddDbContext<ReadDbContext>(opts => opts.UseSqlServer(builder.Configuration.GetConnectionString("ReadDb")));

        // MediatR + FluentValidation
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreateCustomerCommand>());
        builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerValidator>();

        // PipelineBehavior to automatically run FluentValidation before handlers
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // RabbitMQ connection/publisher
        builder.Services.AddSingleton<IRabbitMqConnection>(sp =>
        {
            var cfg = sp.GetRequiredService<IOptions<RabbitConfig>>().Value;
            var factory = new ConnectionFactory
            {
                HostName = cfg.Host,
                Port = cfg.Port,
                UserName = cfg.User,
                Password = cfg.Password,
                AutomaticRecoveryEnabled = true
            };

            return new RabbitMqConnection(factory);
        });
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        // Hosted workers
        builder.Services.AddHostedService<OutboxProcessor>();    // reads outbox and publishes to RabbitMQ
        builder.Services.AddHostedService<ReadModelConsumer>();  // consumes events and updates read DB

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Apply migrations on startup (for demo)
        using (var scope = app.Services.CreateScope())
        {
            var writeDb = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
            var readDb = scope.ServiceProvider.GetRequiredService<ReadDbContext>();

            writeDb.Database.Migrate();
            readDb.Database.Migrate();
        }

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseRouting();

        app.MapControllers();
        app.Run();
    }
}