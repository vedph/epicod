# Clauss

EDCS: Epigraphik-Datenbank Clauss.

## Scraper

Root entry: <https://db.edcs.eu/epigr/epitest.php> with the search form. We drive the search by picking content region by region.

Available regions are:

TODO: list of regions

(A) level 1 (regions): TODO

(B) level 2 (region's inscriptions): get to the page via a form POST to <https://db.edcs.eu/epigr/epitest.php>, filling the form parameters accordingly (`p_provinz`, `s_sprache`=`en`).

This gets a static web page structured as follows:

- `//b` with text "inscriptions found: N": the count of inscriptions. This is read from the page for checking.

- `body/p`: each inscription is in a paragraph.

(C) level 3 (inscription): split the content of this `p` at each `br`. The result is metadata and text as follows:

- for text, `br` is not followed by `b`, but directly by a text node. The text can be Latin, Greek, or a mix of the two.

- metadata: in general their value is a text node preceded by a sibling `b` element with their name. See the following subsections for special metadata. Metadata names should be trimmed, the end `:` removed, and eventually split at `/`.

### Metadatum: dating/to

AFAIK the `dating` metadatum is always followed by a `to` metadatum to represent a range between two year values:

```html
<b>dating:</b> 121&nbsp;<b>to</b>122&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
```

### Metadatum: publication

Always at the start of each inscription. Followed either by a text sibling, or by `a`. In the latter case, the text is a comma delimited list, while `@href` links to a PHP script which gets as parameters `s_language` (`en`) and `bild` with `$` + image file names:

```html
<b>publication:</b>
<a href="https://db.edcs.eu/epigr/bilder.php?s_language=en&amp;bild=$RRMAM-03-02_00127a.jpg;$RRMAM-03-02_00127a_1.jpg;$RRMAM-03-02_00127a_2.jpg"
target="_blank">RRMAM-01, 00047a = RRMAM-02-01, 00077 = RRMAM-03-02, 00127a</a>
```

### Metadatum: place

- **type 1**: text sibling (eventually `?` when unknown).

```html
<b>place:</b>
?<br />
```

- **type 2**: siblings of `place`: `script`, `a`, `noscript`. Get lat and lon from `a`. Example:

```html
<b>place:</b>
<script language="JavaScript">
<!--
document.writeln(
    "<a href=\"javascript:Neues_Fenster('osm-map.php?ort=Abazli&latitude=39.547119&longitude=33.025478&provinz=Galatia')\">Abazli</a>"
);
-->
</script>
<a
href="javascript:Neues_Fenster(&#39;osm-map.php?ort=Abazli&amp;latitude=39.547119&amp;longitude=33.025478&amp;provinz=Galatia&#39;)"
>Abazli</a
>

<noscript>
<a
    href="osm-map.php?ort='Abazli'&latitude='39.547119'&longitude='33.025478'&provinz='Galatia'"
    target="_blank"
    >Abazli</a
>
</noscript>
<br />
```
