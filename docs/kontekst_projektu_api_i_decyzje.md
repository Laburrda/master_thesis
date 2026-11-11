# Kontekst projektu: decyzje i kontrakt API (wersja 1, 2026-10-14)

> Ten plik jest nadrzędnym źródłem prawdy o decyzjach projektowych i kontrakcie API. Ma być załączany do każdej sesji z agentem (Claude Code). Jeśli coś w rozmowie lub w kodzie różni się od tego pliku, obowiązuje plik. Zmiany decyzji wprowadza wyłącznie właściciel projektu i aktualizuje ten plik.

Dokumenty uzupełniające (nie zmieniają niczego, co jest tu ustalone):
- `plan_pracy_magisterskiej.md`: temat, teza, harmonogram, zakres.
- Baza wiedzy z bibliografii: https://claude.ai/artifact/VWhJjfgzvvB2MFv5Aajf5w (uzasadnienia metodologiczne i ryzyka).

Oznaczenia statusu decyzji:
- **[USTALONE]**: nie zmieniać bez zgody właściciela.
- **[DOMYŚLNE]**: przyjęte robocze rozwiązanie, wdrażać, ale zgłosić, jeśli okaże się problematyczne.
- **[OTWARTE]**: jeszcze nie rozstrzygnięte, nie implementować na własną rękę, zapytać.

---

## 0. Zasady pracy agenta (czytać najpierw)

1. **Nie rozszerzaj zakresu.** To środowisko badawcze, nie platforma sklepowa. Nie dodawaj endpointów, encji, uwierzytelniania, koszyka, płatności, frontendu ani żadnej logiki spoza tego pliku.
2. **Nie optymalizuj wydajności na własną rękę** (cache, dodatkowe indeksy, batchowanie, zmiany pul połączeń, równoległość). To, co jest mierzone, nie może się zmieniać po cichu. Każdy parametr wpływający na wydajność (rozmiar puli połączeń, `prefetch`, timeouty, limity Kestrela, liczba konsumentów) ma być w konfiguracji i opisany w dokumentacji.
3. **Oba warianty (synchroniczny i zdarzeniowy) muszą współdzielić logikę biznesową.** Różnią się wyłącznie tym, gdzie i kiedy ta sama logika jest wywoływana (patrz sekcja 5 i 7).
4. **Kontrakt API jest wspólny** dla obu wariantów. Dozwolone różnice opisano w sekcji 6.3 i tylko te.
5. **Brak endpointów testowych w API** (reset, seed, statystyki). Seed, reset i weryfikacja poprawności odbywają się skryptami i bezpośrednio na bazie.
6. W razie konfliktu, niejasności lub pomysłu, który zmieniałby decyzję z tego pliku: zatrzymaj się i zapytaj.

---

## 1. Projekt w skrócie [USTALONE]

**Tytuł:** Wpływ stylu architektonicznego na skalowalność i latencję systemów pod obciążeniem: analiza porównawcza architektury synchronicznej i zdarzeniowej.

**Teza:** wraz ze wzrostem liczby równoległych użytkowników architektura zdarzeniowa wykazuje łagodniejszą degradację przepustowości niż synchroniczna, kosztem systematycznie wyższej latencji pojedynczego żądania, przy identycznym zakresie funkcjonalnym i gwarancji poprawności operacji na współdzielonym, ograniczonym zasobie.

**System referencyjny:** flash sale, czyli sprzedaż z ograniczonym stanem magazynowym (np. 5000 użytkowników vs 100 sztuk). Tylko backend.

**Dwa porównywane systemy:**
- **Synchroniczny:** klient czeka na pełny wynik zakupu, łącznie z zapisem do bazy.
- **Zdarzeniowy:** API przyjmuje żądanie, publikuje zdarzenie do RabbitMQ i odpowiada bez czekania na przetworzenie; worker przetwarza zakup.

**Definicja "architektury zdarzeniowej" w tej pracy:** asynchroniczne przekazywanie wiadomości typu fire-and-forget (nadawca nie czeka na odpowiedź). To NIE jest RPC przez broker (kolejka żądań plus kolejka odpowiedzi, klient czeka) ani pub/sub typu fanout. Wzorzec konsumpcji: work queue (competing consumers).

