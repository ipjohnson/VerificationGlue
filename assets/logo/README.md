# Logo

The VerificationGlue logo is the glue drop: a green drop with a check cut out of it. `spikes/logo/README.md` records how it was chosen.

| File | Use |
| --- | --- |
| `mark.svg` | The mark on a light background |
| `mark-dark.svg` | The mark on a dark background |
| `icon.svg` | The mark on a green tile |
| `icon-128.png`, `icon-256.png`, `icon-512.png` | `icon.svg` as PNG. Each NuGet package packs `icon-128.png` as its icon. |
| `lockup.svg` | The mark with the VerificationGlue wordmark, light background |
| `lockup-dark.svg` | The mark with the wordmark, dark background |
| `lockup.png` | `lockup.svg` as PNG at twice its size. Each package README shows it, because nuget.org does not show SVG images. |

The wordmark is set in Inter Display Bold and converted to outlines, so it looks the same on every machine. Inter is licensed under the SIL Open Font License 1.1. The font is not in the repository.

`build.py` rebuilds every file here from `spikes/logo/b-glue-drop`. Download Inter from https://github.com/rsms/inter/releases, install `uharfbuzz`, `fonttools` and `resvg-py`, and run `python3 build.py PATH/TO/InterDisplay-Bold.ttf`.

| Name | Light | Dark |
| --- | --- | --- |
| Green | `#1D6B4F` | `#6FCF9F` |
| Ink | `#17241D` | `#F1EEE7` |
| Background | `#FAF8F4` | `#17241D` |
