using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Features.Cases;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Sentinel;

/// <summary>
/// The scripted CRS Sentinel (docs/PLAN.md 13.3), the main demo path: it calls the real tools and the case changes in the
/// database. The mock sees only the last message, so it is a state machine over the case: the code comes from the message
/// (the case page's buttons send <see cref="SentinelActions.Analyze"/> and <see cref="SentinelActions.Verify"/>), the next step
/// from the case status; a button pressed in the wrong state says what comes first. Every answer cites rule ID and page, says
/// "format valid", calls a signal "semnal, fără alertă", quotes („…”) only verified evidence and never repeats a masked value.
/// </summary>
internal static partial class SentinelMock
{
    public static async Task RunAsync(MockContext m)
    {
        var caseCode = CaseCodeRegex().Match(m.Input) is { Success: true } cm ? cm.Value.ToUpperInvariant() : null;
        var clientCode = ClientCodeRegex().Match(m.Input) is { Success: true } km ? km.Value.ToUpperInvariant() : null;
        if (caseCode is null && clientCode is not null)
            caseCode = (await m.Context.Services.GetRequiredService<ICaseService>().QueueAsync(new CaseQueueFilter(OpenOnly: false, ClientCode: clientCode)))
                .FirstOrDefault()?.Code;

        if (caseCode is null)
        {
            if (clientCode is not null) await ClientWithoutCaseAsync(m, clientCode);
            else await m.SayAsync(L("Spune-mi codul cazului (de exemplu CRS-2026-0001) sau al clientului (de exemplu CL-0001) și pregătesc cazul.",
                "Tell me the case code (e.g. CRS-2026-0001) or the client code (e.g. CL-0001) and I will prepare the case."));
            return;
        }

        var c = await m.CallAsync("getCase", new { caseCode });
        if (c.Str("error") is { } error)
        {
            await m.SayAsync(L($"Nu găsesc cazul {caseCode}: {error}", $"I cannot find case {caseCode}: {error}"));
            return;
        }

        var canEdit = Roles.Editors.Split(',').Contains(m.Context.User.Role);
        var summaryOnly = TextMatch.ContainsAny(m.Input, "rezum", "summar", "stadiu", "status", "overview");
        var status = c.Str("status");
        if (!canEdit || summaryOnly || status is "Closed" or "Expired")
        {
            await m.SayAsync(Summary(c, canEdit));
            return;
        }

        var action = SentinelActions.Recognize(m.Input);
        if (action == SentinelActions.Action.Analyze && status is not "Open")
        {
            await m.SayAsync(Join(Summary(c, canEdit),
                L($"Cererea către client a plecat deja. Când sosește răspunsul, folosește „{SentinelActions.VerifyLabel}”.",
                  $"The request to the client has already gone out. When the answer arrives, use \"{SentinelActions.VerifyLabel}\".")));
            return;
        }
        if (action == SentinelActions.Action.Verify && status is "Open")
        {
            await m.SayAsync(Join(Summary(c, canEdit),
                L($"Cererea către client nu a fost trimisă încă: întâi „{SentinelActions.AnalyzeLabel}”, apoi aprobarea operatorului.",
                  $"The request to the client has not been sent yet: first \"{SentinelActions.AnalyzeLabel}\", then the operator's approval.")));
            return;
        }

        // Steps that write (evidence, signals, a draft, a self-certification, a response) run only for the two buttons or a
        // request in whole words; a question ("Ce conținut are autocertificarea?", "De ce nu e caz?") only reads.
        var request = action == SentinelActions.Action.None && !m.Input.TrimEnd().EndsWith('?');
        var prepare = action == SentinelActions.Action.Analyze || (request && HasPhrase(m.Input, PreparePhrases));
        var verify = action == SentinelActions.Action.Verify || (request && HasPhrase(m.Input, VerifyPhrases));

        switch (status)
        {
            case "Open" when c.Arr("requests").Any(r => r.Str("status") == "Draft"):
                await m.SayAsync(Join(
                    L($"Cazul {caseCode}: cererea către client (nr. {c.Arr("requests").Last(r => r.Str("status") == "Draft").Int("id")}) e pregătită și așteaptă aprobarea unui operator. Nu am trimis nimic.",
                      $"Case {caseCode}: the request to the client (no. {c.Arr("requests").Last(r => r.Str("status") == "Draft").Int("id")}) is drafted and awaits an operator's approval. Nothing was sent."),
                    L("Termen: ", "Deadline: ") + c.Str("deadline") + "."));
                break;
            case "Open" when prepare:
                await PrepareAsync(m, c);
                break;
            case "Open":
                await m.SayAsync(Join(Summary(c, canEdit), Next(SentinelActions.AnalyzeLabel)));
                break;
            case "AwaitingClient" or "Reminded" or "Escalated":
                await AnswerAsync(m, c, verify);
                break;
            case "ResponseReceived" when verify:
                await ProposeClosingAsync(m, c, "");
                break;
            case "ResponseReceived":
                await m.SayAsync(Join(Summary(c, canEdit), Next(SentinelActions.VerifyLabel)));
                break;
            default:
                await m.SayAsync(Summary(c, canEdit));
                break;
        }
    }

