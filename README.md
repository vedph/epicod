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

## Provisional Target

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

## Corpora

- [Packhard Humanities Greek Inscriptions](./doc/packhum.md)
