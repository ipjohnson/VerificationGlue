# A documentation site built around the next question

A visitor should be able to choose a package, run an example, and find an explanation when something fails.

This is a local design proposal. The guides and diagnostic pages come from the repository. The homepage, entry page, and README draft demonstrate the proposed presentation.

## The visual direction

Use warm paper backgrounds, deep green accents, clear typography, and code as the main visual. Amber marks failure output. Keep the homepage expressive and the documentation quiet. Dark mode follows the same hierarchy.

The homepage moves from a concrete failure message to package choices, real code examples, and task-based links. It carries the preview status next to the product introduction.

## Four surfaces to review

| Surface | What the reader gets | Preview |
| --- | --- | --- |
| Homepage | A reason to try the library and a choice of starting points | [Open homepage](/) |
| Guide | Setup, examples, section navigation, and copyable code | [Getting started with Verify](/docs/guide/getting-started-verify.md) |
| Reference | A searchable code catalog and a consistent fix format | [Diagnostic catalog](/diagnostics.md) |
| Repository README | A short introduction with routes into the docs | [README proposal](/readme-proposal.md) |

## Organize by the reader's task

1. **Start here:** choose Verify, Mocks, or Tests.
2. **Learn a package:** setup first, then common tasks and deeper behavior.
3. **Migrate:** map existing xunit and NSubstitute patterns.
4. **Troubleshoot:** search a diagnostic code and find its fix.
5. **Understand:** inspect generated code, tiers, and Native AOT behavior.

Keep design reports and research out of primary navigation. They remain available as supporting material.

## Keep the content trustworthy

The preview copies the existing Markdown at build time. It rewrites repository source links to GitHub and builds the diagnostic catalog from the page titles. Homepage example tabs are extracted from the root README's sample blocks.

The production site should retain the existing snippet, link, diagnostic, and style checks. Use the repository samples as the source of code examples. Avoid claims about speed, package availability, or supported behavior without evidence.

## Publish on GitHub Pages

The preview uses VitePress, which is built on Vite. Its base path is `/VerificationGlue/`, matching the repository's GitHub Pages path. Each guide builds to an HTML file so direct links do not depend on a server fallback.

The `.github/workflows/docs-pages.yml` workflow builds the site and deploys it to GitHub Pages on each push to `main` that changes the docs.

See the [VitePress deployment guide](https://vitepress.dev/guide/deploy) for the hosting configuration and the [search reference](https://vitepress.dev/reference/default-theme-search) for the browser search index used here.

## What would make the launch complete

- Review the README copy and the three package entry paths.
- Validate commands against the versions actually available to readers.
- Check narrow screens, keyboard navigation, contrast, and search in a browser.
- Add the site build to CI alongside the existing documentation checks.
- Enable Pages and add the published link to the repository's About section.

Further improvements can include a version policy and release notes once the release process is settled. They are not represented as existing features in this preview.
