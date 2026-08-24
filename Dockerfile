# ==============================================================================
# HAWASSA UNIFIED CAMPUS EVENT MANAGEMENT SYSTEM (HUCEMS)
# ENTERPRISE DOCKER MULTI-STAGE PRODUCTION CONTAINER
# ==============================================================================

# STAGE 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["HawassaUnifiedCampusEventManagementSystem.csproj", "./"]
RUN dotnet restore "HawassaUnifiedCampusEventManagementSystem.csproj"

# Copy source code and publish release build
COPY . .
RUN dotnet publish "HawassaUnifiedCampusEventManagementSystem.csproj" -c Release -o /app/publish /p:UseAppHost=false

# STAGE 2: Runtime Environment
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Run HUCEMS
ENTRYPOINT ["dotnet", "HawassaUnifiedCampusEventManagementSystem.dll"]
