using MovieReviewHub.Observability;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace ReviewService.Messaging;

public class RabbitMqPublisher
{
    private const string QueueName = "review-created";

    private readonly IConfiguration _configuration;

    public RabbitMqPublisher(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task PublishReviewCreatedAsync(ReviewCreatedEvent reviewEvent)
    {
        using var activity = MessagingTrace.StartPublish();
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMq:Host"] ?? "localhost",
            UserName = _configuration["RabbitMq:Username"] ?? "guest",
            Password = _configuration["RabbitMq:Password"] ?? "guest"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );

        var json = JsonSerializer.Serialize(reviewEvent);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties { Headers = MessagingTrace.CreateHeaders() };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: QueueName,
            mandatory: false,
            basicProperties: properties,
            body: body
        );
    }
}
