import { readdir, readFile, mkdir, writeFile, rm, cp } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const site = fileURLToPath(new URL('../', import.meta.url));
const repo = path.resolve(site, '../..');
const out = path.join(site, '.generated');
const github = 'https://github.com/ipjohnson/VerificationGlue/blob/main/';
await rm(out, { recursive: true, force: true });
await mkdir(out, { recursive: true });
await cp(path.join(site, 'pages'), out, { recursive: true });
async function visit(dir) {
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const file = path.join(dir, entry.name);
    if (entry.isDirectory()) { await visit(file); continue; }
    const relative = path.relative(repo, file);
    const target = path.join(out, relative);
    await mkdir(path.dirname(target), { recursive: true });
    if (!file.endsWith('.md')) { await cp(file, target); continue; }
    let content = await readFile(file, 'utf8');
    content = content.replace(/^# (.*)$/gm, (_, title) => '# ' + title.replaceAll('<', '&lt;').replaceAll('>', '&gt;'));
    const rewrite = (url) => {
      if (/^(https?:|mailto:|#)/.test(url)) return url;
      const [pathname, anchor] = url.split('#');
      const resolved = pathname.startsWith('/') ? pathname.slice(1) : path.relative(repo, path.resolve(path.dirname(file), pathname));
      if (resolved.startsWith('docs/') && resolved.endsWith('.md')) return url;
      return github + resolved + (anchor ? '#' + anchor : '');
    };
    // Preserve fenced examples; change only prose links and snippet source links.
    content = content.split(/(^```[^\n]*\n[\s\S]*?^```\s*$)/m).map((part, i) => i % 2 ? part : part
      .replace(/(\]\()([^\s)]+)(\))/g, (_, a, url, b) => a + rewrite(url) + b)
      .replace(/(href=['"])([^'"]+)(['"])/g, (_, a, url, b) => a + rewrite(url) + b)).join('');
    await writeFile(target, content);
  }
}
await visit(path.join(repo, 'docs'));
const readme = await readFile(path.join(repo, 'README.md'), 'utf8');
const snippets = {};
for (const [key, name] of Object.entries({ Verify: 'verify-in-xunit', Mocks: 'mocks-style-typed', Tests: 'first-verify-tests' })) {
  const section = readme.split(`<!-- snippet: ${name} -->`)[1]?.split('<!-- endSnippet -->')[0];
  const code = section?.match(/```cs\n([\s\S]*?)```/)[1];
  if (!code) throw new Error(`Missing sample ${name}`);
  snippets[key] = code.trim();
}
await writeFile(path.join(out, 'examples.json'), JSON.stringify(snippets, null, 2));
const diagnostics = [];
for (const name of (await readdir(path.join(repo, 'docs/diagnostics'))).sort()) {
  const title = (await readFile(path.join(repo, 'docs/diagnostics', name), 'utf8')).split('\n')[0].replace(/^# /, '');
  diagnostics.push(`| [${name.replace('.md', '')}](/docs/diagnostics/${name}) | ${title.replace(/^[^:]+: /, '').replaceAll('<', '&lt;').replaceAll('>', '&gt;')} |`);
}
await writeFile(path.join(out, 'diagnostics.md'), `# Find a diagnostic\n\nFind the code from your build output. Each page explains the cause, shows an example, and gives the fix.\n\nUse the search box to look up a code or describe the problem.\n\n| Code | Message |\n| --- | --- |\n${diagnostics.join('\n')}\n`);
console.log(`Prepared existing docs and ${diagnostics.length} diagnostic entries.`);
