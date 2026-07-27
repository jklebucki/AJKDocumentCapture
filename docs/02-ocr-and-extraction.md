# OCR i ekstrakcja

```mermaid
flowchart LR
  S["source/original"] --> P["PaddleOcrClient"]
  P --> O["artifacts/ocr.json"]
  O --> L["OllamaExtractionClient"]
  L --> Q["artifacts/ollama-request.json"]
  L --> E["artifacts/extraction.json\ncomarch XML tree v3"]
  E --> V["InvoiceValidator"]
  V --> R["Ready / ReviewRequired"]
```

Prompt Ollama jest celowo krótki: zawiera zasady bezpieczeństwa, minimalny szkielet faktury i kontrakt drzewa XML. Nie zawiera XSD ani listy ścieżek; po odpowiedzi deterministyczny renderer i walidator XSD sprawdzają strukturę. Z poziomu logo aplikacji dostępny jest podgląd samych instrukcji, bez OCR lub danych dokumentu.

Worker przechodzi trwałymi etapami `Normalizing → OcrRunning → Extracting → Validating`. `PaddleOCR-VL-1.6` zwraca pełny JSON, który jest zachowany w artefakcie; do gpt-oss trafia wyłącznie pole Markdown i identyfikatory bloków. Klient Ollama wysyła `/api/chat`, `stream=false`, `temperature=0` i JSON Schema; prompt traktuje OCR jako nieufny materiał oraz zabrania zgadywania.

Raw OCR, pełne body żądania `/api/chat` i odpowiedź ekstraktora trafiają do artefaktów, nie do zwykłych logów. Bezpośrednio przed wysłaniem Worker zapisuje niezmieniony `artifacts/ollama-request.json`; historia dokumentu dostaje wpis **Ollama request sent** z przyciskiem podglądu tego artefaktu. Dzięki temu operator widzi dla konkretnej faktury dokładny system prompt, user prompt i OCR przekazane do Ollama, także gdy wywołanie kończy się błędem. Dane adresu Ollama pochodzą wyłącznie z `OLLAMA_BASE_URL`/konfiguracji środowiska.

Historia przetwarzania pokazuje operatorowi nazwę wykonawcy etapu bez treści dokumentu: `PaddleOCR-VL` dla OCR/layout, `Ollama gpt-oss:20b` dla ekstrakcji oraz `C# InvoiceValidator` dla walidacji.

Timeout OCR i ekstrakcji wynosi 10 min; błąd sieciowy, 5xx lub timeout jest retriowany na tym samym trwałym etapie. Licznik prób resetuje się po jego pomyślnym ukończeniu.
