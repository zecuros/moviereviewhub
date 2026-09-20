FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["services/AuthService/AuthService.csproj", "services/AuthService/"]
COPY ["shared/MovieReviewHub.Observability/MovieReviewHub.Observability.csproj", "shared/MovieReviewHub.Observability/"]
RUN dotnet restore "services/AuthService/AuthService.csproj"

COPY . .
RUN dotnet publish "services/AuthService/AuthService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AuthService.dll"]