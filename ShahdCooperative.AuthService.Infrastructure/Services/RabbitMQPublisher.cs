using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Infrastructure.Services;

public class RabbitMQPublisher : IMessagePublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMQPublisher> _logger;
    private const string ExchangeName = "shahdcooperative.events";

    public RabbitMQPublisher(IConfiguration configuration, ILogger<RabbitMQPublisher> logger)
    {
        _logger = logger;

        var rabbitMQConfig = configuration.GetSection("RabbitMQ");
        var factory = new ConnectionFactory
        {
            HostName = rabbitMQConfig["Host"] ?? "localhost",
            Port = int.Parse(rabbitMQConfig["Port"] ?? "5672"),
            UserName = rabbitMQConfig["Username"] ?? "guest",
            Password = rabbitMQConfig["Password"] ?? "guest",
            VirtualHost = rabbitMQConfig["VirtualHost"] ?? "/",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        try
        {
            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            // Declare exchange
            _channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false
            ).GetAwaiter().GetResult();

            _logger.LogInformation("RabbitMQ connection established successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish RabbitMQ connection");
            throw;
        }
    }

    public async Task PublishAsync<T>(T message, string routingKey) where T : class
    {
        try
        {
            var messageBody = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(messageBody);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body
            );

            _logger.LogInformation("Published message to RabbitMQ: {RoutingKey}", routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to RabbitMQ: {RoutingKey}", routingKey);
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.CloseAsync().GetAwaiter().GetResult();
        _channel?.Dispose();
        _connection?.CloseAsync().GetAwaiter().GetResult();
        _connection?.Dispose();
    }
}
