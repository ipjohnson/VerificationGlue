<script setup>
import { ref } from 'vue';
import { withBase } from 'vitepress';
import examples from '../../.generated/examples.json';
const active = ref('Verify');
const copied = ref('Copy example');
const packages = [
  { name: 'Verify', number: '01', tag: 'EXPRESS THE EXPECTATION', description: 'Write checks in C#. Read failures that name the condition, file, and line.', path: 'getting-started-verify', code: 'Verify(order.Total == 40);' },
  { name: 'Mocks', number: '02', tag: 'CONTROL THE DEPENDENCIES', description: 'Generate mocks at build time. Set up behavior and check calls with a typed API.', path: 'getting-started-mocks', code: 'var prices = Mock.Of<IPrices>();' },
  { name: 'Tests', number: '03', tag: 'RUN THE WHOLE TEST', description: 'Discover tests at build time and run them on Microsoft.Testing.Platform.', path: 'getting-started-tests', code: '[VerifyTest]' }
];
async function copy() {
  try { await navigator.clipboard.writeText(examples[active.value]); copied.value = 'Copied'; }
  catch { copied.value = 'Select code to copy'; }
  setTimeout(() => copied.value = 'Copy example', 2200);
}
</script>
<template>
  <div class="docs-home">
    <section class="hero">
      <div class="hero-copy">
        <div class="eyebrow"><span class="status-dot"></span> C# TESTING, BUILT AT COMPILE TIME <span class="preview-label">Preview</span></div>
        <h1>Clear expectations.<br><span>Readable failures.</span></h1>
        <p class="hero-description">Checks, generated mocks, and a test runner for C#.<br class="desktop-break"> Start with the piece you need. Take it to Native AOT.</p>
        <div class="hero-actions"><a class="primary-link" :href="withBase('/start.html')">Find your starting point <span>↗</span></a><a class="text-link" href="https://github.com/ipjohnson/VerificationGlue/tree/main/samples">Explore the examples <span>→</span></a></div>
        <div class="frameworks"><span>Verify & Mocks work with</span><strong>xunit</strong><strong>NUnit</strong><strong>MSTest</strong><strong>TUnit</strong></div>
      </div>
      <div class="code-window">
        <div class="window-header"><span><i></i> OrderTests.cs</span><span>C#</span></div>
        <div class="hero-code"><span class="code-comment">// Say what should be true.</span><br><span class="code-purple">var</span> order = <span class="code-purple">new</span> Order(Id: <span class="code-orange">7</span>, Total: <span class="code-orange">40</span>);<br><br><span class="code-green">Verify</span>(order.Total == <span class="code-orange">30</span>);</div>
        <div class="failure-label"><span>↳</span> WHEN THE CHECK FAILS</div>
        <div class="failure-output"><strong>Verify failed: order.Total == 30</strong><br>expected order.Total to equal 30<br><span>at OrderTests.cs:32</span></div>
        <a class="code-source" :href="withBase('/docs/guide/getting-started-verify.html#read-a-failure')">From the tested xunit example <span>↗</span></a>
      </div>
    </section>
    <section class="package-section">
      <div class="section-heading"><div class="eyebrow">A TOOLKIT THAT FITS YOUR TESTS</div><h2>Three ways to get started.</h2><p>Keep your framework with Verify and Mocks, or try the Tests runner.</p></div>
      <div class="package-grid"><a v-for="item in packages" :key="item.name" class="package-card" :href="withBase('/docs/guide/' + item.path + '.html')"><div class="card-top"><span>{{ item.number }} / {{ item.tag }}</span><span>↗</span></div><h3>{{ item.name }}</h3><p>{{ item.description }}</p><code>{{ item.code }}</code><div class="card-link">Get started with {{ item.name }} <span>→</span></div></a></div>
    </section>
    <section class="example-section">
      <div class="example-intro"><div class="eyebrow">LESS GUESSWORK</div><h2>The code is<br>the explanation.</h2><p>Explore real examples from the repository. Every example here comes from the existing sample code.</p><a class="text-link" :href="withBase('/docs/guide/generated/interceptors.html')">Look inside the generated code →</a><div class="preview-note"><strong>Know before you adopt</strong><p>All three packages are previews. The Tests runner builds against the MTP 3 preview. Start with its setup guide for feed and runner configuration.</p></div></div>
      <div class="sample-panel"><div class="sample-toolbar"><div class="sample-tabs" aria-label="Example package"><button v-for="name in Object.keys(examples)" :key="name" :aria-pressed="active === name" :class="{ selected: active === name }" @click="active = name">{{ name }}</button></div><button class="copy-button" @click="copy" aria-live="polite">{{ copied }}</button></div><pre tabindex="0"><code>{{ examples[active] }}</code></pre></div>
    </section>
    <section class="next-section"><div><div class="eyebrow">KEEP MOVING</div><h2>A useful answer,<br>one page away.</h2></div><div class="next-links"><a :href="withBase('/docs/guide/migrate-from-nsubstitute.html')"><span><strong>Coming from NSubstitute?</strong><small>Map the patterns you already know.</small></span><span>↗</span></a><a :href="withBase('/diagnostics.html')"><span><strong>A diagnostic in your build?</strong><small>Find the code, the cause, and the fix.</small></span><span>↗</span></a><a :href="withBase('/docs/guide/run-tests-under-native-aot.html')"><span><strong>Ready for Native AOT?</strong><small>Publish and run a native test application.</small></span><span>↗</span></a></div></section>
  </div>
</template>
