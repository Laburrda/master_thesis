# Kontekst: baza danych i seed (wersja 4, 2026-10-21)

> Uzupelnienie do `kontekst_projektu_api_i_decyzje.md` (nadrzedny). Niczego tam nie zmienia. Punkty z sekcji 6 wymagaja wpisu wlasciciela w pliku glownym. Oznaczenia jak w pliku glownym: [USTALONE], [DOMYSLNE], [OTWARTE].

## 0. WAZNE: jezyk kodu i danych [USTALONE]

Projekt trafi do portfolio i bedzie ogladany przez rekruterow i programistow, ktorzy niekoniecznie znaja polski. Dlatego:

- **Po angielsku:** komentarze w kodzie, nazwy (tabele, kolumny, klasy, zmienne, constrainty, indeksy), komunikaty skryptow i logow, komunikaty bledow, opisy commitow oraz **dane seeda** (nazwy produktow i kategorii).
- **Po polsku moga zostac tylko** pliki dokumentacyjne (`.md` i podobne), w tym ten plik i plik glowny.
- Styl komentarzy: krotkie, rzeczowe, wyjasniajace *dlaczego* (nie powtarzajace *co* robi kod). Bez dekoracyjnych banerow, emoji i przegadanych opisow.
- Dotyczy tez wszystkich przyszlych plikow: C#, k6, docker compose, skrypty. Agent ma stosowac te zasade bez przypominania.

Skutki dla juz ustalonych rzeczy: kategorie w seedzie sa po angielsku (`electronics`, `home`, `garden`, `sports`, `fashion`, `toys`, `books`, `automotive`, `health`, `groceries`), a produkty nazywaja sie `Product N`. Filtr `category` w `GET /products` to dokladne dopasowanie, wiec k6 musi uzywac tych nazw. Przyklady w pliku glownym (sekcja 6.2: `"Produkt 1"`, `"elektronika"`) wymagaja poprawienia na angielskie przez wlasciciela.

## 1. Co jest gotowe

Paczka `db-package/` (zweryfikowana na PostgreSQL 16 poza Dockerem; docelowo obraz `postgres:17`; sam `docker-compose.db.yml` nie byl uruchomiony, sprawdzic przy pierwszym `up`):

| Plik | Rola |
|---|---|
| `db/init/01_schema.sql` | tabele `products`, `orders` + indeksy z 7.1 (`reviews` zakomentowane) |
| `db/init/02_seed.sh` | deterministyczny seed (bez `random()`), parametry z env |
| `docker-compose.db.yml` | sama baza, `max_connections=200`, healthcheck, wolumen `pgdata` |
| `scripts/reset_db.sh` | `down -v` + `up --wait` = czysty stan + seed |
| `scripts/verify_db.sh` | niezmienniki (kod wyjscia 1 przy naruszeniu), miks wynikow, e2e p50/p95/p99 |
| `db/verify/*.sql` | zapytania uzywane przez skrypt weryfikacji |
| `.gitattributes` | LF w `.sh`/`.sql` (CRLF z Windowsa psuje skrypty w kontenerze) |

Mechanizm: obraz `postgres` wykonuje pliki z `/docker-entrypoint-initdb.d/` tylko przy PUSTYM wolumenie; reset = skasowanie wolumenu. Brak lokalnej instalacji Postgresa; polaczenie z hosta: `localhost:5432`, baza `shop`, user `shop`.

## 2. Model danych [DOMYSLNE, zgodny z 7.1 z jednym odstepstwem]

