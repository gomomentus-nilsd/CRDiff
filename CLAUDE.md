# CRDiff — serializes Crystal `.rpt` files to JSON for text diffing

`CRSerializer/` produces the JSON; `CRDiff/` is the command-line wrapper. Rationale for non-obvious serializer choices lives in [docs/adr/](docs/adr/); read it before changing the serializer.

- ADR 0001: conditional formulas (suppress, can grow, ...) are read from the RAS report definition via typed interfaces, not from the Engine objects, because the Engine API does not expose them and the RAS objects are bare COM objects.
