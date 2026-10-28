# Phone, desktop and other clients

Pick what the challenge needs. All of it is optional; if you don't use something, it costs nothing.

| Need | Use | Effort |
| --- | --- | --- |
| "It should work on a phone / laptop" | The app itself: it's responsive and **installable** (PWA) | Nothing: already on |
| Photo of a document (receipt, ID, invoice) | The "Attach file" button in the chat: on a phone it offers the camera | Nothing (needs a vision model for real reading) |
| A separate frontend (Lovable, React, a second team) | The **HTTP API** + `Security:AllowedOrigins` | 30 min |
| NFC, push on iPhone, fully offline, app-store app | .NET MAUI Blazor Hybrid (see the end) | Hours: only if the challenge requires it |

---

## 1. Installable app (PWA)

The web app installs like a native app: own icon, own window, no browser bar. Same code, same deploy.

- **Android / Chrome, Edge on Windows/Mac:** the **Install app** button in the app bar (or the install icon in the address bar).
- **iPhone / iPad (Safari):** Share → **Add to Home Screen**.
- It needs HTTPS (Render/Azure give you that; `localhost` works too).

Customize:
- Name, color, start page: `Config/SiteConfig.cs` (`Name`, `PrimaryColor`, `Pwa`). The manifest is generated from it.
- Icons: replace the files in `wwwroot/icons/` (same names and sizes: 192, 512, maskable 512, apple-touch 180).
  <https://maskable.app/editor> checks the maskable one.
- Phone bottom bar: `SiteConfig.MobileNav` (hrefs from `Nav`; empty = no bottom bar).
- Turn it off: `Enabled: false` in `SiteConfig.Pwa`, or just ignore it.

Offline: the service worker (`wwwroot/service-worker.js`) only shows a friendly offline page. It deliberately doesn't
cache the app, because Blazor Server needs the connection, and stale caches are the classic PWA demo-day bug.
Runs continue on the server if the phone loses signal; reopening the chat link (`?c=…`) shows the result.

**Demo tip:** record the phone part of the video with the installed app (Chrome DevTools → device toolbar works too).

---

## 2. A separate frontend over the API

Every agent is an HTTP endpoint. Interactive docs with "Try it": **`/api-docs`**.

### Enable it for another origin

Set on the host (or in `appsettings.Local.json`):

```
Security__AllowedOrigins = https://your-app.lovable.app,http://localhost:5173
Security__ApiKey         = some-long-random-string
Security__ApiUser        = member          # the demo user API calls act as (viewer = read-only)
```

**Warning:** a key used from browser code is visible to anyone who opens DevTools. For a hackathon that's fine because the
rate limits (`Security:AgentRunsPerMinute`, `DailyLiveRunBudget`) protect your AI credits. Use `ApiUser=viewer`
if the frontend shouldn't change data. Never put the model's API key in a frontend.

### Simple: one request, one JSON answer

```js
const API = "https://your-app.onrender.com";
const KEY = "some-long-random-string";

async function ask(messages, files = []) {
  const res = await fetch(`${API}/api/agents/assistant?stream=false`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Api-Key": KEY },
    body: JSON.stringify({ messages, files }),
  });
  if (!res.ok) throw new Error((await res.json()).error ?? res.statusText);
  return res.json(); // { runId, status, source, answer, fallbackReason, pendingApprovals: [{ id, tool, summary }], run }
}

const result = await ask([{ role: "user", content: "Give me an overview of our items" }]);
console.log(result.answer);
```

Conversation history: send all previous turns in `messages` (`role` = `user` / `assistant`), last one from the user.

### Approvals (human in the loop)

When `status === "AwaitingApproval"`, show `pendingApprovals[i].summary` with Approve/Reject buttons:

```js
await fetch(`${API}/api/runs/${result.runId}/approvals/${approval.id}`, {
  method: "POST",
  headers: { "Content-Type": "application/json", "X-Api-Key": KEY },
  body: JSON.stringify({ approved: true }),
}).then(r => r.json()); // same shape as above, with the final answer
```

### Images (e.g. a photo taken on the phone)

