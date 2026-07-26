# AJK Document Capture

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C%23 14](https://img.shields.io/badge/C%23-14-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Blazor](https://img.shields.io/badge/Blazor-Interactive%20Server-512BD4?logo=blazor&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![PostgreSQL 17](https://img.shields.io/badge/PostgreSQL-17-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![PaddleOCR--VL](https://img.shields.io/badge/PaddleOCR--VL-1.6-0062B5)](https://www.paddleocr.ai/)
[![Ollama](https://img.shields.io/badge/Ollama-gpt--oss%3A20b-black?logo=ollama&logoColor=white)](https://ollama.com/)
[![Docker Compose](https://img.shields.io/badge/Docker%20Compose-local-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)

Aplikacja do bezpiecznego przechwytywania faktur i paragonów z NIP. Przetwarza dokument przez lokalny pipeline OCR, mapuje wynik do kanonicznego JSON i kieruje go do walidacji lub ręcznego review. Eksport XML/PDF do ERP jest planowany jako kolejny etap.

```mermaid
flowchart LR
  U["Upload"] --> W["Blazor Web"]
  W --> Q["PostgreSQL + storage"]
  Q --> K["Worker"]
  K --> P["PaddleOCR-VL-1.6"]
  P --> O["Ollama gpt-oss:20b"]
  O --> V["C# validation"]
  V --> R["Ready / ReviewRequired"]
```

## Szybki start

Wymagania: .NET SDK 10, Docker Desktop oraz dostępny Ollama z modelem `gpt-oss:20b`.

```bash
cp deploy/.env.example deploy/.env
# Ustaw OLLAMA_BASE_URL i bezpieczne dane PostgreSQL w deploy/.env.
./scripts/dev-up.sh
```

Pierwszy start pobiera model PaddleOCR-VL do named volume i może potrwać kilka minut. Aplikacja nasłuchuje domyślnie tylko lokalnie na `http://127.0.0.1:8088`; użyj `WEB_PORT`, aby zmienić port.

## Weryfikacja

```bash
dotnet format InvoiceCapture.slnx --no-restore --verify-no-changes
dotnet build InvoiceCapture.slnx -c Release --no-restore
dotnet test InvoiceCapture.slnx -c Release --no-build
```

Stan prac i kryteria ukończenia znajdują się w [planie wykonawczym](CODEX_INVOICE_CAPTURE_IMPLEMENTATION_PLAN.md). Zwięzłe diagramy procesów są w [docs](docs/README.md).

> Projekt jest w trakcie realizacji. Nie używaj go jeszcze do produkcyjnego eksportu księgowego ani jako źródła XML dla ERP.
