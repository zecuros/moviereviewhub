FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["services/ReviewService/ReviewService.csproj", "services/ReviewService/"]
RUN dotnet restore "services/ReviewService/ReviewService.csproj"

COPY . .
RUN dotnet publish "services/ReviewService/ReviewService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ReviewService.dll"]