    // ─── Run 1: getCase, listDocuments, readDocument, recordEvidence, recordSignal, draftClientRequest, sendClientRequest ──

    private static async Task PrepareAsync(MockContext m, JsonElement c)
    {
        var code = c.Str("code")!;
        var indicia = c.Arr("indicia").ToList();
        var docs = await m.CallAsync("listDocuments", new { clientCode = c.Str("clientCode") });
        var payslip = docs.Arr("documents").LastOrDefault(d => d.Str("kind") == "Payslip");

        var evidenceLines = new List<string>();
        if (payslip.ValueKind == JsonValueKind.Object)
        {
            var docId = payslip.Int("id") ?? 0;
            var file = payslip.Str("fileName");
            var read = await m.CallAsync("readDocument", new { caseCode = code, documentId = docId });
            foreach (var page in read.Arr("pages"))
            {
                var number = page.Int("page") ?? 1;
                var text = Unwrap(page.Str("text") ?? "");

                var phoneRule = indicia.Select(i => i.Str("ruleId")).FirstOrDefault(r => r is "CRS-IND-PHONE");
                if (phoneRule is not null && PhoneRegex().Match(text) is { Success: true } phone)
                {
                    var ev = await m.CallAsync("recordEvidence", new { caseCode = code, documentId = docId, page = number, quote = phone.Value, ruleId = phoneRule });
                    var jurisdiction = indicia.First(i => i.Str("ruleId") == phoneRule).Str("jurisdiction") ?? "";
                    // The quote holds a phone number (masked for a model): cite where it is, never the number.
                    evidenceLines.Add(ev.Bool("verified")
                        ? L($"Dovadă pentru {phoneRule}: telefonul din {SentinelWording.Country(jurisdiction)} apare pe {file}, p. {number} (citat verificat de cod; nu repet numărul, e o dată personală).",
                            $"Evidence for {phoneRule}: the phone in {SentinelWording.Country(jurisdiction)} is on {file}, p. {number} (quote verified by code; I do not repeat the number, it is personal data).")
                        : L($"Telefonul de pe {file} nu a putut fi verificat pe pagină: nu îl folosesc ca dovadă.",
                            $"The phone on {file} could not be verified on the page: I do not use it as evidence."));
                }

                if (Employer(text) is { } employer)
                {
                    var description = L($"Venit salarial de la {employer.Name} (fluturaș de salariu)", $"Salary income from {employer.Name} (payslip)");
                    var signal = await m.CallAsync("recordSignal", new { caseCode = code, description, documentId = docId, page = number, quote = employer.Quote });
                    var label = signal.Str("label") ?? SentinelWording.SignalLabel;
                    evidenceLines.Add(signal.Bool("verified")
                        ? L($"Context ({label}): {description}, „{employer.Quote}” ({file}, p. {number}, citat verificat). Nu schimbă decizia și nu deschide caz.",
                            $"Context ({label}): {description}, „{employer.Quote}” ({file}, p. {number}, verified quote). It does not change the decision and opens no case.")
                        : L($"Angajatorul de pe {file} nu a putut fi verificat pe pagină: nu îl folosesc.",
                            $"The employer on {file} could not be verified on the page: I do not use it."));
                }
            }
        }

        var draft = await m.CallAsync("draftClientRequest", new { caseCode = code, body = Letter(c), language = "ro" });
        if (draft.Str("error") is { } error)
        {
            await m.SayAsync(Join(Header(c), Indicia(c), string.Join("\n", evidenceLines), L($"Nu am putut salva cererea: {error}", $"I could not save the request: {error}")));
            return;
        }
        await m.CallAsync("sendClientRequest", new { caseCode = code, requestId = draft.Int("requestId"), channel = "email" });

        var documents = string.Join("; ", draft.Arr("requiredDocuments").Select(d => d.GetString()));
        await m.SayAsync(Join(
            Header(c),
            Indicia(c),
            string.Join("\n", evidenceLines),
            L($"Am pregătit cererea către client, în limbaj simplu. Documente cerute: {documents}. Termen: {DueDate(c)}. Trimiterea pe email așteaptă aprobarea unui operator; până atunci nu pleacă nimic.",
              $"I drafted the request to the client in plain Romanian. Documents requested: {documents}. Due: {DueDate(c)}. Sending it by email awaits an operator's approval; nothing leaves before that.")));
    }

