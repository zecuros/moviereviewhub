using System.Diagnostics;
using System.Text;
using MovieReviewHub.Observability;

namespace Observability.Tests;

[Collection("Telemetry listeners")]
public class MessagingTraceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Headers_LinkConsumerToProducer_WithOptionalStateAndUtf8Encoding(bool state, bool bytes)
    {
        using var listener = Listen();
        using var producer = MessagingTrace.StartPublish();
        Assert.NotNull(producer);
        if (state) producer.TraceStateString = "vendor=value";
        var headers = MessagingTrace.CreateHeaders();
        Assert.Equal(producer.Id, headers["traceparent"]);
        Assert.Equal(state, headers.ContainsKey("tracestate"));
        if (bytes)
            headers = headers.ToDictionary(p => p.Key, p => (object?)Encoding.UTF8.GetBytes((string)p.Value!));
        using var consumer = MessagingTrace.StartProcess(headers);
        Assert.NotNull(consumer);
        Assert.Equal(ActivityKind.Consumer, consumer.Kind);
        Assert.Equal("review-created process", consumer.OperationName);
        Assert.Equal(ActivityKind.Producer, producer.Kind);
        Assert.Equal("review-created publish", producer.OperationName);
        Assert.Equal(producer.TraceId, consumer.TraceId);
        Assert.Equal(producer.SpanId, consumer.ParentSpanId);
        Assert.Equal(producer.TraceStateString, consumer.TraceStateString);
        Assert.True(consumer.HasRemoteParent);
    }

    [Fact]
    public void Headers_WithoutActivity_AreEmpty()
    {
        var previous = Activity.Current;
        try
        {
            Activity.Current = null;
            Assert.Empty(MessagingTrace.CreateHeaders());
        }
        finally { Activity.Current = previous; }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public void MissingOrInvalidParent_DoesNotPreventProcessing(string? parent)
    {
        using var listener = Listen();
        var headers = parent is null ? null : new Dictionary<string, object?> { ["traceparent"] = parent };
        using var consumer = MessagingTrace.StartProcess(headers);
        Assert.NotNull(consumer);
        Assert.Equal(ActivityKind.Consumer, consumer.Kind);
        Assert.False(consumer.HasRemoteParent);
    }

    [Fact]
    public void NullHeaderValues_AreAccepted()
    {
        using var listener = Listen();
        using var consumer = MessagingTrace.StartProcess(new Dictionary<string, object?>
        {
            ["traceparent"] = null, ["tracestate"] = null
        });
        Assert.NotNull(consumer);
        Assert.False(consumer.HasRemoteParent);
    }

    private static ActivityListener Listen()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MessagingTrace.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}

// Activity listeners are process-wide; do not let the tests observe each other's spans.
[CollectionDefinition("Telemetry listeners", DisableParallelization = true)]
public class TelemetryListenerCollection;
