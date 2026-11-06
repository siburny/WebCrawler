# WebCrawler

A Windows link checker. Give it a web address and it follows every link on the site, then shows each address with its
status, HTTP code, content type, size and response time, so broken links, redirects and slow pages stand out.

## Features

- Follows links on pages of the starting site up to a configurable depth; links to other sites are listed but not followed.
- Reports errors (4xx/5xx, unreachable hosts, invalid addresses), redirects (with their target) and response times.
- Several requests at once, but never more than a set number per second to one site (see **Politeness** below).
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
| `RequestDelayMs` | 250 | Least time in milliseconds between two requests to the same site, however many connections are used (250 = at most 4 requests per second). 0 removes the limit. |
| `UserAgent` | empty | The `User-Agent` header to send. Empty means `Mozilla/5.0 (compatible; WebCrawler/<version>)`. |
| `ExcludeRules` | none | Texts; addresses containing one are skipped (case-sensitive). |
| `HighlightRules` | none | `Expression` (regular expression, case-insensitive) and `HighlightColor` (for example `0xff8000`). |

Settings saved by earlier versions (`config.xml` next to the program) are imported the first time the program starts.
A `config.json` that cannot be read is renamed to `config.json.bad` and the defaults are used.

## Politeness

- Every request carries a `User-Agent` header (many sites refuse requests without one). Set `UserAgent` to use your own.
- Requests to one site are spaced at least `RequestDelayMs` apart, whatever `MaxProcesses` is. More connections then
  mainly help when pages are slow to answer; lower the delay (or set it to 0) to scan a site of your own faster.
- `robots.txt` is not read: the program checks every address of the site you give it. Only use it on sites you may check.
- TLS versions are left to the operating system, so TLS 1.3 works.

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

`.github/workflows/build.yml` runs on Windows for every push to the main branch and every pull request: it builds,
runs the tests and keeps the application as a downloadable build artifact.

**To publish a release,** put `[release]` in the message of the last commit of the push:

```
git commit -m "Add CSV export [release]"
git push
```

The build then also creates a GitHub release. Its version is the latest release tag with the minor number raised by one
(`v1.4.0` becomes `1.5.0`; the first release is `1.1.0`). The release, named `WebCrawler 1.5.0`, has
`WebCrawler-1.5.0-win.zip` attached and release notes generated from the commits since the previous release.
Only the last commit's message is checked, so with several commits in one push the marker must be in the last one.

To start a new major version, create a tag by hand (`git tag v2.0.0`, `git push origin v2.0.0`); the next release is
then `2.1.0`.

The zip needs the .NET 10 Desktop Runtime on the target machine.
