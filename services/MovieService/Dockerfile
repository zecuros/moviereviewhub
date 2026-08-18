FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["services/MovieService/MovieService.csproj", "services/MovieService/"]
RUN dotnet restore "services/MovieService/MovieService.csproj"

COPY . .
RUN dotnet publish "services/MovieService/MovieService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MovieService.dll"]