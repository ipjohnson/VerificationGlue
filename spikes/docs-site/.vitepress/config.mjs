import { defineConfig } from 'vitepress';
const group = (text, items) => ({ text, collapsed: false, items: items.map(([text, slug]) => ({ text, link: '/docs/guide/' + slug })) });
export default defineConfig({
  title: 'VerificationGlue',
  description: 'C# checks, generated mocks, and tests built for Native AOT.',
  base: '/VerificationGlue/',
  srcDir: '.generated',
  cleanUrls: false,
  markdown: { languages: ['csharp', 'xml'], languageAlias: { csproj: 'xml' } },
  head: [['meta', { name: 'theme-color', content: '#176c50' }]],
  themeConfig: {
    siteTitle: 'VerificationGlue',
    nav: [
      { text: 'Guide', link: '/start' },
      { text: 'Diagnostics', link: '/diagnostics' },
      { text: 'Design proposal', link: '/proposal' }
    ],
    socialLinks: [{ icon: 'github', link: 'https://github.com/ipjohnson/VerificationGlue' }],
    search: { provider: 'local' },
    outline: { level: [2, 3], label: 'On this page' },
    sidebar: [
      { text: 'Start here', items: [{ text: 'Choose your path', link: '/start' }] },
      group('Verify', [['Getting started', 'getting-started-verify'], ['Soft checks', 'check'], ['Read failure messages', 'messages'], ['How tiers work', 'tiers']]),
      group('Mocks', [['Getting started', 'getting-started-mocks'], ['Choose a setup style', 'choose-a-setup-style'], ['Arguments & matchers', 'arguments-and-matchers'], ['Async members', 'mock-async-members'], ['Recursive mocks', 'recursive-mocks'], ['Generic helpers', 'mock-a-type-from-a-generic-helper']]),
      group('Tests', [['Getting started', 'getting-started-tests'], ['Parameters', 'test-parameters'], ['Lifecycle', 'test-lifecycle'], ['Containers & scopes', 'containers-and-scopes'], ['Run with Native AOT', 'run-tests-under-native-aot']]),
      group('Migration', [['From xunit assertions', 'migrate-from-xunit-assert'], ['From NSubstitute', 'migrate-from-nsubstitute'], ['NSubstitute differences', 'nsubstitute-differences']]),
      { text: 'Reference', items: [{ text: 'Diagnostic catalog', link: '/diagnostics' }, { text: 'Generated code', link: '/docs/guide/generated/interceptors' }, { text: 'Extending Verify', link: '/docs/guide/extending' }] },
      { text: 'Design review', collapsed: true, items: [{ text: 'Site proposal', link: '/proposal' }, { text: 'README proposal', link: '/readme-proposal' }] }
    ],
    footer: { message: 'VerificationGlue · Preview packages · Built from the repository’s examples.' }
  }
});
