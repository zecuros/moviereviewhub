using Microsoft.EntityFrameworkCore;
using WatchlistService.Data;
using WatchlistService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var movieServiceUrl =
    builder.Configuration["MovieService:BaseUrl"]
    ?? "http://localhost:5002/";

builder.Services.AddHttpClient<MovieReactiveClient>(client =>
{
    client.BaseAddress = new Uri(movieServiceUrl);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();