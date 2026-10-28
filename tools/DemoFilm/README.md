# DemoFilm: traseul video, condus de Playwright

Parcurge traseul din [`docs/PITCH.md`](../../docs/PITCH.md) (S1-S9) într-un browser vizibil, în ritmul vocii: fiecare
segment așteaptă până la durata lui din `PITCH.md` §1. Vocea se înregistrează separat; la montaj folosești timpii din
`film-chapters.txt`.

- Interfața în română, logat ca Ana (admin), viewport 1536x864 la scara 1,25 (= 1920x1080, zoom 125%).
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

Pe Render (aplicația cere codul de acces):

```bash
DEMO_ACCESS_CODE=<codul> dotnet run --project tools/DemoFilm -- --url https://hackathon-starter-k0ym.onrender.com
```

Atenție: resetarea șterge datele tuturor celor care folosesc aceeași instanță. Pe Render, rulează doar după code freeze.

| Opțiune | Ce face |
| --- | --- |
| `--url URL` | Aplicația (implicit `http://localhost:5000`) |
| `--code COD` | Codul de acces (sau `DEMO_ACCESS_CODE`) |
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
