# Fișa OCDE pentru TIN: Austria (AT)

Sursa pentru cazul-etalon 3 (Bogdan Lazăr, autocertificare fără TIN pentru AT, motivul B): decizia `Compliant` din
[`GOLDEN-CASES.md`](../GOLDEN-CASES.md) se sprijină pe această fișă.

| | |
| --- | --- |
| Document | „Jurisdiction's name: AUSTRIA. Information on Tax Identification Numbers” (OCDE, portalul AEOI) |
| URL | https://www.oecd.org/content/dam/oecd/en/topics/policy-issue-focus/aeoi/austria-tin.pdf |
| Pagina portalului | https://www.oecd.org/en/networks/global-forum-tax-transparency/resources/aeoi-implementation-portal/tax-identification-numbers.html (citată și de ghid, p. 48) |
| Accesat | 28.10.2026, descărcat cu `curl` (vechiul URL `.../tax-identification-numbers/Austria-TIN.pdf` redirecționează aici) |
| Versiunea | PDF de 2 pagini, 143.718 octeți, `Last-Modified: Fri, 21 Jun 2024 19:01:33 GMT`; în metadatele PDF: creat 18.08.2016, modificat 17.11.2021 |
| SHA-256 | `9aa2daa774b67023912f1732dcd5c0e10d60237ead2a05e9a3e2bff6800b4818` |

## Citatele exacte (pagina 1)

> Question 1 – Does your jurisdiction automatically issue TINs to all residents for tax purposes?
> Individuals No Entities Yes

> Question 2a – If you answered no to Question 1 with respect to individuals, describe those instances where
> individuals are not being automatically issued a TIN.
> TINs are only issued to individuals who are liable to tax.

> The Local Tax Offices issue TINs to the taxpayers having their residence in the area of competence of these Offices,
> when they ask the local office for a service. This means that a TIN can change when a taxpayer changes residence.

> TIN are not reported on official documents of identification: no TIN cards are issued. TIN numbers can be found on
> tax assessments in the right upper corner on the first page.

Formatul (Section II): `99-999/9999`, 9 cifre; cratima și bara nu sunt obligatorii.

## Ce folosim din ea

- Ghidul (p. 37): o declarație fără TIN nu e de încredere dacă portalul OCDE „indică faptul că jurisdicția raportabilă
  emite un TIN tuturor rezidenților fiscali”. Pentru AT fișa spune „Individuals No” (Q1), deci lipsa TIN-ului, cu motivul
  B („nu poate obține”, p. 53) și explicația clientului, nu face declarația nesigură: cazul 3 dă `Compliant`, cu motivul
  înregistrat.
- Motivul A („țara nu emite TIN”) nu se potrivește: AT emite TIN, doar nu tuturor (Î8).
- Fișa descrie formatul; nu spune nimic despre existența unui TIN anume. Spunem „format valid”, niciodată „TIN valid”.
