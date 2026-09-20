FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["services/NotificationService/NotificationService.csproj", "services/NotificationService/"]
COPY ["shared/MovieReviewHub.Observability/MovieReviewHub.Observability.csproj", "shared/MovieReviewHub.Observability/"]
RUN dotnet restore "services/NotificationService/NotificationService.csproj"

COPY . .
RUN dotnet publish "services/NotificationService/NotificationService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.dll"]