    // ─── Waiting, then run 2: listDocuments, extractSelfCertification, checkTin, registerResponse, evaluateRules, closeCase ─

    /// <param name="write">False for a question: say what arrived and stop before reading it into the case.</param>
    private static async Task AnswerAsync(MockContext m, JsonElement c, bool write)
    {
        var code = c.Str("code")!;
        var openedAt = c.Date("openedAt") ?? DateTime.MinValue;
        var docs = await m.CallAsync("listDocuments", new { clientCode = c.Str("clientCode") });
        var form = docs.Arr("documents")
            .Where(d => (d.Str("kind") == "SelfCertForm" || TextMatch.ContainsAny(d.Str("fileName") ?? "", "autocert", "selfcert", "self-cert"))
                        && d.Date("uploadedAt") > openedAt)
            .OrderBy(d => d.Date("uploadedAt"))
            .LastOrDefault();

        if (form.ValueKind != JsonValueKind.Object)
        {
            var documents = string.Join("; ", c.Arr("requiredDocuments").Select(d => d.GetString()));
            await m.SayAsync(Join(
                L($"Cazul {code} ({StatusText(c.Str("status"))}). Așteptăm de la client: {documents}. Termen: {c.Str("deadline")}.",
                  $"Case {code} ({StatusText(c.Str("status"))}). We are waiting for the client to send: {documents}. Deadline: {c.Str("deadline")}."),
                L("Indiciile rămân deschise până la răspuns: ", "The indicia stay open until the answer: ") + IndiciaShort(c) + ".",
                L("Nu a sosit încă o autocertificare nouă după deschiderea cazului.", "No new self-certification has arrived since the case opened.")));
            return;
        }

        if (!write)
        {
            await m.SayAsync(Join(
                L($"Cazul {code} ({StatusText(c.Str("status"))}): a sosit o autocertificare nouă, {form.Str("fileName")}. Nu am citit-o încă în caz.",
                  $"Case {code} ({StatusText(c.Str("status"))}): a new self-certification arrived, {form.Str("fileName")}. I have not read it into the case yet."),
                Next(SentinelActions.VerifyLabel)));
            return;
        }

        var documentId = form.Int("id") ?? 0;
        var x = await m.CallAsync("extractSelfCertification", new { caseCode = code, documentId });
        if (x.Str("error") is { } error)
        {
            await m.SayAsync(L($"Nu am putut citi autocertificarea: {error}", $"I could not read the self-certification: {error}"));
            return;
        }
        if (!x.Bool("recorded"))
        {
            var missing = string.Join(", ", x.Arr("missing").Select(f => FieldText(f.GetString())));
            await m.SayAsync(Join(
                L($"Cazul {code}: în autocertificarea primită ({form.Str("fileName")}) codul nu a găsit, cu citat verificat: {missing}.",
                  $"Case {code}: in the self-certification received ({form.Str("fileName")}) code found no verified quote for: {missing}."),
                L("Nu am înregistrat nimic și nu propun închiderea. Un operator decide dacă cere o autocertificare corectată.",
                  "I recorded nothing and do not propose closing. An operator decides whether to ask for a corrected form.")));
            return;
        }

        var tins = new List<string>();
        foreach (var j in x.Arr("jurisdictions").Where(j => j.Bool("verified")))
        {
            var country = j.Str("country")!;
            if (j.Str("tin") is { Length: > 0 } tin)
            {
                var t = await m.CallAsync("checkTin", new { country, tin });
                tins.Add($"{country} (TIN: {t.Str("result")})");
            }
            else tins.Add(L($"{country} (fără TIN, motivul {j.Str("noTinReason")?[..1]})", $"{country} (no TIN, reason {j.Str("noTinReason")?[..1]})"));
        }

        var summary = L($"Autocertificare nouă primită online ({form.Str("fileName")}): {string.Join(", ", tins)}.",
            $"New self-certification received online ({form.Str("fileName")}): {string.Join(", ", tins)}.");
        var registered = await m.CallAsync("registerResponse", new
        {
            caseCode = code, kind = "NewSelfCertification", category = "None", channel = "online", documentId,
            selfCertificationId = x.Int("selfCertificationId") ?? 0, summary,
        });
        if (registered.Str("error") is { } failed)
        {
            await m.SayAsync(L($"Nu am putut înregistra răspunsul: {failed}", $"I could not register the response: {failed}"));
            return;
        }

        await ProposeClosingAsync(m, c, Join(
            L($"Cazul {code}: am citit autocertificarea nouă ({form.Str("fileName")}). Codul a verificat citatul fiecărui câmp: nume, adresă de reședință, data nașterii, jurisdicții, semnătură și data semnării.",
              $"Case {code}: I read the new self-certification ({form.Str("fileName")}). Code verified the quote of every field: name, residence address, date of birth, jurisdictions, signature and its date."),
            L("Jurisdicții declarate: ", "Declared jurisdictions: ") + string.Join(", ", tins) + ".",
            L("Am înregistrat răspunsul clientului (autocertificare nouă, canal online).", "I registered the client's response (new self-certification, online).")));
    }

