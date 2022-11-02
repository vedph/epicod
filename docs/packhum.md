# Packard Humanities Greek Inscriptions

- [Packard Humanities Greek Inscriptions](#packard-humanities-greek-inscriptions)
  - [Quick start](#quick-start)
  - [Scraper](#scraper)
  - [Strategy](#strategy)
  - [Reading Scraper's Log](#reading-scrapers-log)
  - [Text](#text)
    - [Date](#date)
  - [Metadata](#metadata)

This corpus contains plain Unicode text with Leiden conventions and minimalist metadata.

## Quick start

You need your PostgreSQL service up and running.

1. create the database: `./epicod create-db epicod` (`epicod` is the database name);
2. start scraping with note parsing: `./epicod scrape-packhum -n`.

While scraping you will see a running log; a full log will be stored in log files (1 per day) in the program's directory; and you can always query the database being filled with data to see how it's going.

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

## Reading Scraper's Log

The scraper saves a full log of its work under the program's directory. There is a log file for each day, named `epicod` plus the date and extension `txt`.

In order to better explain the scraping strategy, and let team members check the process, let's look at the first entries of a sample log. I removed date and time of each entry and some less relevant details to improve its readability.

(1) At first, the root page is loaded, with the list of all the regions (level A). The first region to be followed is Attica. Its node is logged as `[packhum#1]`, i.e. the first node of `packhum` corpus. The `P` section in the entry introduces additional node's properties (metadata), which for non-text nodes are not present (whence `P: -`).

Then (level B), the page for Attica is loaded. There, the first entry found is book `IG I²`, which in turn will be another node, child of the Attica region node.

```txt
[INF] [A] Regions at https://inscriptions.packhum.org/allregions
[INF] [packhum#1] @1.1 Attica (IG I-III) | P: -
[INF] [B] Books at https://inscriptions.packhum.org/regions/1701
[INF] [packhum#2] @2.1 IG I² | P: -
```

Here is a screenshot of the first entries in the Attica region's page:

![region: Attica](img/region.png)

(2) Opening the link for IG I² starts by loading the texts level (C). Once the page is loaded, the script in it triggers the loading of a first (default) list. The `path` indication after the page's URL refers to the relative path followed to walk the tree of range items. Thus, it starts as `/`, i.e. with the root path corresponding to the base page list for the requested region.

Once the page is loaded, its content is inspected. Here, we found 26 text items.

```txt
[INF] [C] Texts at https://inscriptions.packhum.org/book/3?location=1701: path /
[INF] Loading page from https://inscriptions.packhum.org/book/3?location=1701
[INF] Text items: 26
```

The screenshot here shows the first list, where range items are green, and text items are just "regular" blue links:

![book: IG I](img/book.png)

We first follow each of the text items, scraping it into a node. Its metadata properties names are listed in the log. Nodes are all at level 3, and range from 1 to 26 for entries `165` to `Fasti272b90`.

```txt
[INF] [packhum#3] @3.1 165 | P: -
[INF] [packhum#4] @3.1 IG I² 165 - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#5] @3.2 185a | P: -
[INF] [packhum#6] @3.2 IG I² 185a - PHI Greek Inscriptions | P: text, note, region, location, reference, reference, phi
[INF] [packhum#7] @3.3 400,Ib | P: -
[INF] [packhum#8] @3.3 IG I² 400,Ib - PHI Greek Inscriptions | P: text, note, region, location, date-nan, date-phi, reference, phi
[INF] [packhum#9] @3.4 400,II | P: -
[INF] [packhum#10] @3.4 IG I² 400,II - PHI Greek Inscriptions | P: text, note, region, location, date-nan, date-phi, reference, phi
[INF] [packhum#11] @3.5 503,adn | P: -
[INF] [packhum#12] @3.5 IG I² 503,adn - PHI Greek Inscriptions | P: text, note, region, location, type, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#13] @3.6 522 | P: -
[INF] [packhum#14] @3.6 IG I² 522 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, reference, phi
[INF] [packhum#15] @3.7 561 | P: -
[INF] [packhum#16] @3.7 IG I² 561 - PHI Greek Inscriptions | P: text, note, region, location, layout, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#17] @3.8 575 | P: -
[INF] [packhum#18] @3.8 IG I² 575 - PHI Greek Inscriptions | P: text, note, region, location, date-nan, date-phi, reference, phi
[INF] [packhum#19] @3.9 644 | P: -
[INF] [packhum#20] @3.9 IG I² 644 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#21] @3.10 730 | P: -
[INF] [packhum#22] @3.10 IG I² 730 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#23] @3.11 774 | P: -
[INF] [packhum#24] @3.11 IG I² 774 - PHI Greek Inscriptions | P: text, note, region, location, layout, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#25] @3.12 827 | P: -
[INF] [packhum#26] @3.12 IG I² 827 - PHI Greek Inscriptions | P: text, note, region, location, type, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#27] @3.13 836 | P: -
[INF] [packhum#28] @3.13 IG I² 836 - PHI Greek Inscriptions | P: text, note, region, location, type, phi
[INF] [packhum#29] @3.14 845 | P: -
[INF] [packhum#30] @3.14 IG I² 845 - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#31] @3.15 869 | P: -
[INF] [packhum#32] @3.15 IG I² 869 - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#33] @3.16 913,adn | P: -
[INF] [packhum#34] @3.16 IG I² 913,adn - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#35] @3.17 919 | P: -
[INF] [packhum#36] @3.17 IG I² 919 - PHI Greek Inscriptions | P: text, note, region, location, type, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#37] @3.18 934 | P: -
[INF] [packhum#38] @3.18 IG I² 934 - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#39] @3.19 1031 | P: -
[INF] [packhum#40] @3.19 IG I² 1031 - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#41] @3.20 1046 | P: -
[INF] [packhum#42] @3.20 IG I² 1046 - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#43] @3.21 1050 | P: -
[INF] [packhum#44] @3.21 IG I² 1050 - PHI Greek Inscriptions | P: text, note, region, location, reference, reference, reference, phi
[INF] [packhum#45] @3.22 1054 | P: -
[INF] [packhum#46] @3.22 IG I² 1054 - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#47] @3.23 1070,2 | P: -
[INF] [packhum#48] @3.23 IG I² 1070,2 - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#49] @3.24 1077 | P: -
[INF] [packhum#50] @3.24 IG I² 1077 - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#51] @3.25 1086 | P: -
[INF] [packhum#52] @3.25 IG I² 1086 - PHI Greek Inscriptions | P: text, note, region, location, type, date-txt, date-val, date-phi, phi
[INF] [packhum#53] @3.26 Fasti 272b90 | P: -
[INF] [packhum#54] @3.26 IG I² Fasti 272b90 - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
```

Then we have to follow the green range items. There are 13 of them; we thus start looping range items from 1 to 13. The first cycle in the loop is for range item 1 of 13, whose label is `790 - 800`. You can now see that the relative `path` is `/0` because we have walked down from the "root" list (`/`) to the list loaded from the 1st range, whose index is 0; thus, path is `/0` = 1st child range item in the source list.

Once we "click" on the range item, a new list replaces the existing one in our current page. This list then gets scraped. There are 11 text items here, and no range item.

```txt
[INF] Ranges to follow: 13
[INF] Range 1/13: 0: "790 - 800"
[INF] [C] Texts at https://inscriptions.packhum.org/book/3?location=1701*: path /0
[INF] Text items: 11
[INF] [packhum#55] @3.1 790 | P: -
[INF] [packhum#56] @3.1 IG I² 790 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#57] @3.2 791 | P: -
[INF] [packhum#58] @3.2 IG I² 791 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#59] @3.3 792 | P: -
[INF] [packhum#60] @3.3 IG I² 792 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#61] @3.4 793 | P: -
[INF] [packhum#62] @3.4 IG I² 793 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#63] @3.5 794 | P: -
[INF] [packhum#64] @3.5 IG I² 794 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#65] @3.6 795 | P: -
[INF] [packhum#66] @3.6 IG I² 795 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#67] @3.7 796 | P: -
[INF] [packhum#68] @3.7 IG I² 796 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#69] @3.8 797 | P: -
[INF] [packhum#70] @3.8 IG I² 797 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#71] @3.9 798 | P: -
[INF] [packhum#72] @3.9 IG I² 798 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#73] @3.10 799 | P: -
[INF] [packhum#74] @3.10 IG I² 799 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
[INF] [packhum#75] @3.11 800 | P: -
[INF] [packhum#76] @3.11 IG I² 800 - PHI Greek Inscriptions | P: text, note, region, location, type, reference, phi
```

Here is the corresponding screenshot:

![exploded texts range](img/texts.png)

Note that _this is the same page of the preceding list_. The JS code in the page has replaced the list with another one. So, should you navigate to this page with your browser and then click the back button, you will not get back to the previous list, but rather to the previous page, with no texts list at all.

Here we are lucky enough that there is no range item; so once we have finished scraping the text items, we can go back to the source list. As we have just remembered, we can't simply "go back". We need to restart from the Attica's books list re-loading it, and then click the 1st range item to get back to the list we are processing. These are the corresponding entries in the log:

```txt
[INF] Repositioning to / starting from https://inscriptions.packhum.org/book/3?location=1701
[INF] Loading page from https://inscriptions.packhum.org/book/3?location=1701
[INF] Repositioning completed
```

We can now keep looping through the range items of our list. We thus move to the 2nd range item (`865,A - 865,B`) by clicking on it, which triggers the update of the list. Note that now the path reads `/1` because we are processing the 2nd child range item (whose zero-based index is 1).

This list happens to have only 2 text items, which get followed. The corresponding nodes are saved.

```txt
[INF] Range 2/13: 1: "865,A - 865,B"
[INF] [C] Texts at https://inscriptions.packhum.org/book/3?location=1701*: path /1
[INF] Text items: 2
[INF] [packhum#77] @3.1 865,A | P: -
[INF] [packhum#78] @3.1 IG I² 865,A - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
[INF] [packhum#79] @3.2 865,B | P: -
[INF] [packhum#80] @3.2 IG I² 865,B - PHI Greek Inscriptions | P: text, note, region, location, date-txt, date-val, date-phi, reference, phi
```

Again, no range items here. So we need to re-load the "root" list, and then click until we get back to our source list. Once this is done, we click the 3rd range item, and follow its text items, as above.

```txt
[INF] Repositioning to / starting from https://inscriptions.packhum.org/book/3?location=1701
[INF] Loading page from https://inscriptions.packhum.org/book/3?location=1701
[INF] Repositioning completed
[INF] Range 3/13: 2: "908,1 - 908,2"
[INF] Loaded page hash: 332857183
[INF] [C] Texts at https://inscriptions.packhum.org/book/3?location=1701*: path /2
[INF] Text items: 2
[INF] [packhum#81] @3.1 908,1 | P: -
[INF] [packhum#82] @3.1 IG I² 908,1 - PHI Greek Inscriptions | P: text, note, region, location, type, date-txt, date-val, date-phi, reference, reference, phi
[INF] [packhum#83] @3.2 908,2 | P: -
[INF] [packhum#84] @3.2 IG I² 908,2 - PHI Greek Inscriptions | P: text, note, region, location, type, date-txt, date-val, date-phi, reference, reference, phi
```

The process then continues recursively, until all the range items have been followed. Once this happens, we will move to the next book; once all the books are done, we will move on the next region; and once all the regions are done, we have finished.

If you now look at the target database, you will see nodes in the `text_node` table, e.g.:

|id|parent_id|corpus|y|x|name|uri|
|--|---------|------|-|-|----|---|
|1|0|packhum|1|1|Attica (IG I-III)|<https://inscriptions.packhum.org/regions/1701>|
|2|1|packhum|2|1|IG I²|<https://inscriptions.packhum.org/book/3?location=1701>|
|76|1|packhum|2|2|IG I³|<https://inscriptions.packhum.org/book/4?location=1701>|
|3|2|packhum|3|1|IG I² 165 - PHI Greek Inscriptions|<https://inscriptions.packhum.org/text/1754?&amp;bookid=3&amp;location=1701>|
|4|2|packhum|3|2|IG I² 185a - PHI Greek Inscriptions|<https://inscriptions.packhum.org/text/1755?&amp;bookid=3&amp;location=1701>|
|5|2|packhum|3|3|IG I² 400,Ib - PHI Greek Inscriptions|<https://inscriptions.packhum.org/text/1756?&amp;bookid=3&amp;location=1701>|
|6|2|packhum|3|4|IG I² 400,II - PHI Greek Inscriptions|<https://inscriptions.packhum.org/text/1757?&amp;bookid=3&amp;location=1701>|
|7|2|packhum|3|5|IG I² 503,adn - PHI Greek Inscriptions|<https://inscriptions.packhum.org/text/1758?&amp;bookid=3&amp;location=1701>|
|8|2|packhum|3|6|IG I² 522 - PHI Greek Inscriptions|<https://inscriptions.packhum.org/text/1759?&amp;bookid=3&amp;location=1701>|
|9|2|packhum|3|7|IG I² 561 - PHI Greek Inscriptions|<https://inscriptions.packhum.org/text/1760?&amp;bookid=3&amp;location=1701>|
|10|2|packhum|3|8|IG I² 575 - PHI Greek Inscriptions|<https://inscriptions.packhum.org/text/1761?&amp;bookid=3&amp;location=1701>|

Here (numbers refer to Y and X; query is `select * from text_node order by y,parent_id,x;`):

- (1.1) the root node for `Attica (IG I-III)` is the one with `parent_id`=0.
- (2.1-2.2) its direct children are books `IG I²` and `IG I³`.
- (3.1-3.8) the first children of book `IG I²` are inscriptions 165-575.

Also, looking at nodes metadata you get other details, including the full inscription's text. For instance, here are the properties for node 3, corresponding to IG I² 165:

|id|node_id|name|value|
|--|-------|----|-----|
|7|3|date-phi|c. 2nd ac|
|5|3|date-txt|c. II BC|
|6|3|date-val|-150|
|4|3|location|Ath.: Od.Evangelistrias?|
|2|3|note|Att. — Ath.: Od.Evangelistrias? — c. 2nd ac — IG I³, p.972|
|9|3|phi|PH1754|
|8|3|reference|IG I³, p.972|
|1|3|text|1	․․5․․οτο— — —... etc.|

Here is the corresponding page screenshot:

![text IG I² 165](img/text.png)

As you can see, the metadata found before the text were parsed into a number of properties:

- date: the original date is `date-phi`; its approximate calculated value is -150; its "normalized" date text is `date-txt`.
- place is in `location`;
- the original PHI ID is under `phi` (see at the bottom-right corner); bibliographic references are under `reference`'s.
- `note` contains the original, unparsed full text from the page. This way, we can refine the parsing algorithm and re-parse all the properties without having to re-scrape the corpus.

Finally, you can fire the API and frontend app to browse the inscriptions as in this screenshot:

![inscriptions browser](img/ui.png)

## Text

In the end, each text will be found in its own page, having a line for each table row: `table[@class="grk"]/tbody/tr`. In the table, each `tbody/tr` has 2 `td`, the first either empty or with numbering, the second with text. Also, metadata are:

- `span[@class="ti"]` with text information.
- a final `div[@class="docref"]` contains `a` with href whose value is the PHI ID like `PHI1754`.

The note at the top of each text page is a line with U+2014 as separator, including these data (the asterisk marks those data which are mostly present):

1. region\*
2. location\*
3. type
4. date\*
5. reference(s)

For instance:

```txt
Att. — Athens: Akropolis — stoich. 28 — 440-410 a. — IG I² 87,f + 141,a, + 174 — IG I³, Add.p.950
```

Unfortunately, it's difficult to detect which field is what, as no field is required. For instance:

```txt
Att. — 440-430 a.
Att. — Lamptrai: Thiti — s. V a. — Elliot(1962) 56-58 (+) — SEG 32.19
```

Going deeper, we can observe that:

- type usually is a word in `[]` (e.g. `[pottery]`), or is related to the writing direction or layout (e.g. `stoich.` with an optional letters count, `non-stoich.`, `boustr.`, `retrogr.`).
- reference start with any of the `RefHeads.txt` prefixes.
- toponyms cannot have digits in their text once text in `()` or `[]` has been removed.
- date has a number of forms (see below).

Sample queries to lookup references in tokens:

```sql
-- single token
select distinct value
from text_node_property tnp 
where tnp.name='note' and tnp.value not like '%—%'
order by value;

-- token 2, letter A
select distinct regexp_replace(tnp.value,'^([^—]+).*$','\1') as n
from text_node_property tnp 
where tnp.name='note' and tnp.value ilike 'a%'
order by n;

-- token 3
select distinct regexp_replace(tnp.value,'^[^—]+—([^—]+).*$','\1') as n
from text_node_property tnp 
where tnp.name='note'
order by n;

-- token 4
select distinct regexp_replace(tnp.value,'^[^—]+[^—]+—([^—]+).*$','\1') as n
from text_node_property tnp 
where tnp.name='note'
order by n;

-- token 5
select distinct regexp_replace(tnp.value,'^[^—]+[^—]+[^—]+—([^—]+).*$','\1') as n
from text_node_property tnp 
where tnp.name='note'
order by n;
```

### Date

Query template for inspection:

```sql
select distinct tnp.value from text_node_property tnp
where tnp.name='date-phi' and value ~ '[^0-9IVX]/[^0-9IVX]'
order by tnp.value
```

General forms:

(A) splitting multiple dates (start from the last date to supply era and hints):

A1. preprocess: this is required to avoid splitting in a wrong way:

- normalize whitespaces, just to ease later processing.
- replace `(?)` with `?`. This appears only in these cases:
  - `126/5(?) a.`
  - `255/4 or 253/2(?)`
  - `475/50 BC(?)`
  - `5th (?) and 4th c. BC`
  - `c. 318-307 bc(?)`
  - `late (?) 2nd c. AD`
  - `later (?) Rom. Imp. period`
- extract `[...]` or `(...)` into hints.
- `or`/`od.`/`oder` + ( `sh.`/`shortly`/`slightly`) + `lat.`/`later`/`aft.`/`after`/`earlier`/`früher`/`später` + (`?`) => wrap in `()` and normalize language.
- `at the earliest` => wrap in `()` if not already inside brackets.
- `,\s+early$` => wrap in `()`.
- `w/` and `w//` => `w`(these are wrong parsing cases: not a date).
- `July/August` => `July`.
- `,\s*([0-3]?[0-9])?\s*(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[^\s]*` => month, day.

>Note: corner cases were defined by this query:

```sql
select distinct tnp.value from text_node_property tnp 
where tnp.name='date-phi' and value ~ '[^0-9IVX]/[^0-9IVX]'
order by tnp.value
```

A2. split at any of the following separators (_unless_ inside `()` or `[]`; these are already ruled out by preprocessing at A1):

- corner case: `[0-9]\?\s+[0-9]`, e.g. `159-156? 157-156? BC (re-inscr. 1st c. AD)`.
- corner case: `[^0-9IVX]/[^0-9IVX]`, e.g. `11th/beg. 12th c. AD`.
- " and "
- " or "
- " od."
- " oder "
- " & "
- ","

(B) split into datation points at `[0-9IVXtdh?]-[0-9IVX]` (start from the last date to supply era and hints).

(C) single datation point: `PREFIX? N SUFFIX?`:

C1. preprocessing:

- detach suffix: `([0-9])(a\.|p\.)` > `$1 $2`.
- `c.`, `ca.` initial = about, applied to all the points.
- `?` = dubious unless inside `()` or `[]` (e.g. `1542 AD (or later?)`, `196 AD [set up betw. 205 and 211?]`). This is found in any of these patterns:
  - at the end: `100-125 AD?`.
  - attached to N: `1025-1028? AD`, `s. V? a.`, `5th? and 4th c. BC`.
  - attached to BC/AD or equivalent suffix: `125/124 BC? [Kram. 81,D1]`
- remove suffixed `?`: any PREFIX + `?` without space: remove `?` (e.g. `early?` becomes `early`).
- `mid-([0-9])` > `med. $1`. This is because this prefix is not separated by space from the next N. All the cases of `mid-` are followed by a digit.
- replace `([0-9])\.(?: ?Jh\.)?` with `$1th` + space (e.g. `10./11.n.Chr.` > `10th /11th n.Chr.`; `10.Jh.n.Chr.` > `10th n.Chr.`).
- `later than the early`: remove.
- lookup periods and stop if match.

C2. parsing:

- PREFIX: in this order:
  - optionally any of:
    - `ante`
    - `post`
  - optionally any of:
    - `init.`, `beg.`, `Anf.`
    - `med.`, `middle`, `mid`
    - `fin.`, `end`, `Ende`, `Wende`
    - `early`, `eher`
    - `early\s*/\s*mid`
    - `late`
    - `1st half`, `2nd half`, `1.Halfte`, `2.Halfte`, `1. Halfte`, `2. Halfte`
    - `mid\s*/\s*2nd half`, `middle\s*/\s*2nd half`
    - `Drittel`, `third`, `third of`, `third of the`
  - `s.`
- N (number: N=Arabic, R=Roman):
  - `N` = year (`N.`, `N.Jh.` = century (e.g. `11.-12.Jh.n.Chr.`) has been removed by preprocessing).
  - `(N.)N.N` = dmy.
  - `R` = century.
  - `N` + `st`|`nd`|`rd`|`th` + (`c.`) = century.
  - `N/N` = year span.
  - `R/R` = centuries range.
- SUFFIX:
  - `BC`, `bc`, `ac`, `a.`, `v.Chr.`
  - `AD`, `ad`, `pc`, `p.`, `n.Chr.`

Examples:

- 65
- 65 AD
- 65 AD?
- 113-120 p.?
- 139p.
- 101/0 BC
- 439/40? AD
- 100/101 AD
- 100/101 BC
- 100-102 AD
- 100-102 BC
- 100-102 AD?
- 113-102/1 BC
- 2nd ac
- c. 2nd ac
- s. V a.
- s. VI/V a.
- beg. 116 AD
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
- 107-98 BC (Per. VIA)
- early imp.
- aet. Hadriani
- 10th-11th c. AD or later
- 114-120 AD or sh.aft.
- 122 AD or shortly after
- 1st c. BC or earlier
- 1st c. BC at the earliest
- 122 AD (or slightly later)
- 11/12 AD (A) and 21/22 AD (B)
- 115 AD, 7 Feb.
- 128/7 BC at the latest
- 13.2.139 n.Chr.
- 146-108 BC (Per. V), early
- 147-161 (or 139-141) AD
- 132/1? 9/10?
- 134? 130? 120? BC

Errors: `117-138 n.Chr.Jh.n.Chr. (Chapot)`.

There can be multiple dates, separated by " and ", " or ", `&`, comma, e.g. `100 or 101 AD`, `10/11 AD and 65/66 or 119/120 AD`, `111, 75 or 46 BC`, `114, 116, & 156 AD`. The comma is also used to add month or day and month (`120 AD, Nov.`, `115 AD, 7 Feb.`, `147 AD, March`).

## Metadata

For Packhum I currently define these metadata:

- `text`: full text.
- `note`: original unparsed note.
- `region`: region.
- `location`: location in region.
- `type`: type.
- `layout`: text layout.
- `date-phi`: the date as found in the note.
- `date-txt`: the date's text in a conventional normal form (Cadmus).
- `date-val`: the date's (approximate) numeric value.
- `reference`: zero or more references.

All these metadata occur at most once per node, except for `reference`.
