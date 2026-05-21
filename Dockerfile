FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props WordGame.sln ./
COPY src/WordGame.Domain/WordGame.Domain.csproj src/WordGame.Domain/
COPY src/WordGame.Application/WordGame.Application.csproj src/WordGame.Application/
COPY src/WordGame.Infrastructure/WordGame.Infrastructure.csproj src/WordGame.Infrastructure/
COPY src/WordGame.Web/WordGame.Web.csproj src/WordGame.Web/
COPY tests/WordGame.UnitTests/WordGame.UnitTests.csproj tests/WordGame.UnitTests/
COPY tests/WordGame.IntegrationTests/WordGame.IntegrationTests.csproj tests/WordGame.IntegrationTests/
RUN dotnet restore WordGame.sln

COPY . .
RUN dotnet restore src/WordGame.Web/WordGame.Web.csproj
RUN dotnet publish src/WordGame.Web/WordGame.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WordGame.Web.dll"]
