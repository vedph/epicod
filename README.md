# Epigraphic Codices

This is work in progress. The solution currently contains some internal-use tools for a VeDPH research project. The scrapers found here are intended for personal research only. Please see the copyright notices of the respective owners.

Quick Docker image build:

```bash
docker build . -t vedph2020/epicod-api:1.0.0 -t vedph2020/epicod-api:latest
```

(replace with the current version).


## Prerequisites

A PostgreSQL service. You can use a Dockerized service like (this sample refers to a Windows machine):

```ps1
docker run --volume postgresData://c/data/pgsql -p 5432:5432 --name postgres -e POSTGRES_PASSWORD=postgres -d postgres
```

(or use an image like `postgis/postgis` if you need GIS functionality).

## Packard Humanities Greek Inscriptions

Plain Unicode text with Leiden conventions and minimalist metadata.

### Scraper

Root entry: <https://inscriptions.packhum.org/allregions>.

1. level 1 (regions, static): list of regions with their target: `//table/tbody/tr/td/a`. Each target points to a relative URI `/regions/ID` where ID is a number, e.g.:

```html
<a href="/regions/1701" title="Attica Eleusis Rhamnous" class="link">Attica (IG I-III)</a>
```

2. level 2 (books, static): list of books with their target: `//div[@class=\"bookclass\"]//a`. Each target points to `book/ID?location=N` where ID is a number and N is the region ID, e.g. (from <https://epigraphy.packhum.org/regions/1701>):

```html
<li class="bookrow"><a href="/book/3?location=1701" class="item">IG I²</a>
</li>
```

3. level 3 (text, dynamic): once JS completed, texts in the book are listed in an `ul`, each in its `li`. An additional issue here is that most of the list items are just single texts, and thus have an anchor targeting it, like `//li[@class=\"item\"]/a`. Yet, some list items, or even all in some cases, can be of class `range item`, and rather represent a range of texts, like `<li class="range item" id="o11c11">790 - 800</li>`. A relevant issue here is that these items do not have any anchor with a link to be followed.

When the range items are clicked, a POST to `https://epigraphy.packhum.org/phikey` happens. For instance, for the list item sampled above the POST has a form data with `location`=1701 (Attica), `bookid`=3 (IG I2), `offset`=11, `count`=11 (`location=1701&patt=&bookid=3&offset=11&count=11`). The response is an HTML fragment, having an `ul` with `li` items for each text, as described above. This fragment replaces the page's list entirely, so that at the end you only have the items in the range, while all the others are gone. This also means that the back button in the browser cannot retrieve the original list, as the page location did not change; only part of its content was replaced.

Thus, the practical approach here is:

1. collect all the range items at once, once the page has first loaded.
2. all the non-range items are followed.
3. for each collected `li` in the original page, click on it and collect the resulting non-range `li`'s (we can tell that the page has finished loading after the POST when no more range items are present in the page).

Note that in some cases this might be recursive, i.e. a page with a range leads to other ranges. This is an issue because we rely on ranges detection to find out when the page has been loaded (i.e. page has loaded once no more ranges exist in it). So in this case the easiest solution is just letting the scraper timeout, after which it will resume to the next set. We can then refer to the log to find out the culprits, manually open the targets in our browser, and feed the scraper with a list of text URIs.

In the end, each text will be found in its own page, having a line for each table row: `table[@class="grk"]/tbody/tr`. In the table, each `tbody/tr` has 2 `td`, the first either empty or with numbering, the second with text. Also, metadata are:

- `span[@class="ti"]` with text information.
- a final `div[@class="docref"]` contains `a` with href whose value is the PHI ID like `PHI1754`.

The note at the top of each text page is a line with U+2014 as separator, including these data (the asterisk marks those data which seem to be always present):

1. region\*
2. location\*
3. type
4. date\*
5. reference(s)

For instance:

```txt
Att. — Athens: Akropolis — stoich. 28 — 440-410 a. — IG I² 87,f + 141,a, + 174 — IG I³, Add.p.950
```

Unfortunately, the only constant field seems to be the region; so it's difficult to detect which field is what. For instance:

```txt
Att. — 440-430 a.
Att. — Lamptrai: Thiti — s. V a. — Elliot(1962) 56-58 (+) — SEG 32.19
```

Going deeper, we can observe that:

- type usually is a word in `[]` (e.g. `[pottery]`), or is related to the writing direction or layout (e.g. `stoich.` with an optional letters count, `non-stoich.`, `boustr.`, `retrogr.`).
- date has a number of forms: I quote an example for each observed pattern:
  - 2nd ac
  - c. 2nd ac
  - s. V a.
  - s. VI/V a.
  - med. s. V a.
  - fin. s. V a.
  - fin. s. VI/init. s. V a.
  - 4th c. BC
  - 427 a.
  - 525-500? BC
  - c. 480? a.
  - c. 380-370 BC
  - c. 425-400? a.
  - ante 450 a.
  - post 427 a.
  - early imp.
  - aet. Hadriani

 Of course this parsing is not fully refined, but is designed to be successful in most cases, because this is enough for this project, based on large numbers.

### Provisional Target

A RDBMS is used as the provisional target just to let me examine data with more ease.

To make the schema more flexible, I represent texts in a tree, where each node is either a branch or a leaf (text). This allows to easily and neutrally represent any hierarchy in the corpus being scraped.

Also, every node can have any number of properties, which are name=value pairs. This also ensures a neutral model, which can fit any metadata set.
Nodes with text (leaves) always have a `text` property.

For Packhum I currently define these metadata:

- `text`
- `note`
- `region`
- `location`
- `type`
- `layout`
- `date-phi`: the date as found in the note.
- `date-txt`: the date's text in a conventional normal form (Cadmus).
- `date-val`: the date's (approximate) numeric value.
- `date-nan`: a non-numeric date, which cannot be expressed in the conventional normal form.
- `reference`

All these metadata occur at most once per node, except for `reference`.
