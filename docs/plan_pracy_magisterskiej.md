# Plan pracy magisterskiej — dokument kontekstowy

> Ten dokument służy jako pełny kontekst projektu. Można go załączać do przyszłych rozmów, żeby nie tłumaczyć wszystkiego od nowa.

---

## 1. Tytuł pracy

**"Wpływ stylu architektonicznego na skalowalność i latencję systemów pod obciążeniem: analiza porównawcza architektury synchronicznej i zdarzeniowej"**

## 2. Teza badawcza

> Wraz ze wzrostem liczby równoległych użytkowników architektura zdarzeniowa wykazuje łagodniejszą degradację przepustowości niż architektura synchroniczna, kosztem systematycznie wyższej latencji pojedynczego żądania, przy zachowaniu identycznego zakresu funkcjonalnego porównywanych systemów oraz gwarancji poprawności operacji na współdzielonym, ograniczonym zasobie.

**Status:** teza i temat wysłane do promotorki (Pani Profesor) z prośbą o akceptację i wpisanie na listę seminarzystów. Oczekiwanie na odpowiedź.

## 3. Wyjaśnienie kluczowych pojęć (dla własnej orientacji)

- **Tytuł** operuje pojęciem szerszym — *skalowalność* jako ogólna zdolność systemu do radzenia sobie z rosnącym obciążeniem.
- **Teza** operacjonalizuje to pojęcie w konkretną, mierzalną zmienną — *degradację przepustowości* (spadek liczby obsłużonych żądań/s wraz ze wzrostem liczby użytkowników).
- Skalowalność i latencja są badane łącznie (nie jako dwa oddzielne wątki), ponieważ są fizycznie powiązane Prawem Little'a — wzrost obciążenia podnosi latencję aż do punktu nasycenia systemu, a różne architektury mają różny kształt tej krzywej degradacji.

---

## 4. Domena systemu referencyjnego

**System sprzedaży z ograniczonym stanem magazynowym ("flash sale")** — uproszczony e-commerce, w którym produkty mają małą, limitowaną pulę sztuk, a eksperyment celowo generuje równoczesny napór wielu użytkowników na ten sam zasób (np. 5000 użytkowników vs 100 sztuk produktu).

**Uzasadnienie wyboru:**
- Naturalny podział na read-heavy (przeglądanie katalogu, sprawdzanie dostępności) i write-heavy (próba zakupu)
- Wprowadza dobrze udokumentowany w literaturze problem inżynierski: *overselling problem*
- Niższe ryzyko techniczne niż np. system rezerwacji z logiką wygasania/TTL
- Dodaje trzecią mierzoną zmienną obok przepustowości i latencji: **poprawność systemu pod obciążeniem**

**Odrzucone alternatywy:**
- Czysty e-commerce bez ograniczonego zasobu — brak naturalnego konfliktu współbieżnego
- System rezerwacji miejsc — ciekawszy naukowo, ale wysokie ryzyko techniczne (logika blokad tymczasowych)
- System śledzenia dostaw — brak sensownego scenariusza read-heavy

---

## 5. Zakres pracy

### Wchodzi w skład obu systemów (identyczne funkcjonalnie):
- Katalog produktów z limitowanym stanem magazynowym (listowanie, wyszukiwanie, szczegóły) — **read-heavy**
- Operacja zakupu z dekrementacją stanu magazynowego (próba zakupu, potwierdzenie/odrzucenie) — **write-heavy**
- Niezmiennik: liczba sprzedanych sztuk nigdy nie przekracza stanu początkowego
- Log/historia transakcji do weryfikacji poprawności po każdym teście
- Wyłącznie backend — brak frontendu/UI

### Świadomie wykluczone:
- Frontend/UI
- Uwierzytelnianie i autoryzacja użytkowników (uproszczony identyfikator użytkownika)
- Integracja z zewnętrznymi systemami płatności (symulacja in-memory)
- Wiele mikroserwisów — po jednej usłudze aplikacyjnej na wariant
- Personalizacja, rekomendacje, rabaty

---

## 6. Cele szczegółowe

1. Zaprojektować i zaimplementować dwa funkcjonalnie równoważne systemy różniące się wyłącznie stylem komunikacji wewnętrznej (synchroniczny vs zdarzeniowy)
2. Zaprojektować i przeprowadzić kontrolowany eksperyment obciążeniowy w obu wariantach, dla scenariuszy read-heavy i write-heavy, przy skalującej się liczbie użytkowników
3. Zmierzyć i porównać: przepustowość, latencję (p50/p95/p99) oraz poprawność pod obciążeniem
4. Sformułować wnioski dotyczące kompromisów (trade-offs) między badanymi stylami architektonicznymi w kontekście systemów o ograniczonym, współdzielonym zasobie

---

## 7. Decyzje technologiczne

| Element | Decyzja | Uzasadnienie |
|---|---|---|
| Broker zdarzeń | **RabbitMQ** | Niższa bariera wejścia niż Kafka, prostszy model (exchange/queue/routing), wystarczająca wydajność dla skali testów, mniejsze ryzyko błędów konfiguracyjnych |
| Środowisko | **Docker** | Identyczne zasoby obliczeniowe (CPU/RAM) dla obu wariantów, zapewnienie porównywalności |
| Kafka | odrzucona | Wymaga klastra i strojenia nieadekwatnego do warunków domowych — ryzyko, że wyniki odzwierciedlą złą konfigurację, nie właściwości architektury |

**Do ustalenia w kolejnych rozmowach:** język programowania i framework backendowy, baza danych, narzędzie do generowania obciążenia (kandydaci: k6, Locust, Gatling).

---

## 8. Metodologia eksperymentu

