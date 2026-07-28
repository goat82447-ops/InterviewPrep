# Free AI models — where to get a key & the daily limits

This app can answer with a real AI model. All the models below have a **free tier** —
no credit card needed for most. Pick one, copy the key, paste it into
`appsettings.Local.json`, press **Ctrl+S**, then restart the app.

> The token/request numbers change over time. Always check the provider's own
> "Limits" page for the exact current values. The numbers here are a guide.

---

## 1. Groq (recommended — fastest & free) ⭐

- **Get a key:** https://console.groq.com/keys
- **Cost:** Free, no credit card.
- **Model used here:** `llama-3.3-70b-versatile`
- **Free daily limits (approx):**
  - ~30 requests per minute
  - ~1,000 requests per day
  - ~12,000 tokens per minute
  - ~100,000 tokens per day
- **Exact limits:** Groq Console → **Settings → Limits**
- **Reports remaining quota?** ✅ Yes — the dropdown & badge show live "tokens left".

## 2. Google Gemini (free tier, big limits)

- **Get a key:** https://aistudio.google.com/apikey
- **Cost:** Free tier, no credit card.
- **Model used here:** `gemini-2.0-flash`
- **Free daily limits (approx):**
  - ~15 requests per minute
  - ~1,500 requests per day
  - ~1,000,000 tokens per minute
- **Exact limits:** https://ai.google.dev/gemini-api/docs/rate-limits
- **Reports remaining quota?** ⚠️ Usually not in headers — badge may be blank.

## 3. OpenRouter (many free models)

- **Get a key:** https://openrouter.ai/keys
- **Cost:** Free models exist (look for names ending in `:free`).
- **Model used here:** `meta-llama/llama-3.3-70b-instruct:free`
- **Free limits (approx):**
  - ~20 requests per minute
  - ~50 requests per day on free models (higher if you add a small credit)
- **Exact limits:** https://openrouter.ai/docs/limits
- **Reports remaining quota?** ⚠️ Tracks *credits*, not token headers — badge may be blank.

## 4. Ollama (fully local — unlimited, no key, no internet)

- **Install:** https://ollama.com/download
- **Then run once:** `ollama pull llama3.1`
- **Cost:** Free forever, runs on YOUR PC. No daily limit.
- **Model used here:** `llama3.1` at `http://localhost:11434`
- **Trade-off:** Needs a decent PC (8 GB+ RAM); slower than Groq.
- **Reports remaining quota?** ➖ Not applicable (no limits).

## 5. OpenAI (paid — the same company as ChatGPT)

- **Get a key:** https://platform.openai.com/api-keys
- **Cost:** ❌ Not free — needs billing set up (separate from ChatGPT Plus).
- **Model used here:** `gpt-4o-mini`
- **Reports remaining quota?** ✅ Yes — shows tokens/requests left.
- **Note:** Your ChatGPT website/Plus subscription does **not** work here — the API
  is billed separately.

---

## How to turn it on

1. Open `InterviewPrep/appsettings.Local.json`.
2. Put your key in the matching provider's `ApiKey` field, e.g.:

   ```json
   {
     "Ai": {
       "Providers": {
         "groq": { "ApiKey": "gsk_your_real_key_here" }
       }
     }
   }
   ```

3. **Save the file (Ctrl+S)** — this is important; an unsaved key does nothing.
4. Restart the app:

   ```pwsh
   dotnet run --project .\InterviewPrep\InterviewPrep.csproj -- --web
   ```

5. Open http://localhost:5095/ask — the top badge should read **"AI: on"**.

> `appsettings.Local.json` is git-ignored, so your key is **never** committed.

---

## Quick comparison

| Provider   | Free?         | Daily limit (approx)      | Shows "tokens left" | Best for            |
|------------|---------------|---------------------------|---------------------|---------------------|
| Groq       | ✅ Yes        | ~1,000 req / 100k tokens  | ✅ Yes              | Fast, everyday use ⭐|
| Gemini     | ✅ Yes        | ~1,500 req                | ⚠️ Often no         | Big token budget    |
| OpenRouter | ✅ Free models| ~50 req                   | ⚠️ Credits only     | Trying many models  |
| Ollama     | ✅ Local      | Unlimited                 | ➖ N/A              | Offline / private   |
| OpenAI     | ❌ Paid       | Depends on billing        | ✅ Yes              | Highest quality     |
