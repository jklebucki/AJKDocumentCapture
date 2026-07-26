# Invoice Capture guidance

- Read `CODEX_INVOICE_CAPTURE_IMPLEMENTATION_PLAN.md` and its current checkboxes before changing code.
- Inspect existing abstractions and tests first; do not duplicate behavior.
- Apply Clean Code, DRY, SOLID, KISS and YAGNI. Keep classes and methods small; prefer composition.
- Dependencies flow only inward: `Web`/`Worker`/`Infrastructure` -> `Application` -> `Domain`.
- Keep business logic out of Razor components, endpoints, EF mappings and HTTP clients.
- Prefer BCL/ASP.NET Core before adding packages; public APIs are minimal and types default to `internal`.
- All I/O is asynchronous and receives a final `CancellationToken`; do not use `.Result`, `.Wait()`, `Task.Run` for I/O or `async void`.
- Never log document contents, OCR/model output, tax IDs, bank accounts or secrets.
- Add a regression test for each bug fix. Each completed phase runs format, build and tests.
- Keep commits small and cohesive. Update the implementation plan only after the stated verification succeeds.
