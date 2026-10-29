# DemoFilm: traseul video, condus de Playwright

Parcurge traseul video din [`docs/PITCH.md`](../../docs/PITCH.md) într-un browser vizibil, în ritmul vocii, în 9 segmente
(S1-S9: S8 din PITCH e împărțit în metrici, S8, și fraza de final, S9). Duratele sunt în `Program.cs` (în total 3:00).
Vocea se înregistrează separat; la montaj folosești timpii din `film-chapters.txt`.

- Implicit interfața în română (pentru video, care e în engleză: `--lang en`), logat ca Ana (admin), viewport 1536x864 la scara 1,25 (= 1920x1080, zoom 125%).
- Un cursor vizibil (cerc galben) merge la fiecare buton înainte de click.
- Înainte de prima dublă resetează datele demo (Setări, „Resetează datele demo”), în afara camerei. `--no-reset` sare peste.
- La S3 și S4 se oprește dacă rularea agentului e „Înregistrare” (Replay): pașii care modifică date se filmează doar pe
  Live sau Simulare.
- După Maria (S4), pune data reală înapoi înainte de S5. Acest pas rămâne în afara capitolelor și se taie la montaj.

## Rulare

```bash
# 1. Aplicația, local, pe mock (fără model): Simulare, cu uneltele reale
AI__Provider=None dotnet run --project src/HackathonStarter

# 2. Traseul (alt terminal)
dotnet run --project tools/DemoFilm
```

Pe Render (instanța publică e deschisă, fără cod de acces):

```bash
dotnet run --project tools/DemoFilm -- --url https://hackathon-starter-k0ym.onrender.com
```

Atenție: resetarea șterge datele tuturor celor care folosesc aceeași instanță. Nu rula pe instanța publică (Render) cât
timp juriul o folosește: resetarea și traseul schimbă datele pentru toți.

| Opțiune | Ce face |
| --- | --- |
| `--url URL` | Aplicația (implicit `http://localhost:5000`) |
| `--code COD` | Doar dacă instanța cere un cod de acces (`Security:AccessCode`); sau variabila `DEMO_ACCESS_CODE` |
| `--video DIR` | Înregistrează și un `.webm` 1920x1080 în `DIR` (Playwright) |
| `--shots DIR` | O captură la sfârșitul fiecărui segment, ca să verifici dubla fără s-o vizionezi |
| `--fast` | Repetiție rapidă: pauze scurte, fără așteptarea vocii (aproximativ 1 minut) |
| `--from S5` / `--to S7` | Doar o parte. S3 și S4 continuă cazul Mariei, deci încep de la S2 |
| `--lang en` | Interfața în engleză |
| `--no-reset` | Fără resetarea datelor la început |
| `--headless` | Fără fereastră (pentru verificare) |

## Filmare

- Calitatea cea mai bună: browserul vizibil, înregistrat cu QuickTime (Cmd+Shift+5, „Înregistrează porțiunea selectată”,
  pe fereastra browserului). `--video` e o copie de rezervă.
- Pe mock, o dublă durează aproximativ 3:00. Live (Bedrock), S3 și S4 așteaptă modelul; montajul taie așteptarea.
- Dacă un segment pică, scriptul salvează `film-error-S#.png` și se oprește. Rulează din nou: resetează datele la început.
