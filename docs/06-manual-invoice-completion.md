# Ręczne uzupełnianie faktury

```mermaid
flowchart LR
  U["Użytkownik: karta dokumentu"] --> F["EditForm: dane faktury"]
  F --> H["UpdateManualInvoiceHandler"]
  H --> V["InvoiceValidator"]
  V --> D["PostgreSQL: invoice_documents + issues"]
  D --> S["Ready albo ReviewRequired"]
```

Karta dokumentu pozwala uzupełnić numer i daty, walutę, strony, płatność, rachunek oraz sumy. Zapis normalizuje puste wartości, uruchamia walidację C# i zapisuje wynik. Dokument `ReviewRequired` przechodzi do `Ready` tylko bez błędów walidacji; niespójne sumy nadal wymagają korekty.

Ręczna korekta zmienia rekord roboczy i issues. Artefakt `extraction.json` pozostaje niemodyfikowalnym zapisem odpowiedzi Ollama; jego XML jest nadal wyświetlany osobno w review.
