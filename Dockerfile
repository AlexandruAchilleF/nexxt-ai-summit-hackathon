# Production image. Used by Render (render.yaml); also works on Azure Container Apps, Railway, Fly.io.
#   docker build -t hackathon-starter .
#   docker run -p 8080:8080 -e AI__Provider=Anthropic -e AI__ApiKey=... hackathon-starter
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/HackathonStarter/HackathonStarter.csproj src/HackathonStarter/
# ReadyToRun: methods are compiled ahead of time, so the first visit to each page doesn't spend the (tiny, on a free
# plan) CPU on JIT compilation. Needs a runtime identifier, in restore and publish alike.
RUN dotnet restore src/HackathonStarter/HackathonStarter.csproj -r linux-x64 -p:PublishReadyToRun=true
COPY src/ src/
RUN dotnet publish src/HackathonStarter/HackathonStarter.csproj -c Release -o /app --no-restore \
    -r linux-x64 --self-contained false -p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# The platform terminates HTTPS and forwards plain HTTP to port 8080.
# Data is kept across restarts (seeded only when empty); reset it from Settings → "Reset demo data".
# SQLite lives inside the container, so a new deploy starts fresh: for persistent data use
# Database__Provider=Postgres + ConnectionStrings__Default (see docs/DEPLOY.md).
# Small-instance tuning: workstation GC (less memory and CPU than server GC on a fraction of a core) and no dynamic
# PGO instrumentation (it costs CPU while the app warms up; ReadyToRun code is already optimized enough for a demo).
ENV ASPNETCORE_URLS=http://+:8080 \
    Database__RecreateOnStartup=false \
    DOTNET_gcServer=0 \
    DOTNET_TieredPGO=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "HackathonStarter.dll"]