    private static async Task ProposeClosingAsync(MockContext m, JsonElement c, string before)
    {
        var code = c.Str("code")!;
        var rules = await m.CallAsync("evaluateRules", new { clientCode = c.Str("clientCode") });
        var decision = rules.Str("decision");
        var reportable = string.Join(", ", rules.Arr("reportableFor").Select(r => r.GetString()));
        var reportableText = reportable.Length > 0 ? L($" Raportabil pentru: {reportable}.", $" Reportable for: {reportable}.") : "";

        if (decision != "Compliant")
        {
            var open = string.Join(", ", rules.Arr("hits").Where(h => h.Str("coveredBy") == "None").Select(Cite));
            await m.SayAsync(Join(before,
                L($"Reevaluare în cod (evaluateRules): {decision}. Încă neacoperite: {open}. Nu propun închiderea cazului {code}.",
                  $"Re-evaluation by code (evaluateRules): {decision}. Still uncovered: {open}. I do not propose closing case {code}.")));
            return;
        }

        await m.CallAsync("closeCase", new { caseCode = code, channel = "Online" });
        var covered = string.Join(L(" și ", " and "), rules.Arr("hits").Select(Cite));
        await m.SayAsync(Join(before,
            L($"Reevaluare în cod (evaluateRules): Compliant. {covered} sunt acoperite de autocertificare.{reportableText}",
              $"Re-evaluation by code (evaluateRules): Compliant. {covered} are covered by the self-certification.{reportableText}"),
            L($"Închiderea cazului {code} (canal: Online) așteaptă aprobarea unui operator; după aprobare, codul salvează dovada confirmării.",
              $"Closing case {code} (channel: Online) awaits an operator's approval; once approved, code saves the confirmation proof.")));
    }

    // ─── Read-only answers ───────────────────────────────────────────────────────────────────────────────────────────

