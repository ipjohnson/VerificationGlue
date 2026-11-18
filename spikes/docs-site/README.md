# Documentation preview

Review a VitePress homepage, guide layout, diagnostic catalog, and README proposal using the repository's current content.

```sh
cd spikes/docs-site
npm ci
npm run dev
```

Open `http://localhost:5173/VerificationGlue/`.

```sh
npm run build
npm run preview
```

The build writes `.vitepress/dist`. Source Markdown is copied from `docs/` into the ignored `.generated` directory before development and build. To pick up edits to the original docs during development, run `npm run prepare:docs` again. Change presentation copy in `pages/` and styles in `.vitepress/theme/`.

## Review routes

- `/VerificationGlue/`: homepage with package choices and sample tabs.
- `/VerificationGlue/start.html`: guided entry into the three packages.
- `/VerificationGlue/docs/guide/getting-started-verify.html`: guide layout with code copying and section navigation.
- `/VerificationGlue/diagnostics.html`: diagnostic catalog.
- `/VerificationGlue/docs/diagnostics/VGT1001.html`: diagnostic detail.
- `/VerificationGlue/readme-proposal.html`: proposed root README.
- `/VerificationGlue/proposal.html`: information architecture and launch plan.

The root README introduces the packages and links to this preview. It retains the existing sample snippets and build and harness details. The guides under `docs/` are unchanged. The earlier README proposal is kept as a design reference.

## Hosting

The site is published at `https://ipjohnson.github.io/VerificationGlue/`. [`.github/workflows/docs-pages.yml`](../../.github/workflows/docs-pages.yml) builds this directory and deploys `.vitepress/dist` to GitHub Pages. It runs on a push to `main` that changes `docs/`, this directory or the root README, and it can be run by hand from the Actions tab. The repository's Pages source is GitHub Actions. The Pages environment and permissions follow the [VitePress deployment guide](https://vitepress.dev/guide/deploy).

## Screenshots and validation

Captured from the production preview in Chrome:

- [Desktop homepage](mockups/home-desktop.png)
- [Mobile homepage](mockups/home-mobile.png)
- [Desktop guide](mockups/guide-desktop.png)
- [Mobile guide](mockups/guide-mobile.png)
- [Diagnostic detail](mockups/diagnostic-desktop.png)
- [README proposal](mockups/readme-desktop.png)

Validation completed: production build, local page and asset targets across 60 HTML files, sample tab switching, diagnostic search, direct guide/reference/README navigation, and mobile overflow checks at 390 pixels. No browser page errors were observed. Screenshots were reviewed for the desktop homepage, desktop guide, and mobile homepage. This is not a full accessibility audit or verification of external links. Existing .NET code and tests were not changed or rerun.
