using MovieReviewHub.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddMovieReviewHubObservability("ApiGateway");

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseMovieReviewHubObservability("ApiGateway");

app.UseSwagger();
app.UseSwaggerUI();

app.MapReverseProxy();

app.Run();

// Allows WebApplicationFactory to exercise the actual gateway startup in tests.
public partial class Program { }