    private static string Summary(JsonElement c, bool canEdit)
    {
        var code = c.Str("code");
        var readOnly = canEdit ? "" : L("Ai drept doar de citire: pregătirea, trimiterea și închiderea le face un operator.",
            "You can only read: an operator prepares, sends and closes.");
        switch (c.Str("status"))
        {
            case "Closed" when c.Obj("proof") is { } p:
                return Join(
                    L($"Cazul {code} e închis ({Day(c.Date("closedAt"))}). Rezultat ({c.Str("outcomeRuleId")}): {c.Str("outcome")}",
                      $"Case {code} is closed ({Day(c.Date("closedAt"))}). Outcome ({c.Str("outcomeRuleId")}): {c.Str("outcome")}"),
                    L($"Dovada confirmării: canal {p.Str("channel")}, declarație datată {Day(p.Date("declarationDatedAt"))}, primită {Day(p.Date("declarationReceivedAt"))}, confirmată {Day(p.Date("confirmedAt"))}, tip {p.Str("evidenceKind")}, document {p.Str("transactionOrDocumentId") ?? "-"}, aprobată de {p.Str("approvedBy")}.",
                      $"Confirmation proof: channel {p.Str("channel")}, declaration dated {Day(p.Date("declarationDatedAt"))}, received {Day(p.Date("declarationReceivedAt"))}, confirmed {Day(p.Date("confirmedAt"))}, kind {p.Str("evidenceKind")}, document {p.Str("transactionOrDocumentId") ?? "-"}, approved by {p.Str("approvedBy")}."),
                    Reportable(c, c.Str("outcome")), readOnly);
            case "Expired":
                return Join(
                    L($"Cazul {code} a expirat ({Day(c.Date("expiredAt"))}). Consecința din ghid ({c.Str("outcomeRuleId")}): {c.Str("outcome")}",
                      $"Case {code} expired ({Day(c.Date("expiredAt"))}). Consequence from the guide ({c.Str("outcomeRuleId")}): {c.Str("outcome")}"),
                    Reportable(c, c.Str("outcome")), readOnly);
        }

        var evidence = c.Arr("evidence").Count(e => e.Bool("verified"));
        var signals = c.Arr("signals").Count();
        var requests = c.Arr("requests").Select(r => $"{r.Int("id")}: {r.Str("status")}").ToList();
        return Join(
            Header(c) + " " + L($"Stare: {StatusText(c.Str("status"))}.", $"Status: {StatusText(c.Str("status"))}."),
            Indicia(c),
            L($"Dovezi verificate: {evidence}. Semnale de context: {signals} ({SentinelWording.SignalLabel}).",
              $"Verified evidence: {evidence}. Context signals: {signals} ({SentinelWording.SignalLabel})."),
            requests.Count > 0 ? L("Cereri către client: ", "Requests to the client: ") + string.Join(", ", requests) + "." : "",
            readOnly);
    }

    /// <summary>A client without a case: what the rules say now, with the reasons for what was cleared (the traps).</summary>
    private static async Task ClientWithoutCaseAsync(MockContext m, string clientCode)
    {
        var rules = await m.CallAsync("evaluateRules", new { clientCode });
        if (rules.Str("error") is { } error)
        {
            await m.SayAsync(L($"Nu găsesc clientul {clientCode}: {error}", $"I cannot find client {clientCode}: {error}"));
            return;
        }
        var cleared = rules.Arr("cleared").Select(h => $"- {Cite(h)}: {h.Str("explanation")}");
        var hits = rules.Arr("hits").Select(h => $"- {Cite(h)}: {h.Str("explanation")}");
        var docs = await m.CallAsync("listDocuments", new { clientCode });
        var shielded = docs.Arr("documents").Where(d => (d.Int("hiddenFragmentsRemoved") ?? 0) > 0)
            .Select(d => L(
                $"- {d.Str("fileName")}: scutul a scos {d.Int("hiddenFragmentsRemoved")} fragment(e) de text ascuns (p. {string.Join(", ", d.Arr("hiddenPages").Select(x => x.GetRawText()))}; {Reasons(d)}) înainte de orice model. Textul scos nu a fost citit de model; un om verifică documentul.",
                $"- {d.Str("fileName")}: the shield removed {d.Int("hiddenFragmentsRemoved")} hidden text fragment(s) (p. {string.Join(", ", d.Arr("hiddenPages").Select(x => x.GetRawText()))}; {Reasons(d)}) before any model. The removed text was not read by a model; a person checks the document."))
            .ToList();
        await m.SayAsync(Join(
            L($"Clientul {clientCode} nu are caz. Decizia motorului de reguli (cod): {rules.Str("decision")}.",
              $"Client {clientCode} has no case. The rule engine (code) decides: {rules.Str("decision")}."),
            string.Join("\n", hits),
            cleared.Any() ? L("Verificate și neaplicate:", "Checked and not applied:") + "\n" + string.Join("\n", cleared) : "",
            shielded.Count > 0 ? L("Semnal roșu de la scut:", "Red flag from the shield:") + "\n" + string.Join("\n", shielded) : ""));
    }

