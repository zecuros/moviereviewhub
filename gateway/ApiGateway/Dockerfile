FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["gateway/ApiGateway/ApiGateway.csproj", "gateway/ApiGateway/"]
RUN dotnet restore "gateway/ApiGateway/ApiGateway.csproj"

COPY . .
RUN dotnet publish "gateway/ApiGateway/ApiGateway.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ApiGateway.dll"]