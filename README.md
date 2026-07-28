# InterviewPrep — Honest Interview Study Tool

A small .NET app that helps you **genuinely prepare** for technical and HR
interviews. You ask questions, practice answering out loud, and get instant
feedback plus strong model answers — so the knowledge is truly yours.

> This is a **study helper**, not an interview‑cheating tool. It is meant to be
> used *before* an interview to learn, not *during* one.

---

## How it works

The app runs in two modes from the same codebase:

| Mode | How to start | What you get |
| --- | --- | --- |
| **Web** (main) | `dotnet run -- --web` → http://localhost:5095 | A study dashboard in the browser |
| **Console** | `dotnet run` | A simple text‑based practice loop |

### Web pages

| Route | Page | What it does |
| --- | --- | --- |
| `/ask` | 💡 **Ask & Learn** | Type or speak any question → get a **3‑part answer**: short answer → full explanation → real example. |
| `/practice` | 🎓 **Practice** | Pick a topic, answer in your own words, and get scored against the key points. |
| `/mock` | 🎙️ **Mock interview** | A back‑and‑forth interview simulation with follow‑up questions. |
| `/drills` | ⚡ **Rapid drills** | Quick‑fire questions to build recall day by day. |
| `/plan` | 🗓️ **Study plan** | A suggested schedule across all topics. |

### The 3‑part answer format (Ask & Learn)

Every answer is structured so it's easy to learn and repeat:

1. **⚡ In short** — a 1–2 sentence direct answer (blue box).
2. **The explanation** — 4–6 numbered points (what it is, why it matters,
   how it works, trade‑offs).
3. **💡 Real example** — one concrete real‑world example (amber box).

### Where answers come from

```
Your question
     │
     ▼
Selected AI model (Groq / OpenAI) ──► real LLM answer (3‑part format)
     │  (if it fails / no quota)
     ▼
Any other AI model that has a key ──► real LLM answer
     │  (if no AI is reachable at all)
     ▼
Built‑in QuestionBank (offline) ────► closest stored topic, same format
```

So you always get an LLM answer when any model is available, and a sensible
offline fallback when none is.

### Question topics

Technical: **C#, .NET, SQL, OOP, REST, Azure, CI/CD, Production Support,
System Design, Kafka, Redis, Angular, JavaScript, Docker, Kubernetes,
Entity Framework**.

Non‑technical rounds: **HR Round, Behavioral (STAR method), Managerial /
Leadership**.

### Voice

The **🎤 Speak** buttons use the browser's **Web Speech API** — everything
happens locally in the browser, nothing is recorded or uploaded.

---

## Tech stack

| Layer | Technology |
| --- | --- |
| Language / runtime | **C# 12 on .NET 8** |
| Web server | **ASP.NET Core minimal API** (`WebApplication`, `MapGet`/`MapPost`) referenced via `Microsoft.AspNetCore.App` framework reference |
| UI | Server‑rendered **HTML + CSS** built as strings in `Web/*.cs` (no JS framework) |
| Voice input | **Web Speech API** (browser, no keys) |
| AI answers | **OpenAI‑compatible Chat Completions** — Groq (`llama-3.3-70b`) and OpenAI (`gpt-4o-mini`), Bearer‑token auth via `HttpClient` |
| Config | `appsettings.json` + git‑ignored `appsettings.Local.json` + environment variables |
| Data | Built‑in `QuestionBank` (in‑memory C#), no database |

### Project structure

```
InterviewPrep/
├─ Program.cs              # Entry point; console + web routing
├─ Infrastructure/
│  ├─ AppConfig.cs         # Multi‑provider AI config (Groq / OpenAI)
│  └─ ProjectPaths.cs      # Robust project‑root resolution
├─ Data/
│  └─ QuestionBank.cs      # All questions + model answers + key points
├─ Models/
│  └─ QuestionModels.cs    # Question, Level, Feedback records
├─ Services/
│  ├─ StudyAssistant.cs    # Ask & Learn: calls the LLM, 3‑part format
│  ├─ MockInterview.cs     # Mock interview flow
│  ├─ OpenAiCoach.cs       # Console AI coaching
│  └─ AnswerScorer.cs      # Scores your answer vs key points
└─ Web/
   ├─ AskPage.cs           # Ask & Learn page + model picker + mic
   ├─ PracticePage.cs      # Practice page
   ├─ MockPage.cs          # Mock interview page
   ├─ DrillsPage.cs        # Rapid drills page
   ├─ StudyPlanPage.cs     # Study plan page
   └─ AnswerFormat.cs      # Renders the 3‑part answer HTML + CSS
```

---

## Running it

```powershell
# Web dashboard (recommended)
dotnet run --project InterviewPrep -- --web
# then open http://localhost:5095

# Console practice
dotnet run --project InterviewPrep
```

### Configuring AI (optional)

The app works offline using the built‑in question bank. To enable open‑ended
LLM answers, add a key to **`appsettings.Local.json`** (git‑ignored — never
put keys in the committed `appsettings.json`):

```json
{
  "AiProviders": {
    "Options": [
      { "Id": "groq",   "ApiKey": "YOUR_GROQ_KEY" },
      { "Id": "openai", "ApiKey": "YOUR_OPENAI_KEY" }
    ]
  }
}
```

Or set environment variables: `GROQ_API_KEY`, `OPENAI_API_KEY`.

> **Note:** A new OpenAI account with no billing returns `429 insufficient_quota`.
> Add a payment method at platform.openai.com, or just use Groq (free tier).
