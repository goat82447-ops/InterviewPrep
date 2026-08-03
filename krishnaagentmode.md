# Agent mode — how to use

Agent mode turns a plain-English description into a whole new project: it
creates a folder, writes every file, then builds, fixes, and offers to run it.

- **Web Ask** = fast Gemini Flash.
- **Web Agent + local CLI** = Gemini 2.5 Flash (a stronger coder), or better
  automatically if you add a Groq / OpenRouter key.

---

## Option 1 — Local CLI (recommended)

Saves to your PC and builds + runs the project in one go.

1. Open a terminal and run:
   ```powershell
   & "$env:LOCALAPPDATA\Programs\kr7\kr7.exe"
   ```
   (or just type `kr7` in a new terminal — it is on your PATH)
2. Check the banner shows `AI : on` and `Model : Google Gemini · 2.5 Flash`.
   If it says OFF, add your API key first (see **Add your AI key** below).
3. Type a **project description**, e.g.
   `A C# .NET 8 REST API for a todo list with SQLite`.
4. Pick the **language** from the menu (1–4) if asked.
5. The agent creates a new folder on your **Desktop**, writes all files, then
   **builds → fixes → offers to run** it.

### Handy CLI commands
| Command | What it does |
| --- | --- |
| `/cwd C:\Projects` | Change where new projects are saved |
| `/env` | Show model, AI status, location, version |
| `/help` | List all commands |
| `/exit` or `Esc` | Quit |

---

## Option 2 — Web on your PC

Runs the same engine in a browser; files are written to **your** machine.

1. Start the server:
   ```powershell
   & "$env:LOCALAPPDATA\Programs\kr7\kr7.exe" --web
   ```
2. Open <http://localhost:5095/agent>.
3. Fill in **project name**, **location** (blank = Desktop), and **description**.
4. (Optional) pick a model, then click **▶ Build project**.
5. The `📁` line shows the exact folder the files were written to.

---

## Option 3 — Live site (kr7.onrender.com)

Files are generated on the server, so you **download** them.

1. Open <https://kr7.onrender.com/agent>.
2. Fill in name + description, then click **▶ Build project**.
3. Click the **⬇ Download project (.zip)** button.
4. Unzip on your PC and open in your editor.

---

## Add your AI key (one-time, if AI is OFF)

Set it once, then open a **new** terminal:

```powershell
setx GEMINI_API_KEY "your-free-gemini-key"
```

Or create `appsettings.Local.json` next to `kr7.exe`:

```json
{ "AiProviders": { "ApiKeys": { "gemini": "your-free-gemini-key" } } }
```

> Never share `appsettings.Local.json` — it holds your private key.

---

## What Agent mode is good at (and not)

**Good for:** brand-new projects, prototypes, boilerplate, learning, small–medium
apps (REST APIs, console apps, simple web apps). It builds a layered structure
(Controllers, Services, DTOs, Entities), adds auth/validation/Swagger when asked,
and gets the project compiling.

**Not for:** editing your existing repo (it only creates a new folder and never
writes into an existing project), multi-turn refactors on the same codebase, or
large production systems. For those, use an in-editor tool like Copilot.
