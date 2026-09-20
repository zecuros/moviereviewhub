FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["services/WatchlistService/WatchlistService.csproj", "services/WatchlistService/"]
COPY ["shared/MovieReviewHub.Observability/MovieReviewHub.Observability.csproj", "shared/MovieReviewHub.Observability/"]
RUN dotnet restore "services/WatchlistService/WatchlistService.csproj"

COPY . .
RUN dotnet publish "services/WatchlistService/WatchlistService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WatchlistService.dll"]