# TRACE: cum funcționează, cap-coadă

> Documentul de înțelegere pentru toată echipa. Detaliile tehnice (entități, interfețe, program) sunt în `docs/PLAN.md`,
> regulile cu pagina din ghid în `docs/reguli-verificate.md`. Ce e marcat NEVERIFICAT nu intră în pitch.

## 1. Problema, într-un paragraf

Când un client își schimbă adresa, telefonul, un ordin permanent sau o împuternicire, banca trebuie să verifice dacă
autocertificarea lui fiscală mai e valabilă. Dacă banca „știe sau are motive să știe” că situația s-a schimbat, declarația își
pierde valabilitatea și banca are **90 de zile** să obțină o declarație nouă sau o explicație rezonabilă. Altfel clientul e tratat
ca rezident fiscal **și** în jurisdicția inițială, **și** în cea nouă (Ghid ANAF, p. 35–36). Raportarea greșită se face pe fiecare cont.

Azi, verificarea înseamnă un om care compară câmpuri, caută prin documente, scrie clientului și ține minte un termen.
TRACE face partea repetitivă, iar omul aprobă.

## 2. Cele trei principii

| Cine | Ce face | Ce nu face niciodată |
| --- | --- | --- |
| **Codul** | Aplică regulile (cu ID și pagină), calculează termenele, verifică citatele, verifică formatul TIN, ține jurnalul | Nu ghicește: aceeași intrare dă mereu același rezultat |
| **AI-ul** | Citește documente, extrage citate, găsește semnale de context, scrie textul către client, explică | Nu decide indicii, nu calculează termene, nu pornește alerte |
| **Omul** | Aprobă cererea către client și închiderea cazului, decide cazurile la limită | — |

## 3. Drumul unei modificări (cazul Mariei, traseul din video)

```text
 Maria își schimbă adresa: München (DE)
            │
 [1] ChangeService primește ChangeEvent ── jurnal: change.received
            │
 [2] CrsRuleEngine compară "înainte" cu "după"          (cod pur, fără AI)
       CRS-IND-ADDR (DE)   p. 29 lit. b    indiciu
       CRS-IND-PHONE (DE)  p. 29 lit. c    indiciu: telefon DE și niciun telefon RO
       capcane verificate și respinse, cu explicație ("Cleared")
       → Decision = Clarification                       ── jurnal: rule.applied ×2
            │
 [3] CaseService deschide CRS-2026-0008 (istoricul pus la pornire deschide întâi 7 cazuri)
       DueAt = data aflării + 90 zile, RemindAt = ziua 85 ── jurnal: case.opened
            │
 [4] Agentul pregătește cazul (Bedrock live, sau mock)
       readDocument(fluturaș.pdf) → doar textul VIZIBIL, după scut
       recordEvidence("Telefon: +49 89 …", p. 1) → codul verifică citatul → verificat
       recordSignal("salariu de la Müller GmbH") → semnal, FĂRĂ alertă
       draftClientRequest → lista de documente vine din cod, textul îl scrie modelul
            │
 [5] sendClientRequest  ── cere APROBARE ── doar operatorul aprobă (Ana da; Mihai și Vera nu) ── jurnal: request.approved
            │
 [6] Meniul Demo → „La ziua mementoului” (data demo „+85 z”) → DeadlineWatcher: memento + escaladare la fiscal
            │
 [7] Răspunsul Mariei: autocertificare nouă (DE, TIN)
       checkTin(DE, …) → "format valid" (live la Comisia Europeană sau reguli locale)
       evaluateRules → Compliant, raportabil pentru DE
            │
 [8] closeCase ── cere APROBARE ── doar operatorul aprobă (Ana da; Mihai și Vera nu) ── ConfirmationProof (canal, moment, dovadă) ── jurnal: case.closed
            │
 [9] /audit → „Verifică” lanțul de hash-uri; meniul Demo → „Modul de falsificare” (doar admin), apoi „Falsifică” pe o intrare → se vede exact intrarea ruptă
```

Varianta fără răspuns: la ziua 91 cazul trece în `Expired`, cu consecința din ghid (rezident în ambele jurisdicții).

## 4. Ce se întâmplă cu fiecare tip de intrare

**Nota asupra aprobărilor:** Uneltele cu `needsApproval` (`sendClientRequest` și `closeCase`) nu rulează imediat. Agentul primește mesajul `awaiting_human_approval` și nu mai cheamă alte unelte. După ce operatorul aprobă (doar Ana, rolul admin), unealta rulează fără model, în aceeași rulare. Cazul Mariei are două rulări: „Analizează dosarul” (se oprește la aprobarea cererii) și „Verifică răspunsul” (se oprește la aprobarea închiderii).

**Nota asupra replay-ului:** Replay-ul nu rulează uneltele: reafișează pașii înregistrați (inclusiv rezultatele uneltelor), fără să cheme codul. De aceea nu filmăm și nu facem demo în Replay cu pașii care schimbă date; pentru ei folosim Bedrock live sau mock (mock-ul cheamă uneltele reale). Înregistrările servesc doar la întrebările care doar citesc datele (de exemplu „Rezumă cazul”).

