# Logo concepts

The chosen logo is **B, the glue drop**: a green drop with a check cut out of it. Its files are in `b-glue-drop/`. The other five concepts are kept for reference and are not used.

| File | Use |
| --- | --- |
| `b-glue-drop/mark.svg` | The mark on a light background |
| `b-glue-drop/mark-dark.svg` | The mark on a dark background |
| `b-glue-drop/icon.svg` | The mark on a green tile, for favicons and the NuGet package icon |
| `b-glue-drop/lockup.svg` | The mark with the VerificationGlue wordmark, light background |
| `b-glue-drop/lockup-dark.svg` | The mark with the wordmark, dark background |

`sheet.png` and `index.html` show all six concepts side by side. `python3 build.py` regenerates every file.

The colors come from the docs-site mockups in `spikes/docs-site/mockups`.

| Name | Light | Dark |
| --- | --- | --- |
| Green | `#1D6B4F` | `#6FCF9F` |
| Ink | `#17241D` | `#F1EEE7` |
| Terracotta | `#C2693E` | `#E08A5C` |
| Background | `#FAF8F4` | `#17241D` |

The lockups set the wordmark in the system font, so they render differently on machines without SF Pro. Convert the text to outlines before using a lockup outside a browser.
