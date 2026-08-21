using AuthService.Models;
using AuthService.Services;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AuthService.Tests;

public class JwtServiceTests
{
    private static IConfiguration CreateConfiguration(bool includeKey = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "MovieReviewHub",
            ["Jwt:Audience"] = "MovieReviewHubUsers"
        };

        if (includeKey)
        {
            values["Jwt:Key"] =
                "this-is-a-super-secret-key-for-movie-review-hub-auth-service";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void GenerateToken_ReturnsValidJwtToken()
    {
        var configuration = CreateConfiguration();

        var jwtService = new JwtService(configuration);

        var user = new User
        {
            Id = 1,
            Username = "uros",
            Email = "uros@test.com",
            Role = "User"
        };

        var token = jwtService.GenerateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));

        var handler = new JwtSecurityTokenHandler();

        Assert.True(handler.CanReadToken(token));
    }

    [Fact]
    public void GenerateToken_ContainsExpectedUserClaims()
    {
        var configuration = CreateConfiguration();

        var jwtService = new JwtService(configuration);

        var user = new User
        {
            Id = 7,
            Username = "testuser",
            Email = "test@test.com",
            Role = "User"
        };

        var token = jwtService.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Contains(
            jwtToken.Claims,
            claim =>
                claim.Type == ClaimTypes.NameIdentifier &&
                claim.Value == "7");

        Assert.Contains(
            jwtToken.Claims,
            claim =>
                claim.Type == ClaimTypes.Name &&
                claim.Value == "testuser");

        Assert.Contains(
            jwtToken.Claims,
            claim =>
                claim.Type == ClaimTypes.Email &&
                claim.Value == "test@test.com");

        Assert.Contains(
            jwtToken.Claims,
            claim =>
                claim.Type == ClaimTypes.Role &&
                claim.Value == "User");
    }

    [Fact]
    public void GenerateToken_WhenKeyIsMissing_ThrowsException()
    {
        var configuration = CreateConfiguration(false);

        var jwtService = new JwtService(configuration);

        var user = new User
        {
            Id = 1,
            Username = "test",
            Email = "test@test.com",
            Role = "User"
        };

        Assert.Throws<InvalidOperationException>(
            () => jwtService.GenerateToken(user));
    }
}