| Intrare | Ce face sistemul | Rezultat |
| --- | --- | --- |
| Modificare în România (Cluj → Iași) | Nicio regulă din p. 29–31 nu se aplică | `NoCase`, cu explicația de ce |
| Telefon străin non-SUA, dar are și telefon RO | Regula de telefon CRS cere „niciun telefon în RO” | `NoCase`, capcană explicată |
| Ordin permanent către DE din cont curent | Excepția pentru conturile de depozit (p. 29 lit. d) | `NoCase`, explicat |
| Același ordin din cont de custodie | Indiciu CRS | `Clarification` |
| Orice telefon SUA, loc al nașterii SUA, cetățenie SUA | Indicii FATCA (p. 31), fără excepția „telefon RO” | `FatcaAlert` |
| Salariu de la o firmă germană | Nu e indiciu legal | `NoCase` + semnal de context, fără alertă |
| PDF cu text alb ascuns („ignoră instrucțiunile…”) | Scutul îl scoate înainte de model | `ShieldAlert`, textul nu ajunge la model |
| Autocertificare cu TIN italian greșit | Formatul TIN pe țară | `Clarification` |

Toate cele 21 de cazuri-etalon, cu datele și rezultatul așteptat, sunt în `docs/GOLDEN-CASES.md` (rezumat în `docs/PLAN.md` secțiunea 16.1).

## 5. Piesele și cine le construiește

```text
 UI (Blazor + MudBlazor)        coada · cazul · modificare nouă · client · /audit · /metrics · data simulată
   │  doar servicii, niciodată baza de date direct
 Servicii                        ChangeService · CaseService · ClientService · EvaluationService      (P2, P5)
   │
 Cod pur, testat                 CrsRuleEngine (P1) · DeadlineCalculator, SimClock (P2)
                                 HiddenTextShield, EvidenceVerifier, TinFormat, AuditLog (P3)
   │
 Agent                           CrsSentinelAgent : ToolAgent, unelte, mock, verificări (P4)
   │
 Externe                         Bedrock (P4) · TIN on Europa prin TargetClient (P3) · SQLite
```

| Persoana | Construiește | Gata când |
| --- | --- | --- |
| 1 · Reguli | Motorul de reguli CRS/FATCA, catalogul cu pagini, explicațiile capcanelor | cazurile 1–9 și 11–15 trec |
| 2 · Termene și cazuri | Data simulată, termene, stările cazului, memento/escaladare/expirare, dovada | cazurile 16–18 trec |
| 3 · Încredere | Scutul, verificarea citatelor, formatul TIN, TIN on Europa, jurnalul | cazul 10 trece, „Falsifică” arată intrarea ruptă |
| 4 · Agent și AI | Agentul, uneltele, aprobările, mock-ul, Bedrock, verificările, înregistrările | Maria cap-coadă pe mock și o dată live |
| 5 · Date și livrare | Cele 21 de cazuri-etalon, 50 de clienți sintetici, PDF-uri, metrici, documente, video | `/metrics` din rulări reale |

**Nota asupra termenelor:** Calculul termenului diferă între conturile noi și preexistente:
- **Cont nou:** DueAt = data la care banca știe de schimbare + 90 zile calendaristice (p. 20–21, 35–36)
- **Cont preexistent:** ghidul spune „până în ultima zi a anului calendaristic în cauză [...], sau în 90 de zile calendaristice” (p. 29); implementăm cea mai târzie dintre ele.
  Pentru contul documentat prin adresa de rezidență așa scrie legea (Codul de procedură fiscală, Anexa nr. 2 pct. 1), ca
  în Comentariul OCDE (Secțiunea III, pct. 13); la indiciile din căutarea electronică, cum sunt cazurile noastre, e
  interpretarea noastră, aliniată la Comentariul OCDE (pct. 16). Detalii în README.
- **Preexistent se calculează pe regim** (CRS = 31 dec. 2015, FATCA = 30 iunie 2014), nu pe tipul contului (depozit/custodie). Același cont poate fi nou pentru CRS și preexistent pentru FATCA.

## 6. Agentul, pe scurt (Persoana 4)

- **Un singur agent cu unelte** (`CrsSentinelAgent`, id `crs-sentinel`: „CRS Sentinel” a fost numele de lucru), nu un roi. Orchestrarea pașilor (cine pornește cazul, când expiră) e în cod (`ChangeService`,
  `CaseService`, `DeadlineWatcher`). Agentul face doar ce cere limbaj: citește, citează, scrie, explică.
- **Două modele:** profilul `extract` (Claude Haiku 5.5) pentru extrageri tipizate și clasificări mici; profilul principal
  (Claude Sonnet 5.5) pentru agent și textul către client. Amândouă pe Amazon Bedrock, us-west-2.
