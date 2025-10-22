# ================================
# Base image (runtime)
# ================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Render sẽ map tự động cổng này
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# ================================
# Build image (SDK)
# ================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project file và restore
COPY ["DirtyCoins.csproj", "./"]
RUN dotnet restore "./DirtyCoins.csproj"

# Copy toàn bộ source code
COPY . .

# Build project
RUN dotnet build "./DirtyCoins.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ================================
# Publish
# ================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DirtyCoins.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ================================
# Final image
# ================================
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "DirtyCoins.dll"]
