# Deploy (put it in production)

Blazor Server keeps a live connection (WebSocket) per user, so it needs a host that runs a .NET server.
**Vercel / Netlify / GitHub Pages won't work** (static or short-lived serverless only).

| | Render (recommended for the hackathon) | Azure App Service |
| --- | --- | --- |
| Setup | ~5 min, GitHub login, no card | ~15 min, Azure account (free with *Azure for Students*) |
| Deploys | automatic on every push to `main` | automatic on every push to `main` (GitHub Action) |
| Free tier catch | sleeps after ~15 min idle → first request ~1 min | needs credit or student account |
| Uses | `Dockerfile` + `render.yaml` | `.github/workflows/deploy-azure.yml` |

Do it in the **first hour** of the hackathon, then every push to `main` is live. Share the URL with mentors.

---

## Option A: Render (5 minutes)

1. Go to <https://render.com> → **Sign up with GitHub** (one teammate is enough; give the repo access when asked).
2. **New → Blueprint** → select this repository → Render reads `render.yaml`.
3. Fill in the environment variables it asks for (all optional, leave empty for mock/replay mode):
   - `AI__Provider`: `Anthropic`, `Bedrock`, `OpenAI` (also Featherless/Groq/Ollama-compatible) or `AzureOpenAI`.
     `Bedrock` (added for TRACE) uses `AI__Region` (default `us-west-2`) and takes its credentials only from the AWS
     default chain (on a host like Render: the standard AWS environment variables, set in its dashboard); they are
     never in `render.yaml`, and `AI__ApiKey` is not used
   - `AI__Model`: e.g. `claude-sonnet-5-5`, or the Featherless model id
   - `AI__ApiKey`: the key
   - `AI__Endpoint`: only for OpenAI-compatible / Azure (e.g. `https://api.featherless.ai/v1`)
4. **Apply**. The first build takes ~5 minutes. You get `https://hackathon-starter-xxxx.onrender.com`.
   - `Security__AccessCode`: recommended once a real key is set: the public URL then asks for this code once
     (share it with the jury); stops strangers from spending your AI credits (the TRACE public instance runs without it). `Security__ApiKey` enables `/api` with `X-Api-Key`.
5. Check `https://…onrender.com/health` returns `ok`, then open the app.

Later changes: **just push to `main`**. Render rebuilds and redeploys (~3–5 min). Env vars: service → *Environment*.

**Before the demo:** open the URL 2–3 minutes before going on stage (free instances sleep when idle). Or keep it awake:
GitHub → *Settings → Secrets and variables → Actions → Variables* → `APP_URL` = your Render URL; the `schedule` in
`.github/workflows/keep-awake.yml` (every 10 minutes) is on for the hackathon; comment it out afterwards.

**Data:** the image runs with `Database__RecreateOnStartup=false`, but on Render's free plan the container disk is
temporary, so SQLite still starts fresh (re-seeded) on every deploy/restart. Admins can reset the demo data at any time
from **Settings → Reset demo data**.
For persistent data: create a free PostgreSQL (Render or [Neon](https://neon.tech)) and set
`Database__Provider=Postgres`, `ConnectionStrings__Default=Host=...;Database=...;Username=...;Password=...;SSL Mode=Require`.

---

## Option B: Azure App Service

1. Students: activate **Azure for Students** (<https://azure.microsoft.com/free/students>, university email, no card).
2. Azure Portal → **Create a resource → Web App**:
   - Publish: **Code** · Runtime stack: **.NET 10 (LTS)** · Operating System: **Linux**
   - Region: e.g. *West Europe* · Pricing plan: **Basic B1** (Free F1 works for testing but has limits)
3. Web App → **Settings → Configuration → General settings** → **Web sockets: On** → Save. *(Required by Blazor Server.)*
4. Web App → **Settings → Environment variables** → add:
   - `ConnectionStrings__Default` = `Data Source=/home/app.db` *(the app folder is read-only after deploy; `/home` is writable)*
   - *(optional)* `Database__RecreateOnStartup` = `true` to re-seed on every restart. By default data in `/home/app.db` is
     kept across restarts; use **Settings → Reset demo data** for a clean demo.
   - *(recommended)* `Security__AccessCode` = a code you share with the jury (the TRACE public instance runs without it)
   - `AI__Provider`, `AI__Model`, `AI__ApiKey`, `AI__Endpoint` as needed
5. Web App → **Overview → Download publish profile**.
6. GitHub repo → **Settings → Secrets and variables → Actions**:
   - *Secrets* → New: `AZURE_WEBAPP_PUBLISH_PROFILE` = paste the whole downloaded file
   - *Variables* → New: `AZURE_WEBAPP_NAME` = your Web App name
   *(If GitHub says the publish profile is disabled: Web App → Configuration → General settings → **SCM Basic Auth Publishing Credentials: On**.)*
7. GitHub → **Actions → Deploy to Azure → Run workflow** (or just push to `main`).
8. Open `https://<app-name>.azurewebsites.net/health` → `ok`.

---

## Hackathon-day rules

- **`main` = production.** Work on branches for risky changes; merge when the CI check is green.
- **CI** (`.github/workflows/ci.yml`) builds with warnings-as-errors and runs the tests on every push. Red = don't merge.
- **Secrets only in the host's environment variables**, never in the repo.
- **Code freeze before recording the video**; after that, only demo-path fixes. (Historical: for TRACE it was Thursday
  29 Oct 2026, 08:00, `docs/PLAN.md`.)
- **Plan B, always:** the presenting laptop can run everything offline on the mock, which calls the real tools:
  ```bash
  AI__Provider=None dotnet run --project src/HackathonStarter -c Release
  ```
  (Windows PowerShell: `$env:AI__Provider="None"; dotnet run --project src/HackathonStarter -c Release`)
  Do not force `AI__AgentMode=Replay` for data-changing steps: replay shows the recorded steps without running the tools
  (only a replayed approval runs, with the recording's masked arguments). Without a model a recording is still replayed
  for a similar question before the mock runs, so keep only read-only questions in `Recordings/`.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Page loads, then "Reconnecting…" forever | WebSockets disabled (Azure: step 3) or a proxy blocking them |
| Render: "No open ports detected" | `PORT` env var must be `8080` (set by `render.yaml`) |
| Azure: 500 at startup, `SQLite Error 14` | `ConnectionStrings__Default=Data Source=/home/app.db` (step 4) |
| Agents always show "Mock" | `AI__Provider` / `AI__ApiKey` missing or wrong: check the Settings page in the app. The chat banner says why it fell back |
| "Rate limit" fallback | `Security__AgentRunsPerMinute` / `Security__DailyLiveRunBudget` reached: raise them |
| Everyone gets the access page | Expected when `Security__AccessCode` is set. `/health` stays open |
| Deploy works locally but not on the host | Check the host logs (Render: *Logs* tab · Azure: *Log stream*) |
