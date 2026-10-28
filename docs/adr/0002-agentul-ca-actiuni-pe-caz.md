# ADR 0002: Agentul ca acțiuni pe caz (Agent as case actions)

## Context

The case detail page initially showed a generic chat box as the main interaction, asking operators to type free-form questions like "Pregătește cazul" (prepare the case). This presented several problems:
1. The chat box looked like a generic chatbot, not an operational tool
2. Operators had to remember exact action keywords without hints
3. The UI did not reflect the case's current state or what was actually possible to do next

## Decision

Replace the chat box as the main control with state-aware action buttons. Each button represents the single most important action the operator should take based on the case's current status:
- **Open case**: "Analizează dosarul" (analyze dossier) — read documents, extract evidence, draft the request
- **Request drafted**: Inline approval card (Aprobă / Respinge) from the agent trace
- **Client answered**: "Verifică răspunsul" (check the response) — extract self-certification, verify TIN, register response, re-evaluate
- **Case compliant**: "Închide cazul" (close case) — propose closing with proof

Each action starts a crs-sentinel run with a fixed action ID (e.g., "AnalyzeDossier"), not keyword guessing. The agent steps, checks, and approval cards render inline as AgentTrace does today.

Keep a small, collapsed "Întreabă despre caz" (ask about the case) box for read-only questions only.

## Rejected alternatives

- Chat only, no agent at all: Loses the structured workflow and audit trail.
- Both chat and buttons: Makes the UI cluttered and ambiguous about what the operator should do.
- Generic workflow buttons with agent hidden: Loses transparency of what the agent decided and why.

## Consequences

- Operators see the case state and their next action at a glance.
- New operators learn the workflow faster (state-aware UI).
- The interface is no longer a generic chatbot; it is transparent about what runs and why.
- Read-only questions moved to a secondary panel (the question box), keeping the focus on the primary action.
- Action IDs ("AnalyzeDossier", "VerifyResponse") make runs deterministic and testable.

## Implementation

- AgentChat gains two optional parameters: `AllowActionParameter` and a public method `SendWithAction(text, actionId)`.
- CaseDetailPage renders action buttons based on `c.Status` with `OnClick="() => RunAction(_agentChat, 'AnalyzeDossier')"`.
- The action buttons are in a new `.crs-case-actions` div above the collapsed "Întreabă despre caz" expansion panel.
- Romanian UI text for action labels is in `Services/Lang.Ro.CrsUi.cs`.

## References

- Decision model: CRS Sentinel case state machine (Features/Cases/Case.cs, CaseStatus enum)
- Agent integration: Components/Agent/AgentChat.razor, Components/Pages/Cases/CaseDetailPage.razor
- Tests: MariaFlowTests.cs (E2E) and AgentMockTests.cs (agent mock)
