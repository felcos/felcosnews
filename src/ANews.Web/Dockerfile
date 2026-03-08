FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/ANews.Web/ANews.Web.csproj", "src/ANews.Web/"]
COPY ["src/ANews.Infrastructure/ANews.Infrastructure.csproj", "src/ANews.Infrastructure/"]
COPY ["src/ANews.Domain/ANews.Domain.csproj", "src/ANews.Domain/"]
RUN dotnet restore "src/ANews.Web/ANews.Web.csproj"
COPY . .
WORKDIR "/src/src/ANews.Web"
RUN dotnet build "ANews.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ANews.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ANews.Web.dll"]
