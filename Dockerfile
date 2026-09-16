# ============================================================
# Stage 1: Build React SPA (ClientApp)
# ============================================================
FROM node:20-alpine AS react-build

WORKDIR /app/ClientApp

# Copy package files first to leverage cache
COPY src/Web/ClientApp/package*.json ./
RUN npm ci --silent

# Copy rest of frontend source
COPY src/Web/ClientApp/ ./
RUN npm run build


# ============================================================
# Stage 2: Build & Publish .NET 10 API
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build

WORKDIR /app

# Copy solution & project files first (cache restore layer)
COPY EbayClone.slnx ./
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/Domain/Domain.csproj               src/Domain/
COPY src/Application/Application.csproj     src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Web/Web.csproj                     src/Web/

# Restore NuGet packages
RUN dotnet restore src/Web/Web.csproj

# Copy all source code
COPY src/ src/

# Copy built React app into wwwroot (replaces SPA proxy)
COPY --from=react-build /app/ClientApp/build src/Web/wwwroot/

# Publish – skip NSwag & SPA build (already done in Stage 1)
RUN dotnet publish src/Web/Web.csproj \
    -c Release \
    -o /publish \
    --no-restore \
    /p:SkipNSwag=True \
    /p:SkipPublishRunWebpack=True


# ============================================================
# Stage 3: Runtime image (lean)
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Copy published output
COPY --from=dotnet-build /publish .

# Port exposed by ASP.NET Core
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "EbayClone.Web.dll"]