    // ─── Text pieces ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static string Header(JsonElement c) => L(
        $"Cazul {c.Str("code")} (client {c.Str("clientCode")}, regim {c.Str("regime")?.ToUpperInvariant()}, cont {(c.Bool("isPreexisting") ? "preexistent" : "nou")}). Termen: {c.Str("deadline")}.",
        $"Case {c.Str("code")} (client {c.Str("clientCode")}, {c.Str("regime")?.ToUpperInvariant()} regime, {(c.Bool("isPreexisting") ? "preexisting" : "new")} account). Deadline: {c.Str("deadline")}.");

    private static string Indicia(JsonElement c)
    {
        var lines = c.Arr("indicia").Select(i => $"- {Cite(i)}: {i.Str("explanation")}").ToList();
        return lines.Count == 0
            ? L("Cazul nu are indicii înregistrate.", "The case has no recorded indicia.")
            : L("Indicii găsite de motorul de reguli (cod):", "Indicia found by the rule engine (code):") + "\n" + string.Join("\n", lines);
    }

    private static string IndiciaShort(JsonElement c) => string.Join(", ", c.Arr("indicia").Select(Cite));

    /// <summary>"CRS-IND-ADDR (DE, p. 29)": every indicium with its rule ID and guide page.</summary>
    private static string Cite(JsonElement hit)
    {
        var jurisdiction = hit.Str("jurisdiction") is { Length: > 0 } j ? $"{j}, " : "";
        return $"{hit.Str("ruleId")} ({jurisdiction}p. {hit.Str("guidePage")})";
    }

    /// <summary>"Raportabil pentru: DE.", unless the outcome text written by code already says it.</summary>
    private static string Reportable(JsonElement c, string? outcome)
    {
        var list = string.Join(", ", c.Arr("reportableFor").Select(r => r.GetString()));
        var text = list.Length > 0 ? L($"Raportabil pentru: {list}.", $"Reportable for: {list}.") : "";
        return outcome?.Contains(text, StringComparison.Ordinal) == true ? "" : text;
    }

    /// <summary>The letter to the client: Romanian, plain words, the exact documents (code) and the due date (code).</summary>
    private static string Letter(JsonElement c)
    {
        var what = c.Arr("indicia").Select(i => i.Str("ruleId") switch
        {
            "CRS-IND-ADDR" => "o adresă nouă",
            "CRS-IND-MAIL" => "o adresă de corespondență nouă",
            "CRS-IND-PHONE" => "un număr de telefon nou",
            "CRS-IND-SO" => "un ordin de plată permanent",
            "CRS-IND-POA" => "o împuternicire",
            _ => "date noi",
        }).Distinct().ToList();
        var countries = c.Arr("indicia").Select(i => i.Str("jurisdiction")).OfType<string>().Where(j => j.Length > 0).Distinct()
            .Select(j => SentinelWording.Country(j, en: false)).ToList();
        var change = what.Count == 0 ? "date noi" : what.Count == 1 ? what[0] : string.Join(", ", what[..^1]) + " și " + what[^1];
        var where = countries.Count > 0 ? $", din {string.Join(", ", countries)}" : "";
        var due = c.Date("dueAt") is { } d ? $"până la {SentinelWording.DateRo(d)}" : "cât mai curând";
        var documents = string.Join("\n", c.Arr("requiredDocuments").Select(x => $"- {x.GetString()}"));

        return $"""
            Bună ziua, vă rugăm să ne confirmați țara în care aveți rezidența fiscală.

            Am primit de la dumneavoastră {change}{where}. Legea ne cere să verificăm în ce țară plătiți impozitele, așa că vă rugăm să ne trimiteți, {due}:
            {documents}

            Puteți răspunde online, din aplicația băncii, sau la orice sucursală. Dacă aveți întrebări, vă ajutăm cu drag.

            Cu stimă,
            Echipa Banca Demo
            """;
    }

    private static string DueDate(JsonElement c) => c.Date("dueAt") is { } d ? SentinelWording.Date(d) : SentinelWording.NoDeadline;

    private static string Day(DateTime? d) => d is { } v ? SentinelWording.Date(v) : "-";

    private static string StatusText(string? status) => status switch
    {
        "Open" => L("deschis", "open"),
        "AwaitingClient" => L("în așteptarea clientului", "awaiting the client"),
        "Reminded" => L("memento trimis", "reminder sent"),
        "Escalated" => L("escaladat la departamentul fiscal", "escalated to the tax department"),
        "ResponseReceived" => L("răspuns primit", "response received"),
        "Closed" => L("închis", "closed"),
        "Expired" => L("expirat", "expired"),
        _ => status ?? "",
    };

    private static string FieldText(string? field) => field switch
    {
        "holderName" => L("numele titularului", "the holder's name"),
        "residenceAddress" => L("adresa de reședință", "the residence address"),
        "birthDate" => L("data nașterii", "the date of birth"),
        "jurisdictions" => L("jurisdicția de rezidență fiscală", "the jurisdiction of tax residence"),
        "signed" => L("semnătura", "the signature"),
        "signatureDate" => L("data semnării", "the signature date"),
        _ => field ?? "",
    };

    /// <summary>The visible text of a readDocument page, without the data wrapper.</summary>
    private static string Unwrap(string text)
    {
        var start = text.IndexOf('\n');
        var end = text.LastIndexOf("\n</document>", StringComparison.Ordinal);
        return start >= 0 && end > start ? text[(start + 1)..end] : text;
    }

    /// <summary>"Angajator: Müller Logistik GmbH": the label and the words after it, up to the next label ("Data plății:").</summary>
    private static (string Name, string Quote)? Employer(string text)
    {
        var label = EmployerRegex().Match(text);
        if (!label.Success) return null;
        var start = label.Index + label.Length;
        var lineEnd = text.IndexOf('\n', start);
        var words = WordRegex().Matches(text[start..(lineEnd < 0 ? text.Length : lineEnd)]).Take(10).ToList();
        var taken = 0;
        while (taken < words.Count && taken < 8)
        {
            var word = words[taken].Value;
            if (word.EndsWith(':')) break; // the next label
            if (taken > 0 && taken + 1 < words.Count && words[taken + 1].Value.EndsWith(':') && char.IsUpper(word[0])) break; // "Data plății:"
            taken++;
        }
        if (taken == 0) return null;
        var end = start + words[taken - 1].Index + words[taken - 1].Length;
        return (string.Join(" ", words.Take(taken).Select(w => w.Value)), text[label.Index..end]);
    }

    private static readonly string[] PreparePhrases =
        ["pregătește cazul", "pregătește dosarul", "analizează dosarul", "analizează cazul", "prepare the case", "analyze the file", "analyse the file"];

    // "Maria a trimis autocertificarea nouă." is Ana's sentence in docs/PITCH.md S4.
    private static readonly string[] VerifyPhrases =
    [
        "verifică răspunsul", "verifică autocertificarea", "a trimis autocertificarea", "continuă",
        "check the answer", "check the self-certification", "sent the self-certification", "sent a new self-certification", "continue",
    ];

    /// <summary>A phrase in whole words, without case or diacritics: "continuă" is not in "conținut".</summary>
    private static bool HasPhrase(string text, IEnumerable<string> phrases)
    {
        var words = $" {string.Join(' ', TextMatch.Words(text))} ";
        return phrases.Any(p => words.Contains($" {string.Join(' ', TextMatch.Words(p))} ", StringComparison.Ordinal));
    }

    private static string Next(string action) =>
        L($"Pasul următor, când operatorul decide: „{action}”.", $"Next step, when the operator decides: \"{action}\".");

    /// <summary>"text alb, font sub 2 pt": why the shield removed the text (HiddenTextShield reasons).</summary>
    private static string Reasons(JsonElement d) => string.Join(", ", d.Arr("hiddenReasons").Select(r => r.GetString() switch
    {
        "WhiteText" => L("text alb", "white text"),
        "TinyFont" => L("font sub 2 pt", "font under 2 pt"),
        "OffPage" => L("în afara paginii", "off the page"),
        "Invisible" => L("text invizibil", "invisible text"),
        var other => other ?? "",
    }));

    private static string Join(params string[] parts) => string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string L(string ro, string en) => Lang.Pick(ro, en);

    // ─── JSON helpers over tool results ──────────────────────────────────────────────────────────────────────────────

    private static string? Str(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        } : null;

    private static int? Int(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static bool Bool(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static JsonElement? Obj(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    private static IEnumerable<JsonElement> Arr(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];

    private static DateTime? Date(this JsonElement e, string name) =>
        e.Str(name) is { } s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    [GeneratedRegex(@"\bCRS-\d{4}-\d{4}\b", RegexOptions.IgnoreCase)]
    private static partial Regex CaseCodeRegex();

    [GeneratedRegex(@"\bCL-\d{4}\b", RegexOptions.IgnoreCase)]
    private static partial Regex ClientCodeRegex();

    [GeneratedRegex(@"Telefon:[ \t]*\+?\d[\d \-().]*\d")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"Angajator:[ \t]*")]
    private static partial Regex EmployerRegex();

    [GeneratedRegex(@"\S+")]
    private static partial Regex WordRegex();
}
