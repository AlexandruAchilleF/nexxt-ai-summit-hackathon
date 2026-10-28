# Research: KYC pentru firme (Raiffeisen, Operational Efficiency)

Enunțul oficial: *„Explore how AI agents can reduce repetitive work involving documents, data and verification.”*

Fapte pe care construim produsul și pitch-ul. Fiecare are sursa lângă; ce e marcat **de verificat** nu intră în video
până nu e confirmat (textul consolidat al legii sau un mentor Raiffeisen).

## 1. Beneficiarul real: ce spune legea

| Fapt | Sursă |
| --- | --- |
| Beneficiar real = persoana fizică ce **deține sau controlează în ultimă instanță** clientul (Legea 129/2019, art. 4 alin. 1). | [fiscalitatea.ro](https://www.fiscalitatea.ro/legea-1292019-ce-inseamna-beneficiar-real-si-care-sunt-obligatiile-declarative-aferente-23866/) |
| **Azi, în România: peste 25%.** Textul vorbește de „25% plus o acțiune” / participație „de peste 25%”. Exact 25% **nu** e suficient. | idem; [profit.ro (OUG 111/2020)](https://profit.ro/legal/guest-writer-filip-company-noi-prevederi-privind-beneficiarul-real-introduse-prin-ordonanta-de-urgenta-111-2020-clarificari-si-noi-incertitudini-19433363) |
| **Din 10 iulie 2027 (AMLR, Regulamentul UE 2024/1624, art. 52): 25% sau mai mult.** Exact 25% devine suficient. Procentul indirect = **produsul cotelor pe lanț**, adunat pe toate lanțurile aceleiași persoane. | [Springlex, art. 52](https://www.springlex.eu/en/packages/aml/amlr-regulation/article-52/), [Strise](https://strise.ai/guides/ubo-mapping-amlr), [Noerr](https://www.noerr.com/en/insights/beneficial-owner-and-transparency-register-the-new-eu-anti-money-laundering-regulation) |
| **Controlul contează separat de cote:** cineva cu 8% poate fi beneficiar real prin acord între asociați sau control al consiliului. | [Strise](https://strise.ai/guides/ubo-mapping-amlr) |
| **Dacă nu se identifică nimeni:** beneficiar real devine persoana din **conducerea de rang superior** (administratori, directori), iar banca păstrează evidența pașilor făcuți. | [fiscalitatea.ro](https://www.fiscalitatea.ro/legea-1292019-ce-inseamna-beneficiar-real-si-care-sunt-obligatiile-declarative-aferente-23866/) |
| Firma depune declarația de beneficiar real la ONRC la înmatriculare și **în 15 zile de la orice modificare** (amendă 5.000–10.000 lei). | idem; [fiscalitatea.ro (amenzi)](https://www.fiscalitatea.ro/declaratia-privind-beneficiarii-reali-2025-amenzile-ajung-pana-la-10000-lei-pentru-nedepunere-24002/) |
| Registrul beneficiarilor reali **nu mai e public** (CJUE, nov. 2022); din 2025 accesul cere **interes legitim**, verificat de ONRC. Băncile, ca entități raportoare, au acces pentru KYC. | [economedia.ro](https://economedia.ro/?p=305177), [onpcsb.ro](https://onpcsb.ro/uploads/articole/attachments/67d410015efe2287564155.pdf) |

**Consecința pentru produs (insight-ul nostru):** pragul nu se scrie „hard” în cod. E o regulă configurabilă
(`> 25%` azi, `≥ 25%` din 2027), iar **cazul exact la 25% e marcat pentru om** în ambele regimuri. Juriul o înțelege
într-o propoziție și arată că știm domeniul.

## 2. Certificatul constatator ONRC

| Fapt | Sursă |
| --- | --- |
| Conține administratorii, asociații/acționarii, activitățile, sediile secundare, auditorii și indicatori din ultimele situații financiare. Costă **30 lei**, se comandă online (InfoCert / portal ONRC). | [onrc.ro](https://onrc.ro/index.php/ro/informatii/certificate-constatatoare), [startupcafe.ro](https://startupcafe.ro/p-certificatul-constatator-actul-pe-care-banca-primaria-si-finantatorul-ti-l-cer-in-acelasi-an-ce-contine-si-cum-il-obtii-104392) |
| Arată **asociații direcți** cu numărul de părți sociale / cota. **De verificat** pe un model oficial cum apar exact procentele. | idem |
| Un certificat arată **un singur nivel**. Pentru lanțuri, banca are nevoie de certificatul fiecărei firme-asociat (și de documente din registrul străin pentru entități din afara țării). | dedus din conținut; de confirmat cu mentorul |

**Consecința pentru produs:** demo-ul folosește **3 certificate** (firma client + 2 firme intermediare) și agentul
spune singur ce certificat lipsește ca să închidă lanțul.

## 3. Cât costă azi (pentru „cine plătește”)

| Fapt | Sursă |
| --- | --- |
| O revizuire KYC pentru un client corporate costa în medie **2.598 $** în 2023 (+17% față de 2022); jumătate din bănci cheltuie **1.500–3.000 $** pe o revizuire. | [Corporate Compliance Insights](https://www.corporatecomplianceinsights.com/kyc-review-cost-survey-2023/), [A-Team Insight](https://a-teaminsight.com/briefs/financial-institutions-spend-1500-to-3000-to-complete-one-client-kyc-review) |
| O revizuire KYC dura în medie **95 de zile** în 2023 (84 în 2022). | [Fenergo](https://www.fenergo.com/newsroom/share-of-banks-losing-clients-to-poor-kyc-practices-surges-to-record-high) |
| Doar **o treime** din revizuirile periodice sunt automatizate. | [fintech.global](https://fintech.global/2025/10/08/70-of-banks-lose-clients-due-to-slow-onboarding/) |
| Raiffeisen Bank România: ~**2,33 mil. clienți**; ~**95.000** clienți IMM activi în Raiffeisen Online IMM / Smart Business (T1 2024); +70% clienți IMM noi în S1 2026. | [businessforum.ro](https://www.businessforum.ro/finance/20260130/raiffeisen-bank-romania-posts-4-profit-growth-in-2025-2808), [romania-insider.com](https://www.romania-insider.com/business/raiffeisen-bank-romania-growth-q1-2024), [BVB, rezultate S1 2026](https://bvb.ro/infocont/infocont26/RBRO27A_20260730173229_RBRO-Financial-Results-2026-Q2-ENG.pdf) |

Atenție: cifrele Fenergo vin de la un vânzător de software KYC (studii sponsorizate). În pitch le spunem cu sursa.

## 4. Întrebări pentru mentorul Raiffeisen (la primul checkpoint)

1. Câte revizuiri KYC de firme faceți pe an și cât durează una, în ore de analist?
2. Ce parte e cea mai repetitivă: citirea certificatului, comparația cu dosarul sau calculul pe lanțuri?
3. Ați accepta un model care rulează pe serverele băncii (on-premise)? Ce restricții aveți pentru modele în cloud?
4. Cum arată un dosar KYC la voi (ce câmpuri) și în ce sistem stă?
5. Raportați discrepanțele față de registrul beneficiarilor reali? Cât de des apar?

Răspunsurile la 1 și 2 înlocuiesc ipotezele din calculul de ROI (`PLAYBOOK.md`, secțiunea 4).
