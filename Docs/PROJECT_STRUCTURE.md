# Project Structure

What every file does and why it exists. This is the **InterviewPrep** app — an
honest interview study tool. Pick a topic, get a real technical question, answer
in your own words, and get instant AI feedback plus a strong model answer.

## How it runs

```
dotnet run --project .\InterviewPrep\InterviewPrep.csproj            # console practice
dotnet run --project .\InterviewPrep\InterviewPrep.csproj -- --web   # web app  (http://localhost:5095)
dotnet run --project .\InterviewPrep\InterviewPrep.csproj -- --agent # CLI coding agent
```

## Top-level layout

```
InterviewPrep/
  Program.cs              app entry point + web server routes
  InterviewPrep.csproj    project/build definition (.NET 8)
  appsettings.json        default settings (safe to commit; no secrets)
  appsettings.Local.json  YOUR API keys (gitignored; never commit)
  MODELS.md               guide to all AI models
  PROJECT_STRUCTURE.md    this file
  README.md               quick overview
  Dockerfile              container build for deploy
  render.yaml             Render.com deploy blueprint + env keys
  Data/                   the question bank
  Infrastructure/         config + path helpers
  Models/                 plain data shapes
  Services/               the "brains" (AI calls, scoring, agent)
  Web/                    the web pages (HTML built in C#)
```

## Root files — why each exists

| File | Why |
|---|---|
| Program.cs | Entry point. Reads flags (`--web`, `--agent`), loads config, and defines every web route (`/ask`, `/practice`, `/mock`, `/live`, `/settings`, ...). |
| InterviewPrep.csproj | Tells .NET how to build the app (target framework net8.0, output type). |
| appsettings.json | Non-secret defaults. Safe to commit. |
| appsettings.Local.json | Your real API keys. Gitignored so secrets never leave your machine. |
| Dockerfile | Builds the app into a container image for hosting. |
| render.yaml | Render deploy config: runtime, root dir, health check, and the env-var names for each API key (all `sync:false` = set in dashboard). |
| README.md | Short human intro to the project. |
| MODELS.md | Which AI model is best for speed / coding / interview answers. |

## Data/ — the question bank

| File | Why |
|---|---|
| Data/QuestionBank.cs | A built-in list of real interview questions. Each has a strong model answer, a "say it simply" version, and key points. Also stores AI-generated questions so the scorer can grade them by Id. |

## Infrastructure/ — config + paths

| File | Why |
|---|---|
| Infrastructure/AppConfig.cs | Loads all AI providers/models, reads API keys from settings + environment, shares one key across a provider group (all Groq models share `GROQ_API_KEY`, etc.), and picks the default model (Groq · Llama 3.3 70B). |
| Infrastructure/ProjectPaths.cs | Finds the real project root by walking up from the build output, so `appsettings.json` is found no matter how the app is launched. |

## Models/ — plain data shapes

| File | Why |
|---|---|
| Models/QuestionModels.cs | Simple record types like `Question` (the shape of a question/answer). No logic — just data passed around the app. |

## Services/ — the brains

| File | Why |
|---|---|
| Services/OpenAiCoach.cs | Talks to the AI provider (OpenAI-compatible chat API). Sends your answer, gets back a short critique + tip. Optional — only used when a key is set. |
| Services/AnswerScorer.cs | Scores your typed answer against the question's key points (works with no AI). |
| Services/MockInterview.cs | Runs a multi-question mock interview session. |
| Services/StudyAssistant.cs | Builds study plans / helps organize what to practice. |

## Web/ — the pages (HTML generated in C#)

Each page is a C# class that returns an HTML string. Program.cs maps a URL to
each one.

| File | Route | Why |
|---|---|---|
| Web/AskPage.cs | `/ask` | Ask any question, get an AI answer. |
| Web/PracticePage.cs | `/practice` | Practice a question, type an answer, get feedback. |
| Web/DrillsPage.cs | `/drills` | Quick-fire practice drills. |
| Web/MockPage.cs | `/mock` | Full mock-interview flow. |
| Web/LivePage.cs | `/live` | Webcam mock interview with voice (Web Speech API): reads the question aloud, listens to your spoken answer, then explains. |
| Web/StudyPlanPage.cs | `/plan` | Generates and shows a study plan. |
| Web/AnswerFormat.cs | — | Shared helper to format AI answers into clean HTML. |
| Web/WebChrome.cs | — | Shared page shell (top toolbar / comfort controls) used by all pages. |

## Where your API keys live

Keys are read (in priority order) from environment variables, then
`appsettings.Local.json` next to the app, then `%USERPROFILE%\.krishnaagent\`.
One key per provider group:

- `GROQ_API_KEY`
- `GEMINI_API_KEY`
- `OPENROUTER_API_KEY`
- `TOKENROUTER_API_KEY`
- `NVIDIA_API_KEY`
- `OPENAI_API_KEY`

## Request flow (web mode)

```
Browser → Program.cs route → Web/*Page.cs (builds HTML/handles POST)
        → Services/* (OpenAiCoach / AnswerScorer)
        → Infrastructure/AppConfig (which model + key)
        → AI provider API → answer back to the page → browser
```
