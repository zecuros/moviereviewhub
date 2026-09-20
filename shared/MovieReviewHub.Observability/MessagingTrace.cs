using System.Diagnostics;
using System.Text;

namespace MovieReviewHub.Observability;

// RabbitMQ sends header strings back as UTF-8 bytes. Keep the W3C wire format shared.
public static class MessagingTrace
{
    public const string SourceName = "MovieReviewHub.Messaging";
    private static readonly ActivitySource Source = new(SourceName);

    public static Activity? StartPublish() =>
        Source.StartActivity("review-created publish", ActivityKind.Producer);

    public static Dictionary<string, object?> CreateHeaders()
    {
        var headers = new Dictionary<string, object?>();
        if (Activity.Current is { } current)
        {
            headers["traceparent"] = current.Id;
            if (current.TraceStateString is { } state)
                headers["tracestate"] = state;
        }
        return headers;
    }

    public static Activity? StartProcess(IDictionary<string, object?>? headers)
    {
        string? ReadHeader(string name) =>
            headers?.TryGetValue(name, out var value) == true
                ? value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value?.ToString()
                : null;
        ActivityContext.TryParse(ReadHeader("traceparent"), ReadHeader("tracestate"),
            isRemote: true, out var parent);
        return Source.StartActivity("review-created process", ActivityKind.Consumer, parent);
    }
}
