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

# ---- Stage 2: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./

# Render provides PORT; the app binds to http://0.0.0.0:$PORT in --web mode.
# AI keys (GROQ_API_KEY / OPENAI_API_KEY) are read from environment variables.
ENTRYPOINT ["dotnet", "InterviewPrep.dll", "--web"]
