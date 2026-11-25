'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { versionTag, webhookToken, summarizeNotes, prepareRelease, testMessage, readRelease, outputFields } = require('./prepare-discord-release.cjs');
const base = 'https://github.com/remicks/RMPCS-Windows';
function release() {
  const names = ['RMPCS.Setup.exe','RMPCS-Setup-0.9.2-x64.msi','RMPCS-v0.9.2-win-x64-portable.zip','RMPCS-v0.9.2-icons.zip','RMPCS-v0.9.2.zip','RUNTIMES.md','SHA256SUMS.txt'];
  return { draft: false, tag_name: 'v0.9.2', prerelease: true, html_url: `${base}/releases/tag/v0.9.2`, body: 'New fixes. @everyone <@12345>', assets: names.map(name => ({ name, size: 100, state: 'uploaded', browser_download_url: `${base}/releases/download/v0.9.2/${name}` })) };
}
test('exact release tag validation rejects paths and shell expressions', () => {
  assert.equal(versionTag('v0.9.2'), '0.9.2');
  for (const value of ['latest', 'v0.9.2/other', 'v01.2.3', 'v0.9.2\n', 'v0.9.2;echo secret']) assert.throws(() => versionTag(value));
});
test('webhook validation permits only official HTTPS webhook paths', () => {
  const path = `/api/webhooks/123456789012345678/${'a'.repeat(68)}`;
  assert.equal(webhookToken('https://discord.com' + path), 'a'.repeat(68));
  for (const value of ['', 'http://discord.com' + path, 'https://discord.com.evil.test' + path, 'https://user@discord.com' + path, 'https://discord.com' + path + '?wait=true', 'https://discord.com' + path + '#x', 'https://127.0.0.1' + path]) assert.throws(() => webhookToken(value));
});
test('preview announcement includes only verified download links and neutralizes mentions', () => {
  const prepared = prepareRelease(release(), 'v0.9.2');
  assert.match(prepared.name, /Preview/); assert.match(prepared.body, /Portable ZIP/); assert.match(prepared.body, /Checksums/); assert.match(prepared.body, /RUNTIMES/); assert.ok(!prepared.body.includes('@')); assert.ok(prepared.body.length < 3800);
});
test('stable announcement label and bounded notes', () => {
  const value = release(); value.prerelease = false; value.body = 'x'.repeat(60000);
  const prepared = prepareRelease(value, 'v0.9.2'); assert.ok(!prepared.name.includes('Preview')); assert.ok(prepared.body.length < 3800);
});

test('changelog summary keeps three short highlights and omits later details', () => {
  const notes = '# Changes\n- Fixed installer upgrades. Additional implementation details.\n- Improved runtime checks. More details.\n- Restart handling ' + 'improved '.repeat(100) + '\n- Fourth change should remain on GitHub.';
  const summary = summarizeNotes(notes);
  assert.equal(summary.split('\n').length, 3);
  assert.match(summary, /Fixed installer upgrades\./);
  assert.ok(!summary.includes('implementation details'));
  assert.ok(!summary.includes('Fourth change'));
  assert.ok(summary.split('\n').every(line => line.length <= 142));
  assert.ok(summary.length < 450);
});

test('summary supports prose, empty notes, markdown and mention safety', () => {
  assert.equal(summarizeNotes(''), 'See the release notes for details.');
  assert.equal(summarizeNotes('RMPCS v0.12.1\n\nFixed upgrades. Technical details follow.'), '\u2022 Fixed upgrades.');
  const summary = summarizeNotes('```text\n- Hidden example\n```\n- **Updated** [settings](https://example.com). More details.\n- Notify @everyone safely.');
  assert.ok(!summary.includes('Hidden example'));
  assert.match(summary, /Updated settings\./);
  assert.ok(!summary.includes('@'));
});

test('large release notes produce a compact embed with a full changelog link', () => {
  const value = release(); value.body = Array.from({length: 100}, (_, i) => `- Change ${i}: ${'detail '.repeat(70)}`).join('\n');
  const prepared = prepareRelease(value, 'v0.9.2');
  assert.ok(prepared.body.length < 1400);
  assert.ok(testMessage(prepared, true).body.length < 1600);
  assert.ok(prepared.body.endsWith(`[Full release notes and all downloads](${value.html_url})`));
  assert.ok(!prepared.body.includes('Change 3:'));
});
test('drafts, mismatched tags, foreign links and incomplete uploads fail closed', () => {
  for (const change of [{draft:true},{tag_name:'v0.9.3'},{html_url:'https://evil.test'},{assets:[]},{prerelease:null}]) assert.throws(() => prepareRelease({...release(),...change},'v0.9.2'));
  for (const change of [{state:'new'},{size:0},{browser_download_url:'https://evil.test/file'}]) { const value=release();Object.assign(value.assets[0],change);assert.throws(()=>prepareRelease(value,'v0.9.2')); }
});
test('metadata fetch uses fixed authenticated GitHub endpoint with redirect refusal', async () => {
  const result = await readRelease('v0.9.2','fixture-read-token',async (url, options) => { assert.equal(url,'https://api.github.com/repos/remicks/RMPCS-Windows/releases/tags/v0.9.2'); assert.equal(options.redirect,'error');assert.equal(options.headers.Authorization,'Bearer fixture-read-token');return new Response(JSON.stringify(release()),{status:200}); });
  assert.equal(result.tag_name,'v0.9.2');
});
test('metadata rejects unavailable and oversized responses', async () => {
  await assert.rejects(readRelease('v0.9.2','fixture',async()=>new Response('',{status:404})));
  await assert.rejects(readRelease('v0.9.2','fixture',async()=>new Response('x'.repeat(1048577))));
});
test('multiline output is data, preserving shell-like release notes without executing them', () => {
  const fs=require('node:fs'),os=require('node:os'),path=require('node:path');const dir=fs.mkdtempSync(path.join(os.tmpdir(),'rmpcs-announcement-'));const file=path.join(dir,'out');
  try {outputFields(file,{body:'line one\n$(not-executed)\nEOF\nline two'});const text=fs.readFileSync(file,'utf8');assert.match(text,/body<<RMPCS_/);assert.match(text,/\$\(not-executed\)/);} finally {fs.rmSync(file,{force:true});fs.rmdirSync(dir);}
});

test('explicit tests are labeled without claiming a new release', () => {
  const prepared = prepareRelease(release(),'v0.9.2');
  const marked = testMessage(prepared,true);
  assert.ok(marked.name.startsWith('[Test]'));
  assert.match(marked.body,/No new version is being announced/);
  assert.equal(marked.url,prepared.url);
  assert.equal(testMessage(prepared,false),prepared);
  assert.ok(marked.body.length<3800);
});
