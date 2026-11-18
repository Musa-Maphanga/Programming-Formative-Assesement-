# =============================================================================
# RaceDay web front end (Part 3)
#
# A two-stage build. The SDK image compiles and publishes the application, then
# only the published output is copied into the much smaller ASP.NET runtime
# image. The SDK, the source and the NuGet cache never reach the final image.
# =============================================================================

# ----------------------------------------------------------------- build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# The project file is copied on its own first so that Docker can cache the
# restore layer. Editing a .cshtml file then does not trigger a fresh restore.
COPY ["RaceDay.Web/RaceDay.Web.csproj", "RaceDay.Web/"]
RUN dotnet restore "RaceDay.Web/RaceDay.Web.csproj"

COPY . .
RUN dotnet publish "RaceDay.Web/RaceDay.Web.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# --------------------------------------------------------------- runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Kestrel listens on 8080 so the container does not need root to bind a port.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Running as a non-root user limits what a compromised process could reach.
RUN adduser --disabled-password --gecos "" --uid 5678 raceday \
    && chown -R raceday /app
USER raceday

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD ["dotnet", "--info"]

ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
