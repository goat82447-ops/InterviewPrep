# syntax=docker/dockerfile:1

# ---- Stage 1: build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (better layer caching)
COPY InterviewPrep.csproj ./
RUN dotnet restore InterviewPrep.csproj

# Copy the rest of the source and publish a Release build
COPY . ./
RUN dotnet publish InterviewPrep.csproj -c Release -o /app/publish

# Also cross-compile the self-contained Windows agent (kr7.exe) so the live
# site can serve it from /download-agent. This runs on the Linux SDK image via
# cross-compilation, so no 97 MB binary ever needs to be committed to git.
RUN dotnet publish InterviewPrep.csproj -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:AssemblyName=kr7 -o /app/agent

# ---- Stage 2: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./

# Place the freshly built Windows agent where the /download-agent route looks
# for it (content root is /app, so it reads /app/downloads/kr7.exe).
RUN mkdir -p downloads
COPY --from=build /app/agent/kr7.exe ./downloads/kr7.exe

# Render provides PORT; the app binds to http://0.0.0.0:$PORT in --web mode.
# AI keys (GROQ_API_KEY / OPENAI_API_KEY) are read from environment variables.
ENTRYPOINT ["dotnet", "InterviewPrep.dll", "--web"]
