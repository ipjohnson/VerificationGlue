# Logo

The VerificationGlue logo is the glue drop: a green drop with a check cut out of it. `spikes/logo/README.md` records how it was chosen.

| File | Use |
| --- | --- |
| `mark.svg` | The mark on a light background |
| `mark-dark.svg` | The mark on a dark background |
| `icon.svg` | The mark on a green tile, for 64 px and up |
| `icon-small.svg` | The icon with a larger drop and a heavier check, for 48 px and below |
| `icon-16.png`, `icon-32.png`, `icon-48.png` | `icon-small.svg` as PNG |
| `icon-128.png`, `icon-256.png`, `icon-512.png` | `icon.svg` as PNG. Each NuGet package packs `icon-128.png` as its icon. |
| `lockup.svg` | The mark with the VerificationGlue wordmark, transparent, for a light page |
| `lockup-dark.svg` | The mark with the wordmark, transparent, for a dark page |
| `lockup-card.svg` | `lockup.svg` on a light card. It reads on light and dark pages, so the repository README uses it. |
| `lockup-card-dark.svg` | `lockup-dark.svg` on a dark card |
| `lockup-card.png`, `lockup-card-dark.png` | The cards as PNG at 368 px wide. Each package README shows `lockup-card.png`, because nuget.org shows no SVG and cannot switch an image by theme. |
| `wordmark.svg` | The wordmark outlines that the lockups are built from |

Use a transparent lockup only where the page picks it by its own theme. GitHub picks a `<picture>` source by the operating system's theme, not by the GitHub theme, so a transparent lockup can land on the wrong background there.

The wordmark is set in Inter Display Bold and converted to outlines, so it looks the same on every machine. Inter is licensed under the SIL Open Font License 1.1. The font is not in the repository.

`build.py` rebuilds every file here from `wordmark.svg` and the mark in `spikes/logo/b-glue-drop`. Install `resvg-py` and run `python3 build.py`. To rebuild `wordmark.svg` from the font as well, download Inter from https://github.com/rsms/inter/releases, install `uharfbuzz` and `fonttools`, and run `python3 build.py PATH/TO/InterDisplay-Bold.ttf`.

| Name | Light | Dark |
| --- | --- | --- |
| Green | `#1D6B4F` | `#6FCF9F` |
| Ink | `#17241D` | `#F1EEE7` |
| Background | `#FAF8F4` | `#17241D` |
| Card border | `#E4E0D6` | `#2E3D35` |
