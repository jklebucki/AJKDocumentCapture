# Widok operacyjny i diagnostyka

```mermaid
flowchart LR
  Q["/documents"] --> R["IInvoiceRepository\nsearch + virtual rows"]
  R --> DB["PostgreSQL"]
  D["/diagnostics"] --> DB
  D --> S["File storage"]
  D --> W["Worker heartbeat"]
  D --> P["PaddleOCR-VL"]
  D --> O["Ollama"]
```

Kolejka jest zwartą tabelą z filtrem po nazwie pliku, statusie oraz nazwach i NIP-ach stron. Pierwsza kolumna pokazuje czas uploadu i pierwszego podjęcia przez Worker; dane stron pojawiają się po zapisaniu ekstrakcji.

`/diagnostics` wykonuje na żądanie kontrolę połączenia z PostgreSQL, zapisywalności storage, świeżości heartbeat Workera, zdrowia Paddle i obecności skonfigurowanego modelu Ollama. Nie pokazuje treści dokumentów ani sekretów.
