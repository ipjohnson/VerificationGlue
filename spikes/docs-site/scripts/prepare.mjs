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
const snippets = {};
for (const [key, [file, name]] of Object.entries({
  Verify: ['samples/VerifyInXunit/OrderTests.cs', 'verify-in-xunit'],
  Mocks: ['samples/MocksGuide/SetupStyles.cs', 'mocks-style-typed'],
  Tests: ['samples/TestsGettingStarted/CartTests.cs', 'first-verify-tests'],
})) {
  const source = await readFile(path.join(repo, file), 'utf8');
  const code = source.match(new RegExp(`^[ \\t]*#region ${name}[ \\t]*\\r?\\n([\\s\\S]*?)^[ \\t]*#endregion`, 'm'))?.[1];
  if (!code) throw new Error(`Missing region ${name} in ${file}`);
  const lines = code.replace(/\s+$/, '').split(/\r?\n/);
  const indent = Math.min(...lines.filter((line) => line.trim()).map((line) => line.match(/^ */)[0].length));
  snippets[key] = lines.map((line) => line.slice(indent)).join('\n').trim();
}
await writeFile(path.join(out, 'examples.json'), JSON.stringify(snippets, null, 2));
const diagnostics = [];
for (const name of (await readdir(path.join(repo, 'docs/diagnostics'))).sort()) {
  const title = (await readFile(path.join(repo, 'docs/diagnostics', name), 'utf8')).split('\n')[0].replace(/^# /, '');
  diagnostics.push(`| [${name.replace('.md', '')}](/docs/diagnostics/${name}) | ${title.replace(/^[^:]+: /, '').replaceAll('<', '&lt;').replaceAll('>', '&gt;')} |`);
}
await writeFile(path.join(out, 'diagnostics.md'), `# Find a diagnostic\n\nFind the code from your build output. Each page explains the cause, shows an example, and gives the fix.\n\nUse the search box to look up a code or describe the problem.\n\n| Code | Message |\n| --- | --- |\n${diagnostics.join('\n')}\n`);
console.log(`Prepared existing docs and ${diagnostics.length} diagnostic entries.`);
