# Packard Humanities Greek Inscriptions

Plain Unicode text with Leiden conventions and minimalist metadata.

## Scraper

Root entry: <https://inscriptions.packhum.org/allregions>.

(A) level 1 (regions, static): list of regions with their target: `//table/tbody/tr/td/a`. Each target points to a relative URI `/regions/ID` where ID is a number, e.g.:

```html
<a href="/regions/1701" title="Attica Eleusis Rhamnous" class="link"
  >Attica (IG I-III)</a
>
```

(B) level 2 (region's books, static): list of books with their target: `//div[@class=\"bookclass\"]//a`. Each target points to `book/ID?location=N` where ID is a number and N is the region ID, e.g. (from <https://epigraphy.packhum.org/regions/1701>):

```html
<li class="bookrow"><a href="/book/3?location=1701" class="item">IG I²</a></li>
```

(C) level 3 (book's texts, dynamic): once JS completed, texts in the book are listed in an `ul`, each in its `li`. Most of the list items are just single texts, and thus have an anchor targeting it, like `//li[@class=\"item\"]/a`. Yet, some list items, or even all in some cases, can be of class `range item`, and rather represent a range of texts, like `<li class="range item" id="o11c11">790 - 800</li>`. A relevant issue here is that these items _do not have any anchor with a link_ to be followed. Rather, when the range items are clicked, a POST to `https://epigraphy.packhum.org/phikey` happens. For instance, for the list item sampled above the POST has a form data with `location`=1701 (Attica), `bookid`=3 (IG I2), `offset`=11, `count`=11 (`location=1701&patt=&bookid=3&offset=11&count=11`). The response is an HTML fragment, having an `ul` with `li` items for each text, as described above. This fragment replaces the page's list entirely, so that at the end you only have the items in the range, while all the others are gone. This also means that the back button in the browser cannot retrieve the original list, as the page location did not change; only part of its content was replaced. Further, the HTML fragment loaded via AJAX when clicking on a range can include not only texts, but even other ranges, with the same issues shown above.

## Strategy

The scraping strategy is thus complex for level 3.

For level 1, the list of regions is just loaded and each link to single region pages is followed.

For level 2, the list of books is loaded and each link is followed, as above.

Level 3 instead poses a number of issues, because it opens an interactive page where even the first list is dynamically loaded. Each list loaded can have a mix of text and range items. Range items have no link; we rather have to click on them thus triggering JS code which loads some HTML via AJAX.

The strategy here is:

1. the page at the specified URI is loaded.
2. all the range items are collected. Each is identified by the index of the range item in the array of range items; thus, 0=first range item, 1=second range item, etc. Please note that this index is not the index of the `li` item, as the list includes a mix of range and text items.
3. all the text items are followed and scraped.
4. for each collected range item, we click on its `li` item and let JS load a new list. Then, the procedure at 2-4 is recursively followed for the newly loaded list. Notice that whenever a new range item needs to be processed, we first have to re-load the starting list. We cannot just navigate back, because this would move away from the region page altogether. This is because lists are dynamically loaded via AJAX and thus do not affect browser's history. From the point of view of the browser, we always are in the same page; it's only that its content gets updated by JS when we click on a range item. So, here we need to navigate to the original list by repeating the whole walking process, from the original page as loaded the first time. Once this is loaded again, we continue clicking on range items and get the corresponding list loaded, until we are back to the list corresponding to our current loop. Then we can proceed with clicking the next range item. So, not only this is a recursive process (because of the nesting of range items within other range items); but we also need to re-play the full sequence of clicks which lead to the list we're processing, each triggering the load of a new list.

While scraping, the tree hierarchy of each page is reflected in the target data model, where each page or text corresponds to a node. Each node has Y and X coordinates representing its depth level and sibling number, plus a label got from the page or text.

This strategy can be better explained by looking at the scraper's log (I removed date and time of each entry to improve its readability).

At first, the root page is loaded, with the list of all the regions (level A). The first region to be followed is Attica. Its node is logged as `[packhum#1]`, i.e. the first node of `packhum` corpus. The `P` section in the entry introduces additional node's properties (metadata), which for non-text nodes are not present (whence `P: -`).

Then (level B), the page for Attica is loaded. There, the first entry found is book `IG I²`, which in turn will be another node, child of the Attica region node.

```txt
[INF] [A] Regions at https://inscriptions.packhum.org/allregions
[INF] [packhum#1] @1.1 Attica (IG I-III) | P: -
[INF] [B] Books at https://inscriptions.packhum.org/regions/1701
[INF] [packhum#2] @2.1 IG I² | P: -
```

## Text

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
