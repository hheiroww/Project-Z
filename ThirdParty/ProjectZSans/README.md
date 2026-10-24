# ProjectZ Sans compiled vector outlines

ProjectZ Sans is derived from Inter 4.1 by Rasmus Andersson and contributors,
licensed under the SIL Open Font License 1.1 in OFL.txt. The compiled outline
representation uses its own family name to distinguish the converted data.

Upstream: https://github.com/rsms/inter/releases/tag/v4.1

The framework contains vector path commands, Unicode mappings, advance widths
and kerning for Regular, SemiBold and Bold. It contains no TTF, OTF, WOFF or
bitmap font atlas. Curves are flattened and tessellated to triangles on demand.
The generated data is licensed under OFL 1.1 and must retain its notice.

Rebuild with Python and fonttools==4.67.0:

    python tools/generate-vector-font.py --cache /temporary/folder/outside/repository

The generator verifies the SHA-256 of the upstream archive. Its inputs stay in
the temporary folder. Commit OutlineData.g.cs and OFL.txt after regeneration.
This outline renderer applies kerning and NFC normalization; it is not an
OpenType shaping engine. The Windows adapter retains operating-system shaping
for scripts outside the compiled character coverage or requiring complex shaping.
