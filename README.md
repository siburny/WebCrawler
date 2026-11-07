# WebCrawler

A cross-platform (Windows, macOS, Linux) link checker. Give it a web address and it follows every link on the site, then shows each address with its
status, HTTP code, content type, size and response time, so broken links, redirects and slow pages stand out.

## Features

- Follows links on pages of the starting site up to a configurable depth; links to other sites are listed but not followed.
- Reports errors (4xx/5xx, unreachable hosts, invalid addresses), redirects (with their target) and response times.
- Several requests at once, but never more than a set number per second to one site (see **Politeness** below).
- **Quick scan:** only sends `HEAD` requests (falling back to `GET` for servers that reject them). Fast, but pages are
  not read, so no links are followed.
- **Ignore list:** addresses containing one of these texts are listed as skipped and never requested.
- **Highlight list:** regular expressions matched against the page text; matching rows get your colour.
- **URL history:** the URL box is a dropdown of the last 10 starting addresses you scanned (kept in the settings file).
- Several measurements per address, to average out response times.
- Saves a report as CSV (`.csv`), tab-separated text (any other extension) or prints it to the console.
- **Find:** `Ctrl+F` (or the **Find** button) opens a small window that stays open while you search the list. **Find Next** selects the next row that contains all the words you typed (in the address, notes, content type, status or HTTP code), ignoring case, and starts again from the top after the last row.

## Requirements