```js
const toBase64 = file => new Promise(ok => {
  const r = new FileReader();
  r.onload = () => ok(r.result.split(",")[1]);
  r.readAsDataURL(file);
});
// <input type="file" accept="image/*" capture="environment"> opens the camera on phones
const files = [{ name: file.name, base64: await toBase64(file), mediaType: file.type }];
await ask([{ role: "user", content: "What is the total on this receipt?" }], files);
```

Text documents: `{ name: "contract.txt", text: "…" }`. Keep photos under ~2 MB (resize before upload).

### Streaming (answer appears word by word, tool calls live)

The endpoint without `?stream=false` sends one JSON object per line:
`run` → `step` (new step: tool call, tool result, text…) / `delta` (more text for a step) / `text` (replace a step's text) /
`reset` (fallback restarted the run) → `done` (`answer`, `pendingApprovals`, `run`).

```js
async function askStreaming(messages, onEvent) {
  const res = await fetch(`${API}/api/agents/assistant`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Api-Key": KEY },
    body: JSON.stringify({ messages }),
  });
  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    let newline;
    while ((newline = buffer.indexOf("\n")) >= 0) {
      onEvent(JSON.parse(buffer.slice(0, newline)));
      buffer = buffer.slice(newline + 1);
    }
  }
}

await askStreaming([{ role: "user", content: "Give me an overview" }], e => {
  if (e.type === "step" && e.step.kind === "ToolCall") console.log("tool:", e.step.tool);
  if (e.type === "delta") process.stdout?.write?.(e.text);
  if (e.type === "done") console.log("\nanswer:", e.answer);
});
```

### curl (for the README, the jury, or testing)

```bash
curl -s -X POST "https://your-app.onrender.com/api/agents/assistant?stream=false" \
  -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"Give me an overview of our items"}]}'
```

### Lovable prompt (paste, then adjust)

> Build a mobile-first web app called "<NAME>" that is a chat client for an existing AI agent API.
> API base URL: `<https://your-app.onrender.com>`; every request sends the header `X-Api-Key: <KEY>`.
> Send messages with `POST /api/agents/assistant?stream=false` and the JSON body
> `{ "messages": [{ "role": "user" | "assistant", "content": string }], "files": [{ "name", "base64", "mediaType" }] }`
> (the full conversation, last message from the user). The response is
> `{ runId, status, answer, fallbackReason, pendingApprovals: [{ id, tool, summary }] }`. Show `answer` as markdown.
> If `pendingApprovals` is not empty, show each `summary` as a card with Approve / Reject buttons that call
> `POST /api/runs/{runId}/approvals/{id}` with `{ "approved": true | false }` and show the returned `answer`.
> Add a camera/photo button (`<input type="file" accept="image/*" capture="environment">`), resize the image to max
> 1600px, and send it as base64 in `files`. Show a loading indicator while waiting (can take 5–20 s).
> Use our brand color `<#2563eb>`. Keep the conversation in memory; a "New chat" button clears it.

Then add the Lovable URL to `Security__AllowedOrigins`.

### From C# (another service, a console tool, a MAUI app)

```csharp
using var http = new HttpClient { BaseAddress = new Uri("https://your-app.onrender.com") };
http.DefaultRequestHeaders.Add("X-Api-Key", key);
var response = await http.PostAsJsonAsync("/api/agents/assistant?stream=false",
    new { messages = new[] { new { role = "user", content = "Give me an overview" } } });
var result = await response.Content.ReadFromJsonAsync<JsonElement>();
Console.WriteLine(result.GetProperty("answer").GetString());
```

---

## 3. Native app, only if the challenge really needs it

If you need something a browser can't do well (NFC, background push on iPhone, fully offline, a store listing), use
**.NET MAUI Blazor Hybrid**: a native app that renders Razor components, so it can reuse `Components/` and talk to this
backend through the API above (`HttpClient`, section 2). Before committing to it, check that:

- someone has the Android SDK (or a Mac with Xcode for iPhone) installed and has built a hello-world **before** the hackathon;
- the jury can actually run it (an APK link, a device on stage); the 3-minute video can show it either way.

Otherwise the PWA covers "it's an app on my phone" for a demo, with zero extra code.
