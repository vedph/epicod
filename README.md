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

The database schema is currently designed to be able to hold data from different sources and with different formats and modeling. To this end, I have designed a very simple structure to represent the hierarchical organization of each corpus.

In fact, it's easy to realize that whatever corpus we will handle will include a set of texts in some hierarchical structure. For instance, in the case of Packhum this is the one appearing from the site: regions include books, books include texts.

As we have no clue about the structure of other corpora, nor about the metadata eventually attached to any level of it, I've designed a tree-like structure. In it, the central entity is a (tree) node. The node can correspond to a text, or just to any grouping of texts, like books, or regions. Nodes are stored in table text_node.

Each node can have any number of metadata, stored in table `text_node_properties` as name=value pairs. Metadata come either from context (the level of the structure being scraped) or by parsing the short information text prepended to each inscription.

This allows to easily and neutrally represent any hierarchy in the corpus being scraped.

## Corpora

- [Packhard Humanities Greek Inscriptions](./doc/packhum.md)

## History

- 2022-07-29: upgraded to NET 6.0.
