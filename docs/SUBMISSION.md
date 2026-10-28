# Submission playbook

What judges receive: **source code**, a **practical demonstration of the agent**, and a **video pitch of up to
3 minutes** (problem, what makes the solution unique, how the architecture works).

## Timeline (work backwards from the deadline)

| When | What |
| --- | --- |
| Day 1, after kick-off | Pick the idea in ≤ 45 min. Write the one-sentence pitch and the demo script **first**. |
| Day 1, first 15 min of coding | `SiteConfig.cs`, `tools/NewFeature` for your entities, seed data. **Deploy** (Azure App Service / Docker) and share the URL. |
| Day 1, afternoon | Build the agent: tools on your services → `ToolAgent` → `<AgentChat />`. Show it at the mentor checkpoint. |
| Day 1, evening | Real model connected. Rehearse the demo with `"AI": { "Record": true }` to record runs. Commit `Recordings/`. |
| Night | Polish only the demo path. Hide unfinished pages (remove them from `SiteConfig.Nav`). |
| **Day 2, 07:00** | **Code freeze.** Only fixes to the demo path from now on. |
| Day 2, 07:00–09:30 | Record the video (several takes, `"AgentMode": "Replay"` for identical runs). Edit. |
| Day 2, 09:30–10:30 | README final, `dotnet build` (0 warnings) + `dotnet test`, deploy, test the live URL on a phone. |
| Day 2, **≥ 30 min before deadline** | Submit. Don't submit at the last minute. |
| After submission | Rehearse the live pitch with the timer, at least 3 times. Prepare answers to likely questions. |

## Video: 3 minutes, 4 parts

| Time | Part | Content |
| --- | --- | --- |
| 0:00–0:30 | **Problem** | Who suffers, how often, what it costs (one number). |
| 0:30–1:10 | **What's unique** | Your insight in one sentence. Why existing tools / ChatGPT don't solve it. |
| 1:10–2:20 | **Demo** | The agent working on a realistic case: request → tool calls visible → result → human approval. |
| 2:20–3:00 | **Architecture** | Screen-record `/architecture`: web app → API → agent runtime → tools → data/models. Mention human-in-the-loop, audit trail (`/runs`), tested finance functions as tools, Microsoft.Extensions.AI (model-agnostic) + fallback. |

Tips:
- Write the script word for word; read it while recording the voice-over separately from the screen.
- Use `"AgentMode": "Replay"` so every take is identical; say honestly that the run was recorded if asked.
- Zoom the browser to 125% and hide the bookmarks bar. Use realistic seed data, not "test 123".
- Export at 1080p. Check the duration: **≤ 3:00**.

## Demo checklist

- [ ] Happy-path script written (3–5 steps) and rehearsed
- [ ] Runs recorded for every step of the script (`src/HackathonStarter/Recordings/*.json` committed)
- [ ] Works with Wi-Fi off (`AI__AgentMode=Replay dotnet run --project src/HackathonStarter` on the presenting laptop)
- [ ] Data reset: restart the app (Development recreates the database)
- [ ] Unfinished pages hidden from `SiteConfig.Nav`
- [ ] Dark/light mode chosen for the projector (light is usually safer)

## Code checklist

- [ ] `dotnet build` has 0 warnings, `dotnet test` passes, the app runs with **no appsettings.Local.json**
- [ ] README: what it does, how to run in one command, architecture, which parts are AI
- [ ] `appsettings.Local.example.json` lists every setting; **no secrets committed** (`git log -p | grep -i apikey`)
- [ ] Deployed URL in the README

## Likely judge questions

- What does the agent do on its own, and where does a human decide?
- What happens when the model is wrong? (approval step, audit trail, constrained tools)
- Can this run with sensitive data? (Azure OpenAI in an EU region, or self-hosted / open-weight models via the OpenAI-compatible provider; uploads are not stored)
- How would it scale / what does it cost per request?
- What is real and what is mocked in the demo?