- `products(id, name, category, price, stock_initial, stock_available)`, `CHECK (stock_available >= 0)`. Indeksy: category, price, name (bez innych).
- `orders(id uuid, user_id, product_id, quantity 1..5, status, rejection_reason, idempotency_key varchar(64), created_at, processed_at)`, `UNIQUE (user_id, idempotency_key)`.
- Stany magazynowe zyja w `products`; `orders` jest logiem do weryfikacji.
- **Brak tabel:** `users` [USTALONE, sekcja 4], `order_items` (zamowienie = jeden produkt, brak koszyka), `reviews` (do czasu opcjonalnego endpointu).
- **`orders.product_id` bez FK**, bo 7.3 wstawia zamowienie PRZED dekrementacja i zapisuje tez odrzucenia `PRODUCT_NOT_FOUND`. Skutek: bez FK baza nie pilnuje istnienia produktu, wiec pilnuje tego skrypt weryfikacji (kontrole `confirmed_without_product`, `not_found_but_product_exists`).
- `CHECK`-i spojnosci: REJECTED <=> jest `rejection_reason` (`ck_orders_rejection`); PENDING <=> brak `processed_at` (`ck_orders_processed`). Unikalnosc idempotencji: `uq_orders_user_idempotency`. Nazwy constraintow warto zachowac w mapowaniu EF Core i w obsludze bledow.
- `created_at` ustawia API (nie baza), bo w wariancie zdarzeniowym jest przenoszone w zdarzeniu.

## 3. Seed [DOMYSLNE]

Env w compose: `SEED_TOTAL_PRODUCTS=2000`, `SEED_HOT_COUNT=100`, `SEED_HOT_STOCK=100`, `SEED_COLD_STOCK=100000`.

- Katalog 2000 produktow (`Product 1` .. `Product 2000`), 10 kategorii po 200 (nazwy angielskie, patrz sekcja 0); cena i kategoria z wzorow deterministycznych.
- Gorace = id 1..100, stan 100 (razem 10 000 sztuk), podzbior katalogu. Reszta: stan 100 000.
- Pula `X-User-Id` dla k6: 1..10000 (nic nie jest seedowane w bazie).
- Zakresy id dla k6 (katalog, gorace) czytac z tych samych parametrow co seed, nie wpisywac drugi raz na sztywno. Zakresow nie wpisujemy do API ani workera (walidacja ksztaltu `productId > 0`, istnienie produktu sprawdza baza).

## 4. Niezmiennik i weryfikacja [USTALONE]

Dla kazdego produktu: `sum(quantity CONFIRMED) <= stock_initial` oraz `stock_available = stock_initial - sum(CONFIRMED)` i `>= 0`. Dodatkowo: brak `PENDING` po przebiegu, brak duplikatow `(user_id, idempotency_key)`, brak `CONFIRMED` na nieistniejacy produkt, `PRODUCT_NOT_FOUND` tylko gdy produktu faktycznie nie ma. Weryfikacja: `scripts/verify_db.sh` po kazdym przebiegu, przed resetem.

## 5. Zasady i uzasadnienia (material do pracy)

- Brak `users`/`order_items`: kazda dodatkowa tabela z FK lub join dolozylaby prace na sciezce zakupu w obu wariantach; zmienna badana to rywalizacja o jeden ograniczony zasob.
- Pula 100 gorących x 100 sztuk zamiast jednego produktu: przy jednym produkcie po ~30 zakupach reszta to tanie odrzucenia, a wyniki zdominowalby jeden wiersz.
- `PRODUCT_NOT_FOUND` vs `OUT_OF_STOCK`: dwie rozne przyczyny odrzucenia. W wariancie zdarzeniowym API nie sprawdza bazy, wiec istnienie produktu ustala worker; wynik trafia do zamowienia, nie do bledu HTTP. W pomiarach praktycznie nie wystepuje (k6 uzywa istniejacych id), to przypadek do testu funkcjonalnego.
- `202 PENDING` oznacza "przyjete do przetworzenia", nie "zakup udany". Informowanie klienta o wyniku (polling/push/mail) nie jest budowane (brak frontendu); harness liczy czas do stanu koncowego z bazy. To koszt architektury zdarzeniowej do opisania w wnioskach. Ten sam mechanizm dotyczy rozjazdu CQRS: wyswietlana dostepnosc jest wskazowka, wiazace jest rozstrzygniecie bazy (`REJECTED / OUT_OF_STOCK`).
- Schemat budowany skryptami SQL, EF Core tylko mapuje klasy na istniejace tabele (bez migracji). Zaleta: identyczny stan startowy niezaleznie od aplikacji. Ryzyko: rozjazd klas C# i schematu, widoczny przy pierwszym uruchomieniu na zaseedowanej bazie.
- `max_connections=200`: kazde polaczenie to osobny proces Postgresa; domyslny limit 100 dotyczy sumy polaczen wszystkich klientow. Pule Npgsql (API + worker) musza miescic sie w limicie, inaczej wynik to bledy "too many clients" zamiast pomiaru. 10 000 symulowanych uzytkownikow to kolejka zadan do puli, nie 10 000 polaczen. Rozmiar puli jest parametrem w konfiguracji.

