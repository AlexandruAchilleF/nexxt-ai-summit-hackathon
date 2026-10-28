# Uneltele AI ale echipei

> Ce avem instalat pe mașina cu T3 / Claude Code (Persoana 4) și ce e în repo pentru toți.
> În Claude Code: un skill se cere cu `/nume`; un subagent se cere cu „folosește subagentul X”.

## 1. În repo, pentru toată echipa (Claude Code, T3 și OpenCode)

**Subagenții** (sursa: `docs/agents/`, generați cu `python3 tools/agents/sync.py`):

| Subagent | La ce îl folosești |
| --- | --- |
| `tech-lead` | Agentul principal: împarte sarcina, trimite la specialiști, revizuiește, integrează |
| `domain-analyst` | Întrebări CRS/FATCA, răspunde doar din ghid, cu pagina |
| `architect` | Contracte, interfețe, impactul unei schimbări (doar propune) |
| `rules-engineer` | P1: `Features/Rules` |
| `workflow-engineer` | P2: cazuri, termene, data simulată |
| `security-engineer` | P3: scut, jurnal, mascare |
| `integration-engineer` | Bedrock, TIN on Europa, Docker, CI, deploy |
| `agent-engineer` | P4: agentul, uneltele, mock-ul, verificările |
| `data-engineer` | P5: entități, seed, date sintetice, cazuri-etalon |
| `frontend-engineer` | Paginile Blazor, după 22:00 |
| `test-engineer` | Teste xUnit și Playwright |
| `qa-verifier` | Build + teste, raport (nu repară) |
| `code-reviewer` | Review pe diff, cu lista CRS |
| `docs-writer` | README, documente, pitch |

**Comenzile:** `/verify` (build + teste) · `/golden` (cele 18 cazuri) · `/crs-review` (review pe branch) ·
`/ghid <întrebare>` (răspuns din ghid) · `/handoff` (distribuie blocurile HANDOFF) · `/pr` (commit + PR pe numele tău).

## 2. Skill-uri de proces (cum lucrăm)

| Skill | Când |
| --- | --- |
| `superpowers:brainstorming` | Înainte de o funcționalitate nouă |
| `superpowers:writing-plans`, `superpowers:executing-plans` | Plan pe pași, apoi execuție |
| `superpowers:subagent-driven-development`, `superpowers:dispatching-parallel-agents` | Sarcini independente în paralel |
| `superpowers:test-driven-development` | Test întâi (cum cere planul) |
| `superpowers:systematic-debugging` | Orice bug sau test roșu |
| `superpowers:verification-before-completion` | Înainte să spui „gata” |
| `superpowers:requesting-code-review`, `superpowers:receiving-code-review` | Review |
| `superpowers:using-git-worktrees`, `superpowers:finishing-a-development-branch` | Branch-uri izolate, închiderea lor |
| `feature-dev:feature-dev` (+ agenții `code-explorer`, `code-architect`, `code-reviewer`) | Funcționalitate ghidată cap-coadă |

## 3. Review, calitate, securitate

| Skill / comandă | Ce face |
| --- | --- |
| `/code-review` | Review pe diff sau PR, cu nivel de efort |
| `/review-pr` (pr-review-toolkit) | Agenți: teste, erori tăcute, tipuri, comentarii, simplificare |
| `/simplify`, `code-simplifier` | Curățenie fără schimbare de comportament |
| `/security-review`, `security-guidance` | Review de securitate (rulează și automat după commit) |
| `/commit`, `/commit-push-pr`, `/clean_gone` (commit-commands) | Git; atribuirea e dezactivată în `.claude/settings.json` |

## 4. Stack-ul nostru (.NET, AWS)

| Skill / server | Ce face |
| --- | --- |
| `csharp-lsp` | Navigare și erori C# (cere `csharp-ls`, instalat de `tools/setup-dev.sh`) |
| `microsoft-docs` (+ serverul `microsoft-learn`) | Documentația oficială .NET, Blazor, EF Core |
| `context7` | Documentație la zi pentru biblioteci (MudBlazor, PdfPig, Playwright) |
| `aws-core:amazon-bedrock`, `aws-iam`, `aws-containers`, `aws-secrets-manager`, `aws-deployment`, `aws-observability`… | Bedrock, IAM, ECS, secrete, deploy, loguri |
| `deploy-on-aws:deploy`, `aws-architecture-diagram` | Deploy cu IaC; diagrama de arhitectură pentru video |
| `claude-api` | Referința pentru modelele Claude și API |
| `playwright` | Browser automat, pentru E2E și capturi |

## 5. Interfață, documente, prezentare

| Skill | Ce face |
| --- | --- |
| `frontend-design` | Direcție vizuală pentru paginile noi (pagina cazului) |
| `dataviz` | Grafice corecte și accesibile (`/metrics`, dashboard) |
| `anthropic-skills:pptx`, `pdf`, `docx`, `xlsx` | Slide-uri de pitch, PDF-uri sintetice, documente |
| `session-report` | Raport HTML cu ce a făcut AI-ul (pentru „cum am lucrat”) |

## 6. Configurare și automatizare

`update-config` (setări, hook-uri) · `fewer-permission-prompts` · `claude-code-setup` · `claude-md-management` ·
`skill-creator` / `writing-skills` · `loop` (sarcini repetate) · `workflow-authoring` (orchestrare multi-agent) · `run`
(pornește aplicația și verifică o schimbare).

## 7. Instalate, dar nu ne folosesc aici

`huggingface-skills` (antrenare și găzduire de modele), `anthropic-skills:google-workspace`, `computer-use`, `chrome-browser`,
`built-in-browser`. Nu le folosim: nu antrenăm modele și nu automatizăm desktopul.
