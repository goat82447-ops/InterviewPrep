# Agent CLI — Complete Guide

A simple coding agent you run in your terminal. You give it a project name and a
short description, and it creates a **new project folder** on disk and writes all
the files (source code, `.csproj`/`package.json`, README).

It never writes into this app's own project — it only creates new folders you choose.

---

## 1. Run it (short way)

From the repo folder `c:\Users\v-kbandoju\MedicineReminder`:

```powershell
.\agent
```

That starts the CLI. You will see:

```
=== Agent CLI - build a new project ===
AI: on. Describe a project and it will be created on disk.
Default location: C:\Users\v-kbandoju\OneDrive - Microsoft\Desktop
Type 'quit' at any prompt to exit.

Project name:
```

Then answer three questions:

1. **Project name** — the new folder name, e.g. `TodoApp`
2. **Location** — press Enter for Desktop, or type a path like `C:\Projects`
3. **Describe the project** — e.g. `A C# .NET 8 console to-do list that saves tasks to a file`

The agent writes the files and prints the folder path. Type `quit` to exit.

The launcher is [agent.cmd](agent.cmd). It runs the published exe in
[agent-cli](agent-cli), so it starts fast (no rebuild each time).

---

## 2. Run `agent` from any folder (add to PATH)

So you can type `agent` in any terminal without `.\`:

1. Press the Windows key, type **environment variables**, open
   **Edit the system environment variables**.
2. Click **Environment Variables…**
3. Under **User variables**, select **Path** → **Edit** → **New**.
4. Add: `C:\Users\v-kbandoju\MedicineReminder`
5. Click **OK** on every dialog.
6. Open a **new** terminal and type:

```powershell
agent
```

---

## 3. Other ways to run

| What you want | Command |
| --- | --- |
| CLI agent (short) | `.\agent` |
| CLI agent (long, no publish needed) | `dotnet run --project .\InterviewPrep\InterviewPrep.csproj -- --agent` |
| Web version | `dotnet run --project .\InterviewPrep\InterviewPrep.csproj -- --web` then open http://localhost:5095 |
| Interview practice (default) | `dotnet run --project .\InterviewPrep\InterviewPrep.csproj` |

The web version has an **Agent mode** page at http://localhost:5095/agent that does
the same thing in the browser.

---

## 4. After you change the code — re-publish

The short `.\agent` command uses a published copy. If you edit any C# file,
rebuild that copy once:

```powershell
# stop any running copy first (it locks the exe)
Get-Process InterviewPrep,dotnet -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

dotnet publish .\InterviewPrep\InterviewPrep.csproj -c Release -o .\agent-cli
```

You do **not** need to re-publish if you use the long `dotnet run ... -- --agent`
command — that always builds fresh.

---

## 5. Add or change AI keys

The agent needs an AI key to work. Keys live in
`InterviewPrep\appsettings.Local.json` (this file is git-ignored — never commit keys).

Free options that work well:

- **Groq** — set `GROQ_API_KEY` (get a key at https://console.groq.com)
- **NVIDIA** — set `NVIDIA_API_KEY` (get a key at https://build.nvidia.com)

Example `appsettings.Local.json`:

```json
{
  "AiProviders": {
    "Default": "groq",
    "Options": [
      { "Id": "groq",   "ApiKey": "gsk_your_key_here" },
      { "Id": "nvidia", "ApiKey": "nvapi-your_key_here" }
    ]
  }
}
```

You can also set the keys as environment variables instead of the file.

After changing keys, re-publish (Section 4) so `.\agent` picks them up.

> Security: if a key was ever shown in a screen share or committed, rotate it
> (make a new one) at the provider site.

---

## 6. How to customize the agent

Everything the agent does lives in [CodeAgent.cs](InterviewPrep/Services/CodeAgent.cs).

### a) Change what kind of projects it builds (the instructions)

Open [CodeAgent.cs](InterviewPrep/Services/CodeAgent.cs) and find the **system
prompt** (the big text that tells the AI how to reply). Edit that text to change
behavior, for example:

- "Default to a **Python** project" instead of C#.
- "Always add a **.gitignore** and a **unit test** file."
- "Use **minimal APIs** for web projects."

### b) Change the default location

The default is the Desktop. In [CodeAgent.cs](InterviewPrep/Services/CodeAgent.cs)
the field `_defaultBase` sets this. Change it to any folder, e.g.:

```csharp
private readonly string _defaultBase = @"C:\Projects";
```

### c) Add a new AI model/provider

Providers are listed in [AppConfig.cs](InterviewPrep/Infrastructure/AppConfig.cs)
in the `byId` dictionary. Copy one entry, give it a new `Id`, `DisplayName`,
`Model`, and `BaseUrl` (must be an OpenAI-compatible `/chat/completions` URL),
then add the `Id` to the `order` list. Put its key in `appsettings.Local.json`.

### d) Keep the safety rules

Two methods keep the agent safe — **do not remove them**:

- `TryResolveSafe` — blocks writing outside the chosen project folder
  (stops `..` path escapes).
- `OverlapsAppProject` — blocks creating a project inside this app's own folder.

These are the reason the agent can't damage your real project.

After any code change, re-publish (Section 4).

---

## 7. Make your own custom agent (VS Code custom agent)

Separate from this CLI, VS Code lets you define **custom chat agents** — a named
assistant with its own instructions and allowed tools.

1. Create a file ending in `.agent.md`, for example
   `.github/agents/my-agent.agent.md` in your repo (or in your user prompts
   folder `c:\Users\v-kbandoju\AppData\Roaming\Code\User\prompts`).
2. Put YAML front matter at the top, then the instructions below it:

```markdown
---
description: Short summary of what this agent does and when to use it.
argument-hint: What input it expects, e.g. "a task to implement".
---

You are a focused .NET coding assistant.

- Always explain changes in one or two short sentences.
- Prefer editing existing files over creating new ones.
- Never touch files outside the folder you are asked to work in.
```

3. Save the file. The agent appears in the Copilot agent picker by its file name.
   Pick it and start chatting.

Tips:
- Keep the `description` clear — it decides when the agent is suggested.
- Write short, direct rules in the body (do this / don't do that).
- You can create many agents, each for a different job (tests, docs, review).

---

## 8. Troubleshooting

| Problem | Fix |
| --- | --- |
| `agent` not found in a new terminal | You added PATH — open a **new** terminal, or you forgot Section 2. |
| "AI: off" message | No key set. Add one (Section 5), then re-publish (Section 4). |
| Old behavior after editing code | Re-publish (Section 4). `.\agent` uses the published copy. |
| Exe is locked / publish fails | Stop running copies first (the Stop-Process line in Section 4). |
| Web app "connection refused" right after start | Wait a few seconds — it needs time to start listening. |

---

## 9. File map

| File | What it does |
| --- | --- |
| [agent.cmd](agent.cmd) | Short launcher — runs the published CLI |
| [agent-cli](agent-cli) | Published copy the launcher runs |
| [CodeAgent.cs](InterviewPrep/Services/CodeAgent.cs) | The agent logic + AI prompt + safety rules |
| [AppConfig.cs](InterviewPrep/Infrastructure/AppConfig.cs) | AI providers and key loading |
| [ProjectPaths.cs](InterviewPrep/Infrastructure/ProjectPaths.cs) | Finds config next to the exe or project |
| [Program.cs](InterviewPrep/Program.cs) | Chooses mode: default / `--web` / `--agent` |
| [AgentPage.cs](InterviewPrep/Web/AgentPage.cs) | The browser Agent-mode page |
