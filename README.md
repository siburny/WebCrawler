# WebCrawler

A Windows link checker. Give it a web address and it follows every link on the site, then shows each address with its
status, HTTP code, content type, size and response time, so broken links, redirects and slow pages stand out.

## Features

- Follows links on pages of the starting site up to a configurable depth; links to other sites are listed but not followed.
- Reports errors (4xx/5xx, unreachable hosts, invalid addresses), redirects (with their target) and response times.
- Several requests at once, with a short pause between the requests of each worker.
- **Quick scan:** only sends `HEAD` requests (falling back to `GET` for servers that reject them). Fast, but pages are
  not read, so no links are followed.
- **Ignore list:** addresses containing one of these texts are listed as skipped and never requested.
- **Highlight list:** regular expressions matched against the page text; matching rows get your colour.
- Several measurements per address, to average out response times.
- Saves a report as CSV (`.csv`), tab-separated text (any other extension) or prints it to the console.

## Requirements

- Windows 10 or later
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (to build: the .NET 10 SDK)

## Build and run

```
dotnet build WebCrawler.sln
dotnet run --project WebCrawler
```

Or open `WebCrawler.sln` in a version of Visual Studio that supports .NET 10 and press F5.

Run the tests (they start a small local web server, no internet needed):

```
dotnet test
```

## Command line

```
WebCrawler [options] [url]
```

| Option | Meaning |
| --- | --- |
| `url` | Address to start from; the scan begins at once. |
| `-i <file>` | Read addresses from a text file, one per line. The first valid one defines which addresses count as part of the site. |
| `-d <n>` | Maximum depth of link following (overrides the setting for this run). |
| `-n <n>` | Number of measurements per address (overrides the setting for this run). |
| `-q` | Quick scan (`HEAD` requests only). |
| `-o <file>` | Save the report when the scan is finished and close the program. `%d` in the name becomes today's date (`yyyyMMdd`). `-o console` prints the report to the console instead. |

Example: `WebCrawler -d 3 -o report-%d.csv https://example.com/`

## Settings

Settings are stored as JSON, in `config.json` next to the program if such a file exists (a portable copy keeps its own
settings), otherwise in `%AppData%\WebCrawler\config.json`.

| Setting | Default | Meaning |
| --- | --- | --- |
| `MaxProcesses` | 5 | Number of simultaneous requests. |
| `MaxDepth` | 5 | How many links deep from the starting address pages are searched for further links. |
| `MaxMeasurements` | 1 | How many times each address is requested (the shown time is the average). |
| `QuickScan` | false | Use `HEAD` requests only. |
| `ExcludeRules` | none | Texts; addresses containing one are skipped (case-sensitive). |
| `HighlightRules` | none | `Expression` (regular expression, case-insensitive) and `HighlightColor` (for example `0xff8000`). |

Settings saved by earlier versions (`config.xml` next to the program) are imported the first time the program starts.
A `config.json` that cannot be read is renamed to `config.json.bad` and the defaults are used.

## Project layout

| Project | Contents |
| --- | --- |
| `WebCrawler` | The Windows Forms application: windows, grid, report export. |
| `WebCrawler.Core` | The crawler, with no user interface: `Crawler` (asynchronous workers), `URLCollection` (the list of addresses and the work queue), `Settings`. Uses [AngleSharp](https://anglesharp.github.io/) to read pages. |
| `WebCrawler.Tests` | xUnit tests, including crawls of a scripted local web server (redirects, errors, encodings, stopping). |

How a scan works: the workers (`Crawler`) take the next waiting address from the `URLCollection`, request it with
`HttpClient`, read the page if it is text, and add every link found. A timer in the window copies the results to the grid a
few at a time, so a fast crawl never freezes the window. Stopping cancels the requests in flight.

## Continuous integration

`.github/workflows/build.yml` builds and tests on every push and pull request, and publishes the application as a build artifact.
