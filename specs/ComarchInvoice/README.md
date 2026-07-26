# Comarch EDI XML INVOICE 7.77 - pakiet walidacyjny

Źródło: specyfikacja Comarch `20250929_INVOIC_PL_ECOD_KSEF_FULL.pdf`, wersja 7.77, data 2025-09-29.

## Profile XSD 1.0

- `comarch-edi-invoice-7.77-invoice.xsd` - Faktura.
- `comarch-edi-invoice-7.77-correction.xsd` - Faktura korekta.
- `comarch-edi-invoice-7.77-ksef.xsd` - Faktura KSeF.
- `comarch-edi-invoice-7.77-ksef_correction.xsd` - Faktura korekta KSeF.

Schematy są samodzielne, bez namespace, i odwzorowują kolejność elementów, M/O/C, limity wystąpień, daty i czasy bez strefy, liczby, długości, wzorce, waluty, kraje oraz enumeracje.

Pięć pól metadanych `KSEFDocument*`, oznaczonych w tabeli `-` z przypisem 40, jest dopuszczonych opcjonalnie w profilach KSeF, ponieważ Comarch uzupełnia je przy odbiorze dokumentu z KSeF.

## Reguły warunkowe

XSD 1.0 nie potrafi wyrazić wszystkich zależności między odległymi polami. Dla każdego profilu dołączono odpowiadające mu pliki:

- `comarch-edi-invoice-7.77-<profil>-rules.sch` - źródłowy ISO Schematron;
- `comarch-edi-invoice-7.77-<profil>-rules.xslt` - skompilowany walidator XSLT 1.0 zwracający SVRL, gotowy do użycia w .NET;
- `RULE_COVERAGE.md` - pokrycie wszystkich przypisów 1-76 z rozdzieleniem reguł automatycznych i zależnych od danych zewnętrznych.

Walidacja produkcyjna powinna przebiegać w kolejności: **XSD odpowiedniego profilu, następnie Schematron/XSLT**.

## Raporty i testy

- `validation-report.txt` - kompilacja XSD, walidacja 8 plików testowych i walidacja Schematronem.
- `completeness-report.txt` - porównanie ścieżek elementów z tabeli PDF z każdym wygenerowanym XSD.
- `regression-tests.txt` - dodatnie i ujemne testy kluczowych ograniczeń.
- `sample-*-minimal.xml` - minimalne dokumenty przechodzące XSD i reguły między-polowe.
- `sample-*-full.xml` - dokumenty zawierające wszystkie opcjonalne pola profilu; służą do testu kompletności XSD, nie do Schematronu, ponieważ celowo zawierają pola alternatywne jednocześnie.
- `extracted-model.json` - 876 wierszy modelu odczytanych z tabel specyfikacji.
- `generate_comarch_xsd.py` - generator umożliwiający odtworzenie pakietu.

## .NET: walidacja XSD

```csharp
using System.Xml;

var settings = new XmlReaderSettings
{
    ValidationType = ValidationType.Schema,
    DtdProcessing = DtdProcessing.Prohibit
};
settings.Schemas.Add(null, "comarch-edi-invoice-7.77-ksef.xsd");
settings.ValidationEventHandler += (_, e) => throw e.Exception;

using var reader = XmlReader.Create("invoice.xml", settings);
while (reader.Read()) { }
```

## .NET: walidacja reguł między-polowych

```csharp
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;

var transform = new XslCompiledTransform();
transform.Load("comarch-edi-invoice-7.77-ksef-rules.xslt");

using var output = new StringWriter();
using (var writer = XmlWriter.Create(output, transform.OutputSettings))
{
    transform.Transform("invoice.xml", writer);
}

var svrl = XDocument.Parse(output.ToString());
XNamespace ns = "http://purl.oclc.org/dsdl/svrl";
var failures = svrl.Descendants(ns + "failed-assert").ToList();
if (failures.Count > 0)
{
    throw new InvalidDataException(string.Join(Environment.NewLine,
        failures.Select(x => x.Element(ns + "text")?.Value)));
}
```

## Status dokumentu

To nie jest oficjalny plik XSD wydany przez Comarch. Jest to kompletna strukturalnie rekonstrukcja techniczna z opublikowanej tabeli 7.77, zwalidowana automatycznie. Reguły zależne od prawa, konfiguracji kontrahenta, procesu ECOD lub porównania z fakturą pierwotną są jawnie wskazane w `RULE_COVERAGE.md` i nie są zgadywane.