- Windows 10 or later, a macOS version supported by .NET 10, or a desktop Linux (X11 or Wayland through XWayland) on x64 or arm64
- To run a build: the [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (to build: the .NET 10 SDK)

The window is built with [Avalonia UI](https://avaloniaui.net/), which draws it itself, so nothing else has to be installed
on Windows and macOS.

## Build and run

The commands are the same on every system; run them in the folder that holds `WebCrawler.sln`.

**macOS** (Terminal)

```
brew install --cask dotnet-sdk   # or install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0
dotnet build WebCrawler.sln
dotnet run --project WebCrawler
```

**Windows** (PowerShell or Command Prompt; install the .NET 10 SDK first, for example with `winget install Microsoft.DotNet.SDK.10`)

```
dotnet build WebCrawler.sln
dotnet run --project WebCrawler
```

**Linux** (install the .NET 10 SDK with your package manager or from the Microsoft download page; a desktop session with
`libfontconfig` and, for X11, `libX11`, `libICE` and `libSM` is needed, which any normal desktop has)

```
dotnet build WebCrawler.sln
dotnet run --project WebCrawler
```

Arguments for the program go after `--`: `dotnet run --project WebCrawler -- -d 3 https://example.com/`.
You can also open `WebCrawler.sln` in Visual Studio, JetBrains Rider or VS Code (C# Dev Kit) and run the `WebCrawler` project.

To make a build that runs without the .NET runtime installed, publish it for the target system, for example:

```
dotnet publish WebCrawler -c Release -r osx-arm64 --self-contained -o publish/osx-arm64
dotnet publish WebCrawler -c Release -r win-x64 --self-contained -o publish/win-x64
dotnet publish WebCrawler -c Release -r linux-x64 --self-contained -o publish/linux-x64
```

(runtime identifiers: `osx-arm64`, `osx-x64`, `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`).

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
settings), otherwise in the user's settings folder:

| System | Location |
| --- | --- |
| Windows | `%AppData%\WebCrawler\config.json` |
| macOS | `~/Library/Application Support/WebCrawler/config.json` |
| Linux | `$XDG_CONFIG_HOME/WebCrawler/config.json` (default `~/.config/WebCrawler/config.json`) |

| Setting | Default | Meaning |
| --- | --- | --- |
| `MaxProcesses` | 5 | Number of simultaneous requests. |
| `MaxDepth` | 5 | How many links deep from the starting address pages are searched for further links. |
| `MaxMeasurements` | 1 | How many times each address is requested (the shown time is the average). |
| `QuickScan` | false | Use `HEAD` requests only. |
| `RequestDelayMs` | 250 | Least time in milliseconds between two requests to the same site, however many connections are used (250 = at most 4 requests per second). 0 removes the limit. |
| `UserAgent` | empty | The `User-Agent` header to send. Empty means `Mozilla/5.0 (compatible; WebCrawler/<version>)`. |
| `Cookies` | empty | Cookies sent with every request, one `NAME=VALUE` per line (a pasted `a=1; b=2` works too). Set in the Settings window; stored as plain text in the config file. |
| `RecentUrls` | none | The last 10 starting addresses a scan was started from, newest first. They fill the dropdown of the URL box; the program keeps this list itself. |
| `ExcludeRules` | none | Texts; addresses containing one are skipped (case-sensitive). |
| `HighlightRules` | none | `Expression` (regular expression, case-insensitive) and `HighlightColor` (for example `0xff8000`). |

Settings saved by earlier versions (`config.xml` next to the program) are imported the first time the program starts.
A `config.json` that cannot be read is renamed to `config.json.bad` and the defaults are used.

## Politeness

- Every request carries a `User-Agent` header (many sites refuse requests without one). Set `UserAgent` to use your own.
- Requests to one site are spaced at least `RequestDelayMs` apart, whatever `MaxProcesses` is. More connections then
  mainly help when pages are slow to answer; lower the delay (or set it to 0) to scan a site of your own faster.
- The site is the address you start from, and its subdomains: starting at `https://expeditions.com` also follows
  `https://world.expeditions.com/page`. The parent domain and sibling subdomains are only listed, not followed, and so is
  everything on another scheme or port. A start address with a path (`https://expeditions.com/blog/`) limits the scan to
  that path, on subdomains too.
- `robots.txt` is not read: the program checks every address of the site you give it. Only use it on sites you may check.
- TLS versions are left to the operating system, so TLS 1.3 works.

## Project layout

| Project | Contents |
| --- | --- |
| `WebCrawler` | The [Avalonia](https://avaloniaui.net/) application (`Views/` holds the AXAML windows, `ViewModels/` their MVVM view models, built with CommunityToolkit.Mvvm): main window with the results grid, Settings, Link Information and Find windows, report export. |
| `WebCrawler.Core` | The crawler, with no user interface: `Crawler` (asynchronous workers), `URLCollection` (the list of addresses and the work queue), `Settings`. Uses [AngleSharp](https://anglesharp.github.io/) to read pages. |
| `WebCrawler.Tests` | xUnit tests, including crawls of a scripted local web server (redirects, errors, encodings, stopping). |

How a scan works: the workers (`Crawler`) take the next waiting address from the `URLCollection`, request it with
`HttpClient`, read the page if it is text, and add every link found. A timer on the UI thread copies the results to the grid a
few at a time (worker threads never touch the window), so a fast crawl never freezes it. Stopping cancels the requests in flight.

## Continuous integration

`.github/workflows/build.yml` runs for every push to the main branch and every pull request. It builds and tests on Windows
and macOS and keeps each build as a downloadable artifact: `WebCrawler-win.zip` (needs the .NET 10 Runtime) and
`WebCrawler-macos-arm64.zip` / `WebCrawler-macos-x64.zip` (a self-contained `WebCrawler.app` for Apple silicon and Intel Macs).

**To publish a release,** put `[release]` in the message of the last commit of the push:

```
git commit -m "Add CSV export [release]"
git push
```

The builds then also create a GitHub release. Its version is the latest release tag with the minor number raised by one
(`v1.4.0` becomes `1.5.0`; the first release is `1.1.0`). The release, named `WebCrawler 1.5.0`, has
`WebCrawler-1.5.0-win.zip`, `WebCrawler-1.5.0-macos-arm64.zip` and `WebCrawler-1.5.0-macos-x64.zip` attached and release
notes generated from the commits since the previous release.
Only the last commit's message is checked, so with several commits in one push the marker must be in the last one.

To start a new major version, create a tag by hand (`git tag v2.0.0`, `git push origin v2.0.0`); the next release is
then `2.1.0`.

The Windows zip needs the .NET 10 Runtime on the target machine. The macOS app is signed ad hoc only (there is no Apple
developer account), so macOS blocks it the first time it is opened after a download: right-click `WebCrawler.app`, choose
**Open** and confirm, or run `xattr -dr com.apple.quarantine WebCrawler.app` once.
