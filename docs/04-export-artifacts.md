# Artefakty i eksport

```mermaid
flowchart LR
  I["InvoiceDocument: Ready"] --> X["IInvoiceXmlExporter"]
  I --> P["IPdfOptimizer"]
  X --> XO["output/document.xml"]
  P --> PO["output/document.optimized.pdf"]
```

Docelowa para eksportowa to deterministyczny XML i lekki PDF. W podglądzie review zaimplementowano profil bazowy `comarch-ecod-ksef-7.77`, odwzorowany z „Faktura XML — ELEKTRONICZNE CENTRUM OBSŁUGI DOKUMENTÓW”, wersja 7.77 z 2025-09-29.

```mermaid
flowchart LR
  O["PaddleOCR-VL OCR"] --> L["Ollama: comarchEcodKsef JSON"]
  L --> A["artifacts/extraction.json"]
  A --> P["ComarchEcodKsefXmlPreviewRenderer"]
  P --> R["Review: XML + source PDF"]
  R --> E["Future ERP export"]
```

Kontrakt JSON obejmuje `Invoice-Header`, `Invoice-Parties`, `Invoice-Lines` i `Invoice-Summary` wraz z `Tax-Summary`. Renderer nie uzupełnia braków ani nie przelicza kwot: gdy nie ma danych koniecznych do profilu bazowego, pokazuje preflight i pozostawia dokument w review.

Zakres obecny nie obejmuje korekt, załączników, DRS, płatności częściowych ani wszystkich warunków branżowych z 43-stronicowej specyfikacji. Przed produkcyjnym eksportem wymagane są: zatwierdzone XSD Comarch, przykłady komunikatów przyjmowanych przez konkretny ERP oraz test walidacyjny na tych artefaktach. Do tego momentu XML pozostaje podglądem review, a nie artefaktem ERP.