### Macierz zmiennych

| Wymiar | Warianty |
|---|---|
| Architektura | Synchroniczna (klient/LB/API/baza danych) vs Event-driven (RabbitMQ) |
| Typ obciążenia | Write-heavy vs Read-heavy |
| Skala (liczba symulowanych użytkowników) | 10 / 100 / 1000 / 5000 / 10000 |

**2 × 2 × 5 = 20 warunków testowych**, każdy powtórzony kilkukrotnie (np. 5×) w celu umożliwienia podstawowego wnioskowania statystycznego.

### Zmienne mierzone
- Przepustowość (żądania/s)
- Latencja: p50 / p95 / p99
- Poprawność systemu (czy liczba sprzedanych sztuk przekroczyła stan magazynowy)

### Warunki kontrolowane
- Identyczne limity CPU/RAM w kontenerach Docker dla obu wariantów
- Ten sam sprzęt/środowisko testowe
- Identyczny wolumen danych startowych
- Identyczne definicje operacji biznesowych i identyczne żądania API w obu systemach

---

## 9. Harmonogram szczegółowy (12 miesięcy)

### Faza 1 — Przegląd literatury (miesiąc 1, sierpień)
- Tydz. 1–2: zebranie i przegląd publikacji nt. stylów architektonicznych, CAP theorem, porównań REST vs message-driven systems
- Tydz. 3: identyfikacja 8–12 kluczowych źródeł do bibliografii wstępnej
- Tydz. 4: sformułowanie finalnej wersji rozdziału teoretycznego (szkic)
- **Kamień milowy:** wstępna bibliografia gotowa, akceptacja tematu przez promotorkę

### Faza 2 — Projekt wspólnej infrastruktury (wrzesień)
- Tydz. 1: definicja wspólnego modelu danych (produkty, stan magazynowy, transakcje)
- Tydz. 2: definicja kontraktu API (te same endpointy/żądania dla obu wariantów)
- Tydz. 3: konfiguracja środowiska Docker (limity zasobów, sieć, wspólne obrazy bazowe)
- Tydz. 4: przygotowanie danych testowych (zbiór produktów, początkowe stany magazynowe)
- **Kamień milowy:** wspólna baza, zbiór danych i definicja API gotowe i wersjonowane w repozytorium

### Faza 3 — Budowa systemu synchronicznego (październik–listopad, początek semestru)
- Miesiąc 1: implementacja API (klient → load balancer → API → baza danych), logika katalogu (read-heavy)
- Miesiąc 2: implementacja logiki zakupu z transakcyjną dekrementacją stanu magazynowego (write-heavy), testy funkcjonalne, dokumentacja architektury
- **Kamień milowy:** system synchroniczny działający, przechodzący testy funkcjonalne

### Faza 4 — Budowa systemu event-driven (grudzień–styczeń)
- Miesiąc 1: konfiguracja RabbitMQ, implementacja publikacji/konsumpcji zdarzeń dla katalogu i zakupu
- Miesiąc 2: implementacja mechanizmu zapewniającego niezmiennik poprawności (np. sekwencyjne przetwarzanie zdarzeń zakupu lub optymistyczna kontrola z odrzuceniem), testy funkcjonalne
- **Kamień milowy:** system event-driven działający, przechodzący te same testy funkcjonalne co system synchroniczny

### Faza 5 — Harness testowy (luty)
- Tydz. 1–2: wybór i konfiguracja narzędzia do generowania obciążenia (k6/Locust/Gatling)
- Tydz. 3: implementacja scenariuszy read-heavy i write-heavy dla wszystkich poziomów skali
- Tydz. 4: automatyzacja zbierania metryk (przepustowość, latencja, log transakcji do weryfikacji poprawności)
- **Kamień milowy:** harness gotowy do uruchomienia wszystkich 20 warunków testowych

### Faza 6 — Eksperymenty (marzec)
- Tydz. 1–2: uruchomienie wszystkich 20 warunków testowych, po min. 5 powtórzeń każdy
- Tydz. 3: weryfikacja integralności danych, powtórzenie testów budzących wątpliwości
- Tydz. 4: archiwizacja surowych danych pomiarowych
- **Kamień milowy:** kompletny zbiór danych eksperymentalnych

### Faza 7 — Analiza wyników (kwiecień)
- Tydz. 1–2: analiza statystyczna (rozkłady, percentyle, testy istotności)
- Tydz. 3: wizualizacja wyników (wykresy krzywych degradacji przepustowości/latencji)
- Tydz. 4: interpretacja wyników względem tezy badawczej
- **Kamień milowy:** gotowa analiza i wnioski

### Faza 8 — Pisanie pracy (maj–czerwiec)
- Miesiąc 1: rozdziały teoretyczne, opis metodologii, opis implementacji
- Miesiąc 2: rozdział wyników i wniosków, wstęp, streszczenie, redakcja całości
- **Kamień milowy:** kompletny szkic pracy do przekazania promotorce

### Faza 9 — Bufor i obrona (lipiec)
- Poprawki po recenzji promotorki
- Przygotowanie prezentacji obronnej
- **Kamień milowy:** praca złożona, gotowość do obrony

---

## 10. Otwarte kroki / kolejne decyzje do podjęcia

- [ ] Odpowiedź promotorki na propozycję tematu i tezy
- [ ] Kompletacja wstępnej bibliografii (3–5+ pozycji)
- [ ] Wybór języka/frameworka backendowego
- [ ] Wybór bazy danych
- [ ] Wybór narzędzia do generowania obciążenia
- [ ] Doprecyzowanie progów SLA / kryteriów "degradacji" systemu do analizy statystycznej
