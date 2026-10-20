# Build context is the repository root: docker build -f src/CareerPilot.API/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/CareerPilot.Domain/*.csproj src/CareerPilot.Domain/
COPY src/CareerPilot.Application/*.csproj src/CareerPilot.Application/
COPY src/CareerPilot.Infrastructure/*.csproj src/CareerPilot.Infrastructure/
COPY src/CareerPilot.API/*.csproj src/CareerPilot.API/
RUN dotnet restore src/CareerPilot.API/CareerPilot.API.csproj
COPY src/ src/
RUN dotnet publish src/CareerPilot.API/CareerPilot.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CareerPilot.API.dll"]