## 6. Do wpisania w pliku glownym przez wlasciciela

1. `orders.product_id` bez FK (uzasadnienie w sekcji 2) [zaakceptowane].
2. Dodatkowe `CHECK`-i spojnosci w `orders` [zaakceptowane].
3. Parametry seeda (sekcja 3); zamyka czesc otwartej decyzji 5 (rozmiar seeda, gorace produkty), z pula stala dla wszystkich 20 warunkow [zaakceptowane].
4. `max_connections=200`, pozostale ustawienia Postgresa domyslne obrazu [zaakceptowane do wyjasnienia w sekcji 5].
5. Schemat i seed ze skryptow SQL, EF Core tylko do mapowania; poprawic zapis o migracjach w sekcji 3 pliku glownego [zaakceptowane, o ile nie okaze sie problemem przy `Shop.Data`].
6. `PRODUCT_NOT_FOUND` jako przypadek brzegowy poza macierza testow, pilnowany kontrolami w `verify_db.sh`.
7. Write-heavy: stale `quantity=1`, stala liczba prob K na VU (K ustalane w pilotazu przed eksperymentami) [zaakceptowane].

## 7. Otwarte

1. ~~FK na `orders.product_id`~~: ROZSTRZYGNIETE (bez FK, zaakceptowane 2026-10-21).
2. ~~Budzet prob~~: ROZSTRZYGNIETE co do zasady (stala liczba prob K na VU); wartosc K i kryteria degradacji ustalic w pilotazu, przed eksperymentami.
3. ~~`quantity` w write-heavy~~: ROZSTRZYGNIETE (`quantity=1`); walidacja 1..5 testowana funkcjonalnie osobno.
4. **Cel `POST` w scenariuszu mieszanym i `X-User-Id`:** rekomendacja: tylko gorace (1..100), wyliczane deterministycznie z numeru VU i iteracji; `X-User-Id = numer VU`; nowy `Idempotency-Key` w kazdej probie; ponowienia wylaczone. Nie komentowane przez wlasciciela, traktowac jako [DOMYSLNE].
5. **Limity CPU/RAM bazy:** modyfikowane w pilotazu, ZAMROZONE i zapisane przed 20 warunkami glownymi (zmiana w trakcie eksperymentu unieważnia porownanie).
6. **Skalowana pula produktow:** tylko jako kontrola, gdy pilotaz pokaze dominacje odrzucen; kryterium "dominacji" zapisac przed pilotazem.
7. **Pin tagu obrazu `postgres`:** po pierwszym `up` zapisac `SELECT version();` w opisie warunkow pomiaru i wpisac dokladny tag w compose.
8. **Srodowisko:** Ubuntu (WSL2), repozytorium w `~/` (nie `/mnt/c/`, czyli dysk Windowsa przez wolna warstwe), limity z `.wslconfig` wpisac do opisu warunkow.
9. Poza schematem, bez zmian wzgledem pliku glownego: Redis/CQRS (sekcja 8) oraz `GET /orders/{id}` w wariancie zdarzeniowym (decyzja 3: 404 do czasu przetworzenia vs wiersz PENDING; schemat obsluguje obie opcje).

## 8. Uruchomienie

```bash
docker compose -f docker-compose.db.yml up -d --wait   # start + seed
./scripts/verify_db.sh                                 # po przebiegu
./scripts/reset_db.sh                                  # czysty stan
```
