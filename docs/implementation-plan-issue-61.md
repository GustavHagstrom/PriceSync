# Implementationsplan för issue #61

Status: planförslag för godkännande före implementation. Ingen produktionskod ändras av planen.

Utgångspunkt: `issue-61`, skapad från `Issue-13`, med draft-PR #68. Genomför etapperna sekventiellt på samma gren. Mellanlägen är granskningsbara commits, men PR:n ska förbli draft tills hela #61 är klar.

## Mål och underlag

[Issue #61](https://github.com/GustavHagstrom/PriceSync/issues/61) ska ge Supplier möjlighet att publicera kompletta, immutabla Price Agreement Versions och ge behöriga parter möjlighet att läsa dem. Exakta versioner ska också kunna konsumeras genom Agreements egna kontrakt.

Kravkällor, i läsordning:

1. Issue #61, inklusive eventuella nytillkomna kommentarer.
2. [Context](Context.md), [Agreements](Domain/agreements.md) och [Price Publication](Domain/price-publication.md).
3. [Leveransdesign för #13](agreement-delivery-plan-issue-13.md), särskilt publicering, persistens och retries.
4. ADR 0001–0005 och befintliga kodmönster.

Planen använder `/implement` för genomförande och `/tdd` för beteendedriven red–green–refactor. `codebase-design` används för små publika gränssnitt med sammanhållen implementation. Planen ersätter inte domänreglerna i underlaget.

## Vad som redan finns

- #59 har logiska Price Lists och ett riktigt `IPriceListScopeVerifier` med resultaten Verified, Invalid och Unavailable.
- #60 har stabil Agreement-identitet, create/get/list, behörigheter och persistens i `agreements`.
- `CreateHandler` visar behörighetskontroll och lokal Read Committed-transaktion. Dess idempotency-key-protokoll ska **inte** återanvändas för versionspublicering.
- Agreements saknar versionsmodell, latest-pekare, publicering och versionsläsning. Produktionsprojektet saknar referens till Price Publication Contracts.
- Agreements testhost och PostgreSQL-fixture konfigurerar ännu inte Price Publication. Dessa måste byggas ut för att testa riktig scope-verifiering.
- `AgreementsBoundaryTests` tillåter idag bara AgreementId i kontraktsprojektet; uppdatera för det avsiktliga nya kontraktet.

## Föreslagna gränssnitt att godkänna

Följ leveransdesignens HTTP-form:

| Operation | Gränssnitt | Observerbart resultat |
| --- | --- | --- |
| Publicera | `POST /api/v1/agreements/{agreementId}/versions` | 201 + Location för ny version; 200 för matchande retry; 409 vid konflikt |
| Läs exakt | `GET /api/v1/agreements/{agreementId}/versions/{versionId}` | Komplett immutable version eller scope-säker 404 |
| Lista | `GET /api/v1/agreements/{agreementId}/versions?pageSize=...&cursor=...` | Kompletta versioner, nyast först enligt ordinal, `items` och `nextCursor` |
| Intern konsument | Agreements-owned exact-version reader, föreslaget `IAgreementVersionReader.GetAsync(AgreementVersionId, CancellationToken)` | Kompletta typade fakta eller NotFound; tekniskt fel får inte maskeras som NotFound |

Publiceringsbegäran innehåller obligatoriskt närvarande men nullable `expectedLatestVersionId`, obligatoriskt `validFrom`, optional `validTo`, fullständig listtäckning med förväntad typ och eventuell generell rabatt, samt versionsgemensamma grupprabatter. Explicit null betyder första publicering; utelämnad predecessor ska ge 400. Avrundningsregeln kommer från systemet, inte från begäran.

Versionens svar och konsumentfakta innehåller version-/agreement-/relationship-identitet, predecessor, ordinal, giltighet, full täckning och rabatter, full avrundningssemantik samt PublishedAt och PublishedByPersonId. Använd typade IDs och fakta, inga EF-entiteter. HTTP-DTO:er förblir interna.

Readern är ett betrott modulkontrakt, inte en genväg för HTTP-behörighet. HTTP-läsningar kontrollerar aktiv acting Organization och Member-behörighet hos någon av parterna, även när relationen är Ended. Distribution och urval av kompatibla versioner ägs av konsumenten och ingår inte här.

Prioriterade testgränssnitt är HTTP mot riktig PostgreSQL och riktig scope-verifierare, därefter exact-version-kontraktet. Ren komplex validering får testas genom ett sammanhållet domängränssnitt om HTTP-testerna annars blir onödigt repetitiva. Undvik mocks av interna handlers eller repositories.

## Gemensam arbetsgång för varje agentsession

1. Läs denna plan, senaste handoff, aktuell issue och berörda filer. Kontrollera gren och arbetskopia; bevara användarens ändringar.
2. Implementera endast nästa ofärdiga etapp. Godkännandet av planen omfattar gränssnitten och beteendena ovan; fråga igen bara vid verkliga kravkonflikter eller ändrad scope.
3. Ta ett beteende i taget: skriv ett test, visa att det fallerar av avsedd orsak, implementera minsta lösning, kör testet grönt och refaktorera därefter. Skriv inte hela etappens tester först.
4. Kör berörd testklass regelbundet och `dotnet build PriceSync.slnx` efter förändringar i kontrakt/modell. Kör hela testsviten en gång vid etappens slut, efter sista kodändringen.
5. Använd `/review` mot etappens startcommit och kontrollera både repo-standard och aktuell issue/etapp. Åtgärda fynd och kör relevanta kontroller igen.
6. Commit på `issue-61`. Uppdatera handoff nedan med commit, färdiga beteenden, exakta testkommandon/resultat och nästa etapp. Markera inte ej körda tester som godkända.

Docker-kompatibel runtime krävs för Testcontainers/PostgreSQL 16. Om den saknas ska sessionen dokumentera blockerad verifiering; etappen är inte verifierat klar förrän testerna har körts. De första etapperna är avsiktligt ofullständiga mot hela issuen och får inte beskrivas som leveransklara.

## Etapp 1 — Första kompletta publiceringen och exakt HTTP-läsning

**Omfattning: en normal agentsession.** Bevisa hela flödet med ett General Price List som ännu saknar publicerad listversion.

- Utöka testhost/fixture med riktig Price Publication-konfiguration, migrationer och isolerad reset. Skapa listor genom befintlig publik funktionalitet.
- Lägg till `AgreementVersionId`, intern versionsmodell och den minimala kompletta persistensen: version, coverage, grupprabatter, normaliserad begäran, predecessor och latest-pekare/ordinal på Agreement.
- Modellera redan nu hela avrundningsregeln: fyra decimaler, midpoint away from zero, en slutlig avrundning efter rabatt utan mellanavrundning och bevarad Price Quantity/Unit of Measure. Ingen prisberäkning införs.
- Implementera POST och exakt GET med riktig behörighet, relationship-scope och verifiering av hela listmängden. Spara alla kompletta kommersiella fält atomiskt i lokal transaktion, med lås på endast Agreement-raden.
- För första versionen krävs explicit null predecessor. Tills retry-protokollet är klart ger en redan publicerad agreement konflikt vid upprepat first-publiceringsförsök.
- Lägg Agreements migration för versionslagring. Använd same-module scope-nycklar så versionen hör till rätt Agreement och relationship, unik ordinal per Agreement och samstämmig latest-pekare. Inga externa foreign keys eller växande history-navigationer.

**Första red–green-beteende:** Supplier Owner publicerar fullständiga villkor för en befintlig General-lista med explicit nollrabatt, får 201/Location och kan läsa exakt samma villkor via GET.

**Övriga prioriterade beteenden:** servergenererad UUIDv7, ordinal 1 och publication actor/time; Customer Member kan läsa; obehörig/annan part får inte läsa/publicera; Ended relation stoppar publicering men inte historikläsning; null och utelämnad predecessor skiljs åt.

**Klart när:** POST→persistens→GET fungerar mot PostgreSQL med riktig listverifiering och oförändrade create/get/list-tester. Ingen permissiv verifieringsstub och inga fejkade framgångssvar för oimplementerade flöden.

## Etapp 2 — Kommersiella regler och komplett listverifiering

**Omfattning: en normal agentsession.** Utöka den fungerande publiceringen beteende för beteende.

- Giltighet: obligatoriskt ValidFrom; ValidTo samma dag tillåts, tidigare dag avvisas; överlapp tillåts.
- Minst en unik logisk lista. General kräver explicit rabatt inklusive noll; net tillåter ingen rabatt och måste matcha Customer. Net-only och blandad täckning fungerar utan publicerade listversioner.
- Verifiera existence, typ, Supplier och Customer via riktiga kontraktet. Ett fel i en flerlistbegäran avvisar allt och lämnar latest oförändrad.
- Exakta procent 0–100 med högst en decimal. Testa gränserna 0, 0.1, 99.9 och 100 samt negativa, över 100 och 0.01. Avvisa överprecision, runda inte bort den.
- Avvisa dubbla list-ID och exakt dubbla gruppkoder även när rabatterna är lika. Bevara case och inledande nollor; `A`, `a`, `01` och `1` är skilda. Trimma eller numeriskt konvertera inte koder. Kod behöver inte finnas i listinnehåll.
- Grupprabatter hör till hela versionen, gäller General-täckning och representerar ersättning av generell rabatt. Bygg ingen kalkylator eller gruppregisterkontroll.
- Unavailable ger 503 med stabil kod `agreement_list_verification_unavailable` och explicit retryable-fakta, utan publicering. Använd kontrollerad felinjektion vid kontraktet för just detta feltest; övriga scope-tester använder riktig verifierare.
- Lägg praktiska DB-checks för datum, typ/rabattform och procentskala samt unika nycklar för coverage och grupper med exakt PostgreSQL `C`-collation. Komplettera med ny migration vid behov.

**Klart när:** kommersiella gränsfall och all-or-nothing-fel är testade genom publikationen/exakt GET, med fältadresserbara 400 Problem Details. Persistenstester får direkt pröva DB-constraints där det är själva persistensgarantin som testas.

## Etapp 3 — Nästa version och deterministiska retries

**Omfattning: en normal agentsession; lägg ingen ny läsfunktion här.**

- Publicera nästa fullständiga version från aktuell expected latest. Uppdatera latest och ordinal atomiskt; föregående version förblir oförändrad och läsbar.
- Under Agreement-låset: matchar predecessor latest, skapa successor. Annars: bara om latest är den direkta successor som skapades från inskickad predecessor får hela normaliserade begäran jämföras och återges med 200. Övriga fall ger `agreement_publication_conflict`/409.
- Normalisering gör ordningen på coverage/grupper irrelevant och procentsiffror numeriskt entydiga, men bevarar exakta gruppkoder. Validera dubbletter före normalisering. Jämför samtliga inskickade kommersiella fält och predecessor; ingen global innehållsdeduplicering.
- Sparad begäran ska vara hållbar över processomstart. Separera begäransjämförelsen från systemets aktuella avrundningsregel.
- Testa ändrad systemregel via en smal intern tillförsel av aktuella systemsemantiker. Retry återger originalets regel/ID/tid/ordinal; verkligt ny publicering använder aktuell regel. Ingen publik Supplier-inställning eller regelregistry införs.
- Behörighet, relation och scope kontrolleras även vid retry. Inga idempotency keys och inga automatiska försök mot ny predecessor.

**Prioriterade red–green-beteenden:** nästa version kan ändra/ta bort täckning utan att ändra äldre version; identisk retry efter omstart ger samma fakta; omordnade samlingar matchar; ändrad giltighet/rabatt/lista/gruppkod konflikterar; retry efter ytterligare publicering konflikterar; samma kommersiella innehåll med aktuell predecessor skapar en genuint ny version.

**Klart när:** sekventiella first/next/retry-fall och ändrade rounding defaults är täckta, inklusive att ett konfliktförsök varken lägger till version eller flyttar latest.

## Etapp 4 — Samtidighet, rollback och transaktionsgarantier

**Omfattning: en avgränsad agentsession för högriskbeteenden.**

- Använd separata requests/DbContexts och deterministiska gates/interceptors enligt befintliga transaktionstester. Undvik sleep-baserade race-tester.
- Konkurrerande first-publiceringar: lika begäran ger en 201 och en 200 med samma version; olika begäran ger en 201 och en 409.
- Upprepa för next-publicering med samma predecessor. Bevisa en successor, unik ordinal och korrekt latest.
- Tvinga fel före commit efter att versionsrader/latest har ändrats. Publik läsning ska visa ursprungligt tillstånd; efterföljande retry ska kunna skapa nästa version utan överbliven ordinal eller partiella villkor.
- Kontrollera att olika Agreements inte serialiseras via ett gemensamt lås och att transaktionen inte låser Price Publication eller Trading Relationships. Använd PostgreSQL-diagnostik endast för att verifiera låsgarantin.
- Kontrollera point-in-time-racen: en operation som sett Active får avsluta efter samtidig termination; en senare publicering/retry som ser Ended nekas. Historisk läsning består.
- Förväntade konflikter har stabil Problem Details; orelaterade DB-fel ska nå hostens felhantering, inte döljas av blanket catch/retry.

**Klart när:** samtidighet och felinjektion visar atomisk append/latest och ingen partiell publicering. Eventuella produktionsfixar följer samma ett-test-i-taget-loop.

## Etapp 5 — Exact-version-kontrakt och paginerad versionsläsning

**Omfattning: en normal agentsession.** Bygg ovanpå nu stabil publicering och exakt HTTP-läsning.

- Introducera Agreements-owned reader och typade immutable resultat enligt gränssnittsförslaget. Implementationen läser en exakt version med dess fullständiga kommersiella inputs och metadata, utan att ladda all history.
- Implementera versionslistans HTTP-endpoint med descending ordinal-keyset, default 50 och max 100. Hämta högst pageSize+1 versioner och bara deras barnrader; aldrig hela versionshistoriken.
- Cursor är opak, validerad och knuten till Agreement; felaktig cursor/pageSize ger fältadresserbar 400. En senare publicering får inte duplicera/förskjuta fortsättningssidans redan passerade versioner.
- Versionslista och exakt HTTP-GET använder samma rättigheter som befintlig agreement-läsning. Fel Agreement/version-kombination och annan parts resurser ger scope-säker 404.
- Uppdatera arkitekturtesternas avsiktliga kontraktslista och säkerställ att endast kontrakt, inga implementationer, blir externa beroenden.

**Prioriterade red–green-beteenden:** en konsument kan rekonstruera alla oförändrade inputs för gammal version efter ny publicering; okänd version ger NotFound; båda parters Member kan lista efter Ended; tom lista; 50/100-gränser och ogiltiga värden; sista sidan; fortsättning under ny publicering; cursor från fel Agreement; inga versioner saknas eller återkommer.

**Klart när:** HTTP och modulreader ger samma kompletta versionfakta, alla sidor är begränsade queries och modulerna förblir isolerade. Lägg inte till en separat publik modulkontraktsyta för listning utan en konkret konsument; issue kräver exact-version-kontrakt och paginerad HTTP-lista.

## Etapp 6 — Slutlig kravverifiering, dokumentation och review

**Omfattning: en kortare separat agentsession, reserverad för integration och avslut.**

- Gå igenom varje acceptance criterion i #61 och koppla det till namngivna tester. Lägg endast till test som täcker en identifierad lucka.
- Kontrollera att inga edit/delete/republish-kommandon finns, gamla versioner förblir oförändrade och kompletta versioner inte kan få blandade child-rader vid rollback.
- Kontrollera att inga Customer selections/acceptances, Customer Price Projections, kalkyler, Price Publication-skrivningar eller externa transaktioner tillkommit.
- Verifiera att migrationskedjan från #60 går att applicera med samtliga modulmigrationer och att samma-module constraints håller. Behåll tidigare etappers migrationshistorik.
- Uppdatera README med begäran/svar, felkoder, first/next/retry, datum, pagination och konsumentkontrakt. Ändra domändokument/ADR endast om ett faktiskt nytt beslut behöver dokumenteras.
- Kör slutlig `/review` över hela diffen mot `Issue-13`, åtgärda fynd och dokumentera kvarstående begränsningar. PR #68 ska beskriva den färdiga funktionen och verifieringen.

**Slutkontroller:**

```powershell
dotnet build PriceSync.slnx
dotnet test PriceSync.slnx
```

För riktad körning under etapperna:

```powershell
dotnet test tests/PriceSync.Modules.Agreements.IntegrationTests --filter FullyQualifiedName~<TestClass>
dotnet test tests/PriceSync.ArchitectureTests
dotnet test tests/PriceSync.Modules.PricePublication.IntegrationTests
```

**Klart när:** alla kriterier har testbevis, build och hela sviten är gröna, review-fynd är hanterade och dokumentation/PR matchar slutresultatet. Att markera PR redo eller mergea är ett separat beslut.

## Kravtäckning och beroenden

| Kravområde i #61 | Primär etapp | Slutbevis |
| --- | --- | --- |
| Supplier Owner, aktiv Organization/relation, Member-läsning | 1 | 4–6 |
| Identitet, ordinal, publiceringsfakta, immutable fullständiga versioner | 1, 3 | 5–6 |
| Giltighet, General/net, precision, dubbletter, exakta koder | 2 | 6 |
| Riktig all-or-nothing scope-verifiering och retryable 503 | 1–2 | 6 |
| Fullständig bevarad rounding, inga prisberäkningar | 1, 3 | 5–6 |
| Expected predecessor, lås, atomisk append/latest, retries | 3–4 | 6 |
| Ändrade systemdefaults och hållbar begäransjämförelse | 3 | 6 |
| Egna migrationer och same-module constraints | 1–2, 4 | 6 |
| Exact GET, konsumentkontrakt, paginerad komplett lista | 1, 5 | 6 |
| Concurrency, rollback och point-in-time-status | 4 | 6 |
| Ingen acceptance/activation/projektion eller extern skrivning | Alla | 6 |

Ordning: **1 → 2 → 3 → 4 → 5 → 6**. Varje etapp börjar från föregående gröna commit. Om en session inte räcker: avsluta vid en grön vertikal del, commit och dokumentera exakt återstående beteenden; nästa session slutför samma etapp före nästa. Lämna inte en röd testsvit som överlämning.

## Handoff mellan sessioner

Uppdatera denna sektion vid varje etappavslut:

- Senast klara etapp: ingen; endast planering genomförd.
- Implementationens startcommit: den tomma förberedelsecommiten `37a2381` på `issue-61` (plancommiten kan tillkomma därefter).
- Nästa steg: godkänn föreslagna gränssnitt och prioriterade beteenden, starta sedan etapp 1 med `/implement` och `/tdd`.
- Körda tester: inga; denna ändring är endast dokumentation.
- Öppna beslut/blockeringar: inga identifierade domänkonflikter. Gränssnittsnamn och request-DTO:ns konkreta JSON-form ska stämmas av mot detta planförslag innan kod skrivs.

Startprompt för nästa session:

> Använd /implement och /tdd för nästa ofärdiga etapp i docs/implementation-plan-issue-61.md. Läs aktuell issue #61 och handoff först. Arbeta på issue-61 och genomför bara den etappen med ett beteende i taget. Kör föreskrivna kontroller, använd /review, committa och uppdatera handoff. Planen är godkänd; följ dess gränssnitt och testprioriteringar. Fortsätt inte till nästa etapp i samma session.
