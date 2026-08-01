# AI Models Guide

All models available in the app, grouped by what they are best at.
The default model is **Groq · Llama 3.3 70B (free)**.

## Which are free

| Provider group | Free? | Notes |
|---|---|---|
| Groq (all 3) | Yes (free tier) | Extremely fast |
| Gemini (all 3) | Yes (free tier) | Generous free quota |
| OpenRouter (all `:free`) | Yes | Rate-limited; auto-fallback when one runs out |
| TokenRouter · Kimi K3 (free) | Yes | Only the `-free` one is free |
| Other TokenRouter (Claude 5, GPT 5.6, Grok, ...) | Paid | Pay-per-token from your TokenRouter balance |
| NVIDIA (all) | Yes (free credits) | Free API credits |
| Ollama | Yes (local) | Needs Ollama running on your PC |
| OpenAI GPT-4o mini | Paid | Needs your OpenAI billing |

## Best for FAST response

Groq wins by a wide margin (special LPU hardware).

1. Groq · Llama 3.3 70B (free) — top pick, free + fastest
2. Groq · GPT-OSS 120B (free) — fast + smarter
3. NVIDIA · Nemotron Nano 9B — fastest NVIDIA
4. Gemini · 2.0 Flash (free) — fast + free
5. TokenRouter · Gemini 3.6 Flash (paid, fast)

## Best for CODING

1. TokenRouter · Claude Opus 5 — best overall (paid)
2. TokenRouter · Kimi K2.7 Code — coding-tuned (paid)
3. TokenRouter · GPT 5.6 Sol — strong (paid)
4. Free: OpenRouter · DeepSeek V3, OpenRouter · Qwen 2.5 Coder 32B, OpenRouter · DeepSeek R1 (reasoning)
5. Groq · GPT-OSS 120B (free) — decent + very fast

## Best for INTERVIEW ANSWERS (clear, well-structured)

1. TokenRouter · Claude Sonnet 5 — best balance of quality + speed (paid)
2. TokenRouter · Claude Opus 5 — deepest answers (paid, slower)
3. Gemini · 2.5 Pro — best FREE quality
4. Groq · Llama 3.3 70B (free) — best FREE + fast combo
5. TokenRouter · Grok 4.5 — good conversational tone (paid)

## Recommendation

- Daily practice, free & fast: **Groq · Llama 3.3 70B** (the default)
- Best free answers: **Gemini · 2.5 Pro**
- Best free coding: **OpenRouter · DeepSeek V3** or **Qwen 2.5 Coder**
- Top quality (paid): **TokenRouter · Claude Sonnet 5**

## Full model list

### Groq (free, fastest)
- Groq · Llama 3.3 70B (free) — `llama-3.3-70b-versatile` — **default**
- Groq · Compound (web-connected) — `groq/compound`
- Groq · GPT-OSS 120B (free) — `openai/gpt-oss-120b`

### Google Gemini (free tier)
- Gemini · 2.0 Flash (free) — `gemini-2.0-flash`
- Gemini · 2.5 Pro (best quality) — `gemini-2.5-pro`
- Gemini · 2.5 Flash (fast) — `gemini-2.5-flash`

### OpenRouter (free)
- Llama 3.3 70B (free) — `meta-llama/llama-3.3-70b-instruct:free`
- Google Gemma 3 27B (free) — `google/gemma-3-27b-it:free`
- DeepSeek V3 (free, coding) — `deepseek/deepseek-chat-v3-0324:free`
- DeepSeek R1 (free, reasoning) — `deepseek/deepseek-r1:free`
- Qwen 2.5 Coder 32B (free, coding) — `qwen/qwen-2.5-coder-32b-instruct:free`
- Mistral Small 3.1 24B (free) — `mistralai/mistral-small-3.1-24b-instruct:free`
- OpenAI gpt-oss 20B (free) — `openai/gpt-oss-20b:free`
- Ling 3.0 Flash (free) — `inclusionai/ling-3.0-flash:free`
- Laguna S 2.1 (free, coding) — `poolside/laguna-s-2.1:free`
- Cohere North Mini Code (free, coding) — `cohere/north-mini-code:free`

### TokenRouter (Kimi K3 free; rest paid)
- Kimi K3 (free) — `moonshotai/kimi-k3-free`
- Kimi K3 (long-context) — `moonshotai/kimi-k3`
- Kimi K2.7 Code — `moonshotai/kimi-k2.7-code`
- Claude Sonnet 5 — `anthropic/claude-sonnet-5`
- Claude Opus 5 (best) — `anthropic/claude-opus-5`
- GPT 5.6 (Sol) — `openai/gpt-5.6-sol`
- GPT 5.6 (Luna, cheap) — `openai/gpt-5.6-luna`
- Gemini 3.6 Flash — `google/gemini-3.6-flash`
- Grok 4.5 — `x-ai/grok-4.5`
- DeepSeek V4 Flash — `deepseek/deepseek-v4-flash-0731`
- GLM 5.2 — `z-ai/glm-5.2`
- Qwen 3.7 Plus — `qwen/qwen3.7-plus`
- Mistral Small — `mistralai/mistral-small-2603`

### NVIDIA (free credits)
- Nemotron Super 49B (best, fast) — `nvidia/llama-3.3-nemotron-super-49b-v1.5`
- Nemotron Nano 9B (fastest) — `nvidia/nvidia-nemotron-nano-9b-v2`
- Llama 3.1 70B (bigger, slower) — `meta/llama-3.1-70b-instruct`
- DeepSeek V4 Flash — `deepseek-ai/deepseek-v4-flash`

### Local / Other
- Ollama · Llama 3.1 (local, no key) — `llama3.1`
- OpenAI · GPT-4o mini (ChatGPT API, paid) — `gpt-4o-mini`

## API keys (set once)

Each provider group shares one key. Set via environment variable or `appsettings.Local.json`:

- `GROQ_API_KEY`
- `GEMINI_API_KEY`
- `OPENROUTER_API_KEY`
- `TOKENROUTER_API_KEY`
- `NVIDIA_API_KEY`
- `OPENAI_API_KEY`