---

## 2. Stack technologiczny

| Element | Decyzja | Status |
|---|---|---|
| Język / platforma | C# na aktualnej wersji LTS .NET (zweryfikować w dokumentacji) | [USTALONE] |
| API | ASP.NET Core Web API, kontrolery (nie minimal API) | [DOMYŚLNE] |
| Styl kodu | `async/await` w całości; zakaz blokującego czekania (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`) | [USTALONE] |
| Baza | PostgreSQL | [DOMYŚLNE] |
| Dostęp do danych | EF Core do zwykłego CRUD i odczytów; operacja zakupu jako surowy SQL (sekcja 7) | [DOMYŚLNE] |
| Broker | RabbitMQ, oficjalna biblioteka `RabbitMQ.Client` (bez MassTransit i podobnych nakładek, bo ukrywają ustawienia, które trzeba opisać) | [USTALONE] |
| Środowisko | Docker + docker compose, identyczne limity CPU/RAM per wariant | [USTALONE] |
| Generator obciążenia | k6 (uruchamiany poza kontenerami z limitami zasobów) | [DOMYŚLNE] |
| Cache / baza odczytowa | Redis (`StackExchange.Redis`), patrz sekcja 8 | [OTWARTE] |

Wersje bibliotek i API `RabbitMQ.Client` sprawdzać w aktualnej dokumentacji przed użyciem (API klienta zmieniało się na w pełni asynchroniczne).

---

## 3. Struktura rozwiązania [DOMYŚLNE]

```
Shop.sln
 Shop.Contracts      // DTO żądań/odpowiedzi, enumy statusów, kody błędów, kontrakt zdarzeń
 Shop.Data           // encje, DbContext, migracje, seed, PurchaseService (logika zakupu)
 Shop.Api.Sync       // wariant synchroniczny: kontroler wywołuje PurchaseService bezpośrednio
 Shop.Api.Event      // wariant zdarzeniowy: kontroler publikuje zdarzenie do RabbitMQ
 Shop.Worker         // konsument RabbitMQ: wywołuje TEN SAM PurchaseService
 loadtests/          // skrypty k6, konfiguracja scenariuszy
 scripts/            // reset + seed, weryfikacja poprawności, zbieranie metryk
 docker-compose*.yml // osobne profile/pliki dla wariantów
```

Zasada: `PurchaseService` i odczyty produktów żyją w `Shop.Data` i są współdzielone. Odczyt produktu jest schowany za interfejsem (np. `IProductReader`), z jedną implementacją czytającą z bazy (powód: patrz sekcja 8).

---

## 4. Zakres funkcjonalny

### Wchodzi [USTALONE]
- Katalog produktów z limitowanym stanem (lista, szczegóły). Tylko odczyt, dane z seeda.
- Złożenie zamówienia na jeden produkt (zapis z dekrementacją stanu, rywalizacja o zasób).
- Pobranie zamówienia.
- (Opcjonalnie, na końcu) dodanie recenzji jako zapis bez rywalizacji (grupa kontrolna).

### Świadomie wykluczone [USTALONE]
Frontend, uwierzytelnianie i autoryzacja (rejestracja, logowanie, tokeny), tabela użytkowników i adresów, koszyk, edycja/usuwanie recenzji, historia zamówień użytkownika, płatności, dodawanie/edycja produktów przez API, personalizacja, rabaty, wiele mikroserwisów, endpointy administracyjne i testowe.

Uzasadnienie (do pracy): uwierzytelnianie dodaje drogie CPU (hashowanie haseł), które zniekształciłoby pomiary; koszyk dodaje stan i wieloetapowy flow, niezwiązany z badaną zmienną.

---

## 5. Architektura obu wariantów

### 5.1 Wariant synchroniczny [USTALONE]

```
Klient -> [LB, jeśli jest] -> API (Shop.Api.Sync) -> PurchaseService -> PostgreSQL
         <------------------------ odpowiedź z wynikiem końcowym ------------------
```

`POST /orders` wykonuje całą logikę zakupu w ramach żądania i zwraca status końcowy (`CONFIRMED` lub `REJECTED`).

### 5.2 Wariant zdarzeniowy [USTALONE, szczegóły niżej [DOMYŚLNE]]

```
Klient -> API (Shop.Api.Event) -> RabbitMQ (kolejka orders.requested) -> Shop.Worker -> PurchaseService -> PostgreSQL
         <- 202 PENDING (po potwierdzeniu publikacji)
```

Szczegóły:
- API waliduje tylko kształt żądania i nagłówki, publikuje zdarzenie `OrderRequested` i zwraca `202` ze statusem `PENDING`. [DOMYŚLNE]
- `202` zwracane dopiero po **potwierdzeniu publikacji przez brokera** (publisher confirm), czyli "przyjęte" oznacza "broker przyjął". [DOMYŚLNE]
- Kolejka trwała (durable), wiadomości trwałe (persistent), ręczne potwierdzanie (manual ack) **po** zatwierdzeniu transakcji w bazie. [DOMYŚLNE]
- Dostarczanie jest at-least-once, więc worker musi być **idempotentny** (unikalny klucz w bazie, sekcja 7). [USTALONE]
- `prefetch`, liczba konsumentów i równoległość workera: parametry konfiguracyjne, domyślnie 1 instancja workera, wartości zapisać do dokumentacji. [DOMYŚLNE]
- Każde zdarzenie niesie `correlationId` (z żądania klienta), umieszczany w nagłówkach wiadomości i we wszystkich logach. Wdrożyć od początku budowy wariantu zdarzeniowego. [USTALONE]
- Monitorować długość kolejki jako dodatkową metrykę (obserwacja kontroli przepływu RabbitMQ pod przeciążeniem). [DOMYŚLNE]
- Odczyty (`GET`) w wariancie zdarzeniowym NIE przechodzą przez brokera (klient potrzebuje wyniku od razu). [USTALONE]

### 5.3 Idempotencja w obu wariantach [USTALONE]
Ten sam mechanizm w obu wariantach (nagłówek `Idempotency-Key` plus unikalne ograniczenie w bazie), żeby koszt idempotencji nie obciążał tylko jednej strony porównania. Automatyczne ponowienia po stronie klienta/LB w testach: wyłączone (lub identyczne w obu wariantach, zapisać w metodologii).

---

## 6. Kontrakt API

Baza ścieżek: `/online-store-api/v1`. JSON w UTF-8, pola w camelCase, czasy w ISO-8601 UTC, statusy i kody jako teksty WIELKIMI LITERAMI.

### 6.1 Wspólne

**Nagłówki:**

| Nagłówek | Gdzie wymagany | Opis |
|---|---|---|
| `X-User-Id` | `POST /orders`, `POST /products/{id}/reviews` | dodatnia liczba całkowita (long); identyfikator użytkownika bez uwierzytelniania, nie weryfikowany względem żadnej tabeli |
| `Idempotency-Key` | `POST /orders` | tekst do 64 znaków (zalecany UUID); unikalny per użytkownik per zamówienie |
| `X-Correlation-Id` | opcjonalny we wszystkich | jeśli brak, API generuje; propagowany do zdarzeń i logów |

**Format błędu** (dla każdego kodu 4xx/5xx):
```json
{ "error": { "code": "VALIDATION_ERROR", "message": "opis czytelny dla człowieka" } }
```
Kody błędów: `VALIDATION_ERROR`, `MISSING_HEADER`, `PRODUCT_NOT_FOUND`, `ORDER_NOT_FOUND`, `IDEMPOTENCY_KEY_REUSED`, `INTERNAL_ERROR`.

**Identyfikatory:** `productId` to `long`; `orderId` to GUID generowany po stronie serwera.

### 6.2 Endpointy [USTALONE zakres; szczegóły pól [DOMYŚLNE]]

#### 1. `GET /products`
Lista produktów. Wyłącznie odczyt.

Parametry zapytania:

| Parametr | Domyślnie | Opis |
|---|---|---|
| `page` | 1 | numer strony, >= 1 |
| `pageSize` | 20 | 1..100 |
| `sort` | `name` | `name` lub `price` |
| `order` | `asc` | `asc` lub `desc` |
| `category` | brak | dokładne dopasowanie nazwy kategorii |
| `minPrice`, `maxPrice` | brak | zakres ceny (włącznie) |

Sortowanie zawsze z dodatkowym kluczem `id` (stabilna paginacja).

`200`:
```json
{
  "items": [ { "id": 1, "name": "Produkt 1", "category": "elektronika", "price": 199.99 } ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 2000
}
```
Lista celowo **nie zawiera stanu magazynowego** (stan jest tylko w szczegółach produktu; patrz sekcja 8).

#### 2. `GET /products/{id}`
`200`:
```json
{ "id": 1, "name": "Produkt 1", "category": "elektronika", "price": 199.99, "stockAvailable": 100 }
```
`404` z `PRODUCT_NOT_FOUND`, gdy produkt nie istnieje.

#### 3. `POST /orders`
Składa zamówienie na **jeden produkt**. Nagłówki: `X-User-Id`, `Idempotency-Key` (oba wymagane).

Żądanie:
```json
{ "productId": 1, "quantity": 1 }
```
Walidacja: `productId` > 0, `quantity` w zakresie 1..5. Naruszenie daje `400 VALIDATION_ERROR` (w obu wariantach, po stronie API).

Odpowiedź (to samo ciało w obu wariantach):
```json
{
  "orderId": "3f2b8c1e-9d4a-4c7e-8a55-1b2c3d4e5f60",
  "productId": 1,
  "quantity": 1,
  "status": "CONFIRMED",
  "rejectionReason": null,
  "createdAt": "2026-10-14T10:00:00Z",
  "processedAt": "2026-10-14T10:00:00Z"
}
```
- `status`: `PENDING` | `CONFIRMED` | `REJECTED`
- `rejectionReason`: `null` | `OUT_OF_STOCK` | `PRODUCT_NOT_FOUND`
- `processedAt`: `null` dopóki `PENDING`
- `createdAt`: moment przyjęcia żądania przez API (w wariancie zdarzeniowym przenoszony w zdarzeniu)

Kody odpowiedzi:

| Sytuacja | Synchroniczny | Zdarzeniowy |
|---|---|---|
| Zamówienie potwierdzone | `201`, `CONFIRMED` | n/d (wynik później) |
| Brak towaru / brak produktu | `409`, `REJECTED` + `rejectionReason` | n/d (wynik później) |
| Przyjęte do przetworzenia | n/d | `202`, `PENDING` |
| Powtórzenie z tym samym kluczem (ten sam użytkownik i treść) | `200` z istniejącym zamówieniem (ten sam `orderId`) | `200` z istniejącym zamówieniem lub `202`, wymagane: ten sam `orderId`, brak podwójnego zamówienia |
| Ten sam klucz z inną treścią | `422 IDEMPOTENCY_KEY_REUSED` | `422 IDEMPOTENCY_KEY_REUSED` (jeśli wykrywalne po stronie API), w przeciwnym razie ignorowane przez konsumenta |
| Błąd walidacji / brak nagłówka | `400` | `400` |

#### 4. `GET /orders/{id}`
`200` z ciałem jak wyżej. `404 ORDER_NOT_FOUND`, gdy nieznane.

W wariancie zdarzeniowym zamówienie może zwracać `404`, dopóki worker go nie przetworzy. [OTWARTE, patrz sekcja 11, punkt 3]. Harness NIE polega na pollingu tego endpointu, bo latencję end-to-end liczy się po fakcie z `createdAt`/`processedAt` w bazie.

#### 5. `POST /products/{id}/reviews` [OPCJONALNE, implementować na samym końcu]
Zapis bez rywalizacji, grupa kontrolna, poza główną macierzą testów. Nagłówek `X-User-Id`.

Żądanie: `{ "rating": 5, "comment": "tekst do 500 znaków" }`, `rating` w 1..5.
Odpowiedź: `201` `{ "id": 1, "productId": 1, "userId": 7, "rating": 5, "comment": "...", "createdAt": "..." }`.
Zachowanie w wariancie zdarzeniowym (analogicznie do zamówień: `202` + przetworzenie przez workera) do doprecyzowania przy implementacji. Nie implementować przed zamówieniami.

### 6.3 Dozwolone różnice między wariantami [USTALONE]
Wyłącznie: (a) kod HTTP `201`/`409` vs `202` w `POST /orders` oraz (b) wartość `status` w chwili odpowiedzi (`CONFIRMED`/`REJECTED` vs `PENDING`). Ścieżki, nagłówki, walidacja, formaty ciała i błędów są identyczne.

---

## 7. Model danych i logika zakupu

### 7.1 Tabele [DOMYŚLNE]

```
products: id bigint PK, name text, category text, price numeric(10,2),
          stock_initial int, stock_available int CHECK (stock_available >= 0)
          indeksy: category, price, name (te same w obu wariantach)

orders:   id uuid PK, user_id bigint, product_id bigint FK, quantity int,
          status text, rejection_reason text NULL, idempotency_key text,
          created_at timestamptz, processed_at timestamptz NULL,
          UNIQUE (user_id, idempotency_key)

reviews:  id bigint PK, product_id bigint FK, user_id bigint, rating int,
          comment text, created_at timestamptz     (opcjonalne)
```
Brak tabeli użytkowników. Odrzucone zamówienia też są zapisywane (pełny log do weryfikacji).

### 7.2 Niezmiennik poprawności [USTALONE]
Dla każdego produktu: suma `quantity` zamówień `CONFIRMED` <= `stock_initial` oraz `stock_available = stock_initial - suma(CONFIRMED)` i `stock_available >= 0`. To jest własność *safety*, nigdy nie może być naruszona w żadnym wariancie. Weryfikacja po każdym przebiegu testu skryptem bezpośrednio na bazie (bez endpointu).

### 7.3 Logika zakupu (`PurchaseService`, jedna implementacja dla obu wariantów) [USTALONE]

W jednej transakcji bazodanowej (poziom izolacji domyślny, wystarczający dzięki atomowemu `UPDATE`):
1. Wstaw rekord zamówienia z unikalnym `(user_id, idempotency_key)`. Konflikt oznacza duplikat, więc zwróć istniejące zamówienie i nic więcej nie rób.
2. Warunkowa dekrementacja jednym zapytaniem:
   ```sql
   UPDATE products
   SET stock_available = stock_available - @q
   WHERE id = @id AND stock_available >= @q;
   ```
3. Zmieniono 1 wiersz: status `CONFIRMED`. Zmieniono 0 wierszy: status `REJECTED` z `OUT_OF_STOCK` (lub `PRODUCT_NOT_FOUND`). Ustaw `processed_at`. Zatwierdź transakcję.

Zakaz stosowania "najpierw odczytaj stan, potem zapisz" (wyścig). Zakaz ręcznych blokad aplikacyjnych.

---

## 8. Odczyty i Redis (CQRS) [OTWARTE, decyzja po działającym wariancie synchronicznym]

**Stan domyślny (obowiązuje do czasu decyzji):** odczyty (`GET /products`, `GET /products/{id}`) czytają bezpośrednio z PostgreSQL w obu wariantach, za interfejsem `IProductReader` z jedną implementacją (baza). Redis NIE jest implementowany.

**Rozważane rozszerzenie (kandydat):** CQRS w OBU wariantach, czyli osobny model odczytowy w Redisie tylko dla `stockAvailable` w `GET /products/{id}`:
- Synchroniczny: po zatwierdzeniu transakcji API od razu aktualizuje Redis (w tym samym żądaniu).
- Zdarzeniowy: po zatwierdzeniu transakcji worker aktualizuje Redis.
- Baza pozostaje jedynym źródłem prawdy; zakup zawsze sprawdza i dekrementuje stan w bazie, nigdy w Redisie.
- Stan tylko maleje, więc aktualizacja kopii może mieć semantykę "ustaw, jeśli nowa wartość jest niższa" (odporność na duplikaty i zmianę kolejności bez numerów wersji).
- Ryzyko "dual write" (baza zatwierdzona, aktualizacja Redisa zgubiona): krótki TTL plus test zgodności Redis z bazą po każdym przebiegu.
- Nowa metryka: świeżość odczytu (opóźnienie od zatwierdzenia zakupu do zmiany w Redisie; liczba odczytów pokazujących dostępność przy stanie 0).
- Teza o poprawności zyskałaby drugi wymiar: bezpieczeństwo (nie sprzedać ponad stan) vs świeżość (spektrum spójności; nie powoływać się wprost na CAP, bo środowisko nie ma partycji sieciowych; dopuszczalne spektrum spójności Kleppmanna i PACELC, źródła do zweryfikowania).
- Macierz warunków pozostaje 20; rośnie analiza, nie liczba warunków.

Agent: do czasu wyraźnej decyzji właściciela nie dodawaj Redisa, tylko utrzymuj interfejs odczytu gotowy na drugą implementację.

---

## 9. Metodologia (skrót do implementacji harnessu)

### 9.1 Macierz [USTALONE]
2 architektury x 2 typy obciążenia x 5 skal = 20 warunków, min. 5 powtórzeń każdy. Skala (symulowani użytkownicy): 10 / 100 / 1000 / 5000 / 10000.

### 9.2 Scenariusze
- **Write-heavy (flash sale):** użytkownicy równolegle składają `POST /orders` na "gorący" produkt o małej puli (np. 5000 prób vs 100 sztuk). Każda próba ma unikalny `Idempotency-Key`. [USTALONE]
- **Read-heavy = ruch mieszany:** większość `GET` (lista i szczegóły), mały odsetek `POST /orders`. Proporcje wstępnie ok. 95% / 5% [DOMYŚLNE, do potwierdzenia]. Uzasadnienie: odczyty nie przechodzą przez brokera, więc różnica architektur pojawia się dzięki zakupom, które w wariancie synchronicznym piszą do bazy bezpośrednio i konkurują z odczytami, a w zdarzeniowym idą przez kolejkę.
- **Czysty odczyt** jako punkt odniesienia: [OTWARTE], tani dodatek w harnessie.

### 9.3 Metryki [USTALONE]
Przepustowość (żądania/s), latencja p50/p95/p99 (opcjonalnie p75), odsetek błędów, **poprawność** (nadsprzedaż = 0, zgodność stanu z logiem zamówień, brak duplikatów), długość kolejki RabbitMQ (wariant zdarzeniowy).

### 9.4 Definicja czasu odpowiedzi w wariancie zdarzeniowym [USTALONE]
Raportować oba, w obu wariantach:
- **Czas odpowiedzi HTTP** (czas do przyjęcia żądania), mierzony przez k6.
- **Czas end-to-end do stanu końcowego** = `processed_at - created_at` z bazy (bez pollingu, żeby nie zniekształcać obciążenia).

### 9.5 Warunki kontrolowane [USTALONE]
Identyczne limity CPU/RAM kontenerów, ten sam sprzęt, identyczny wolumen i seed danych, identyczne indeksy, identyczne żądania API, jeden niezależny generator obciążenia dla obu wariantów (poza kontenerami z limitami), rozgrzewka przed pomiarem, ten sam dzień pomiarowy dla grupy testów. Wszystkie parametry konfiguracyjne (pule połączeń, prefetch, timeouty, liczba wątków/instancji) zapisane dla odtwarzalności i opisane jako zagrożenia dla trafności ("jedna technologia jako reprezentant paradygmatu").

### 9.6 Model obciążenia [DOMYŚLNE]
Główny: model zamknięty (stała liczba użytkowników, uzasadnienie: flash sale ma ograniczoną liczbę realnych klientów). Dodatkowo co najmniej jeden scenariusz w modelu otwartym (k6 `arrival-rate`) jako kontrola spójności wniosków i obrona przed coordinated omission. Kryterium "degradacji/przeciążenia" (np. p95 > X ms lub odsetek błędów > Y%) zdefiniować PRZED fazą eksperymentów [OTWARTE].

### 9.7 Seed i reset [DOMYŚLNE]
Deterministyczny seed (stałe ziarno), identyczny dla obu wariantów. Robocza propozycja: 2000 produktów w 10 kategoriach; produkty o id 1..5 jako "gorące" ze stanem 100; pozostałe ze stanem dużym (np. 100000). Reset i seed między przebiegami skryptem (np. `docker compose down -v` plus seed), nie endpointem.

---

## 10. Zagrożenia z literatury, które wpływają na implementację

- **At-least-once w RabbitMQ:** możliwe duplikaty, stąd idempotentny konsument (unikalny indeks w bazie). Osobny mały test: zabicie workera w trakcie przetwarzania i sprawdzenie, czy nie ma duplikatów ani nadsprzedaży.
- **Brak obserwowalności między usługami:** `correlationId` od początku, w nagłówkach wiadomości i logach.
- **Kontrola przepływu (backpressure) RabbitMQ:** pod przeciążeniem broker może spowalniać producentów, więc wariant "asynchroniczny" zacznie się zachowywać synchronicznie. Monitorować i opisać.
- **Pula połączeń i blokada wiersza gorącego produktu** to naturalne wąskie gardła w ASP.NET Core z `async/await` (wątek jest zwalniany na czas czekania na bazę). Parametry (np. rozmiar puli Npgsql) są świadomie ustawiane i dokumentowane.
- Wyniki z innych badań nie są przenoszone liczbowo (zależą od implementacji).

---

## 11. Otwarte decyzje (nie rozstrzygać samodzielnie)

1. **Redis / CQRS** (sekcja 8): decyzja po działającym wariancie synchronicznym.
2. **Zasoby i topologia:** liczba instancji API, obecność load balancera (plan wspomina o LB w wariancie synchronicznym, a jednocześnie "po jednej usłudze aplikacyjnej na wariant"), liczba instancji workera, oraz czy limity CPU/RAM liczone są per kontener czy na cały system (wariant zdarzeniowy ma więcej kontenerów: broker, worker). Domyślnie na start: 1 instancja API, 1 worker.
3. **`GET /orders/{id}` w wariancie zdarzeniowym przed przetworzeniem:** (a) API tylko publikuje zdarzenie, więc `404` do czasu przetworzenia [rekomendowane], lub (b) API wstawia wiersz `PENDING` do bazy przed publikacją (dodatkowy zapis w API i ryzyko niespójności między zapisem a publikacją). Wpływa na tezę, bo zmienia pracę wykonywaną przez API.
4. **Mechanizm idempotencji po stronie API w wariancie zdarzeniowym** (np. deterministyczny `orderId` wyliczany z klucza, aby powtórzenie zwracało ten sam `orderId` bez zapytania do bazy).
5. **Proporcje scenariusza mieszanego**, rozmiar seeda, które produkty są "gorące".
6. **Kryteria degradacji** (progi p95 / odsetek błędów) i definicja przeciążenia.
7. **Czysty odczyt** jako scenariusz odniesienia.

---

## 12. Sugerowana kolejność prac

1. Faza 2: kontrakt API i model danych (ten plik), repozytorium, docker compose z PostgreSQL, seed.
2. Szkielet `Shop.Contracts`, `Shop.Data` (encje, migracje, `PurchaseService`, `IProductReader`) i `Shop.Api.Sync` z endpointami 1-4.
3. Testy funkcjonalne wariantu synchronicznego (w tym test nadsprzedaży pod współbieżnością).
4. **Punkt decyzyjny:** Redis / CQRS (sekcja 8) oraz otwarte decyzje 2-4.
5. Wariant zdarzeniowy: RabbitMQ, `Shop.Api.Event`, `Shop.Worker`, ten sam zestaw testów funkcjonalnych.
6. Harness (k6, skrypty weryfikacji poprawności, zbieranie metryk), potem eksperymenty.
7. Endpoint recenzji (opcjonalnie), jeśli zostanie czas.
