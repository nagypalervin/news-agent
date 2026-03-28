FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY NewsAgent.sln ./
COPY src/NewsAgent/NewsAgent.csproj src/NewsAgent/
COPY tests/NewsAgent.Tests/NewsAgent.Tests.csproj tests/NewsAgent.Tests/
RUN dotnet restore

# Copy all source code and publish
COPY src/ src/
COPY tests/ tests/
RUN dotnet publish src/NewsAgent -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app

RUN apk add --no-cache tzdata && mkdir -p /config /output

COPY --from=build /app .

ENTRYPOINT ["dotnet", "NewsAgent.dll"]