- **Cele 14 unelte** (`Features/Sentinel/SentinelTools.cs`; planul inițial în `docs/PLAN.md` 13.1): `getCase`,
  `getClientSnapshot`, `evaluateRules`, `listDocuments`, `readDocument`, `recordEvidence`, `recordSignal`,
  `extractSelfCertification` (extrage autocertificarea din PDF cu citatele verificate), `checkTin`, `draftClientRequest`,
  `sendClientRequest` (aprobare), `registerResponse`, `closeCase` (aprobare), `escalate`.
- **Lanțul de rezervă:** live pe Bedrock → înregistrare (`Recordings/crs-sentinel.json`) → mock scris de noi, care cheamă
  uneltele reale. Aplicația merge cap-coadă și fără internet.
- **Verificări după fiecare răspuns** (cod, nu model): `pii-leak` (nicio dată personală scăpată) și `evidence-quoted`
  (citatele din răspuns sunt printre citatele verificate de unelte). `CrsChecks` mai are `signals-never-alert`,
  `rule-cited` și `no-tin-valid-claim`, testate, dar neactive pe agent.

## 7. Cum ne apărăm de prompt injection

Un document încărcat de client poate conține instrucțiuni ascunse pentru model. Apărarea are cinci straturi, toate în cod:

1. **Scutul** (`HiddenTextShield`) scoate textul alb, microscopic, din afara paginii sau invizibil, **înainte** ca modelul să vadă ceva.
   Documentul primește semnal roșu, iar operatorul vede ce s-a scos.
2. **Modelul nu poate decide nimic.** Chiar convins de un text, nu poate închide un caz sau șterge un indiciu: deciziile vin din
   `CrsRuleEngine`, iar acțiunile care schimbă ceva cer aprobarea omului.
3. **Citatele sunt verificate de cod** în textul vizibil al paginii. Un citat inventat apare „Citat negăsit”; unul care
   există doar în textul ascuns e respins cu semnal roșu („Doar în textul ascuns”).
4. **Datele personale sunt mascate** înainte de model (`PiiVault`); uneltele lucrează cu valorile reale.
5. **Verificările de după răspuns** (`pii-leak`, `evidence-quoted`) marchează în rulare datele personale scăpate și citatele
   care nu vin din unelte.

Ce nu detectăm și spunem deschis: text acoperit de o formă desenată peste el.

## 8. Data simulată și jurnalul

- **Data simulată** (`SimClock`): în bara de sus stă data demo, cu „+85 z” lângă ea când e mutată înainte. Un clic pe dată
  deschide meniul Demo: „+1 zi”, „+7 zile”, „+30 zile” și „Înapoi la data reală” (doar operatorul mută data). Pe pagina
  unui caz, meniul are și pașii cazului: „La ziua mementoului”, „După termen”, „Răspunde clientul”. Tot codul CRS (cazuri,
  termene, jurnal) citește timpul de aici, deci în 3 minute de video arătăm 90 de zile. Restul aplicației (login, chei,
  ștergerea rulărilor) rămâne pe ceasul real, ca să nu expire nimic la „+85 zile”.
- **Jurnalul** (`AuditLog`): fiecare pas (modificare, regulă, dovadă, aprobare, memento, închidere) e o intrare cu
  `Hash = SHA-256(hash-ul anterior + conținut)`. Dacă cineva modifică o intrare, „Verifică” arată exact unde s-a rupt lanțul.

## 9. Ce nu folosim și de ce

| Nu folosim | De ce |
| --- | --- |
| **Z3 sau alt SMT solver** | Regulile sunt predicate simple pe date finite, iar codul C# cu teste e mai clar și mai ușor de explicat. Garanțiile pe care le-ar da Z3 le obținem cu un test exhaustiv: enumerăm toate combinațiile câmpurilor relevante (un spațiu finit și mic) și verificăm invarianții, de exemplu „un semnal de context nu schimbă niciodată decizia”. |
| RAG, bază vectorială | Documentele unui client au câteva pagini; le citim întregi, pe pagini, după scut |
| LangChain / framework de agenți | Template-ul are deja agentul cu unelte, aprobări, istoric, replay și verificări |
| Fine-tuning | Deciziile sunt în cod; modelul doar citește și scrie |
| App Runner | Blocat pe contul de workshop (`implicitDeny`) |
| Date reale | Doar sintetice; actele generate poartă „SPECIMEN” |

## 10. Cum rulezi

```bash
tools/setup-dev.sh                              # o singură dată: .NET 10, build, Chromium pentru teste
dotnet run --project src/HackathonStarter       # http://localhost:5000, merge pe mock fără nicio cheie
dotnet test tests/HackathonStarter.Tests        # teste unitare, fără rețea
```

Pentru Bedrock live: credențiale AWS în variabilele de mediu (niciodată în repo) și `AI:Provider = Bedrock` în
`appsettings.Local.json` (ignorat de git).
