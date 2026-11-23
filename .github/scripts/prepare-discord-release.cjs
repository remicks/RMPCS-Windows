'use strict';
const fs = require('node:fs');
const crypto = require('node:crypto');
const path = require('node:path');
const REPOSITORY = 'remicks/RMPCS-Windows';
const BASE = `https://github.com/${REPOSITORY}`;
function versionTag(value) {
  if (typeof value !== 'string' || value.trim() !== value || !/^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$/.test(value)) throw new Error('Use an exact RMPCS release tag such as v0.9.2.');
  return value.slice(1);
}
function webhookToken(value) {
  let url;
  try { url = new URL((value || '').trim()); } catch { throw new Error('DISCORD_WEBHOOK_URL is missing or invalid. Set the repository Actions secret.'); }
  const match = /^\/api(?:\/v[0-9]+)?\/webhooks\/[0-9]{17,20}\/([A-Za-z0-9_-]{30,200})$/.exec(url.pathname);
  if (url.protocol !== 'https:' || !['discord.com', 'discordapp.com'].includes(url.hostname) || url.port || url.username || url.password || url.search || url.hash || !match) throw new Error('Use a standard HTTPS Discord webhook URL without query parameters.');
  return match[1];
}
function prepareRelease(release, tag) {
  const version = versionTag(tag);
  if (!release || release.draft !== false || release.tag_name !== tag || release.html_url !== `${BASE}/releases/tag/${tag}` || typeof release.body !== 'string' || release.body.length > 65536 || typeof release.prerelease !== 'boolean') throw new Error('The requested release is not a valid published RMPCS release.');
  const names = ['RMPCS.Setup.exe', `RMPCS-Setup-${version}-x64.msi`, `RMPCS-v${version}-win-x64-portable.zip`, `RMPCS-v${version}-icons.zip`, `RMPCS-v${version}.zip`, 'RUNTIMES.md', 'SHA256SUMS.txt'];
  if (!Array.isArray(release.assets) || !names.every(name => release.assets.filter(a => a.name === name && a.state === 'uploaded' && Number.isSafeInteger(a.size) && a.size > 0 && a.browser_download_url === `${BASE}/releases/download/${tag}/${name}`).length === 1)) throw new Error('Release files are missing or still uploading. No announcement prepared.');
  const download = `${BASE}/releases/download/${tag}`;
  const notes = release.body.replace(/@/g, '\uFF20').replace(/\r/g, '').slice(0, 2400);
  return {
    name: `RMPCS ${tag} - Rusty's PC Toolkit${release.prerelease ? ' (Preview)' : ''}`,
    url: release.html_url,
    body: `${release.prerelease ? '**Preview release**' : '**New release**'}\n\n[Install RMPCS](${download}/RMPCS.Setup.exe) | [Portable ZIP](${download}/RMPCS-v${version}-win-x64-portable.zip)\n[Runtime requirements](${download}/RUNTIMES.md) | [Checksums](${download}/SHA256SUMS.txt)\n\n${notes}\n\n[Full release notes and all downloads](${release.html_url})`
  };
}
function testMessage(prepared, enabled) {
  return enabled ? { ...prepared, name: `[Test] ${prepared.name}`, body: `**Discord notification test**\nThis checks the announcement configuration for an existing release. No new version is being announced.\n\n${prepared.body}` } : prepared;
}
async function readRelease(tag, token, fetcher = fetch) {
  versionTag(tag);
  const response = await fetcher(`https://api.github.com/repos/${REPOSITORY}/releases/tags/${encodeURIComponent(tag)}`, {
    redirect: 'error', signal: AbortSignal.timeout(15000),
    headers: { Accept: 'application/vnd.github+json', Authorization: `Bearer ${token}`, 'X-GitHub-Api-Version': '2022-11-28', 'User-Agent': 'RMPCS-release-announcements' }
  });
  if (!response.ok) throw new Error(`Release metadata could not be verified (HTTP ${response.status}).`);
  if (Number(response.headers.get('content-length')) > 1048576) throw new Error('Release metadata exceeds its size budget.');
  const chunks = []; let bytes = 0;
  for await (const chunk of response.body) { bytes += chunk.length; if (bytes > 1048576) throw new Error('Release metadata exceeds its size budget.'); chunks.push(chunk); }
  return JSON.parse(Buffer.concat(chunks).toString('utf8'));
}
function outputFields(file, fields) {
  for (const [name, value] of Object.entries(fields)) {
    const delimiter = `RMPCS_${crypto.randomUUID()}`;
    fs.appendFileSync(file, `${name}<<${delimiter}\n${value}\n${delimiter}\n`, 'utf8');
  }
}
async function main() {
  if (process.env.GITHUB_REPOSITORY !== REPOSITORY) throw new Error('Announcements are restricted to the RMPCS repository.');
  const token = webhookToken(process.env.DISCORD_WEBHOOK_URL);
  console.log(`::add-mask::${token}`);
  const tag = process.env.RELEASE_TAG || `v${fs.readFileSync('release-version.txt', 'utf8').trim()}`;
  const prepared = testMessage(prepareRelease(await readRelease(tag, process.env.GH_TOKEN), tag), process.env.DISCORD_TEST === 'true');
  if (!process.env.GITHUB_OUTPUT) throw new Error('GitHub Actions output file is unavailable.');
  if (!process.env.RUNNER_TEMP) throw new Error('Runner temporary folder is unavailable.');
  const eventPath = path.join(process.env.RUNNER_TEMP, 'rmpcs-discord-release.json');
  fs.writeFileSync(eventPath, JSON.stringify({ release: { name: prepared.name, body: prepared.body, html_url: prepared.url } }), 'utf8');
  outputFields(process.env.GITHUB_OUTPUT, { ...prepared, event_path: eventPath });
  console.log(`Verified ${tag}: published release, complete download set, and webhook-secret format. No Discord request made during preflight.`);
}
if (require.main === module) main().catch(() => { console.error('Announcement preflight failed. Check the saved webhook secret, exact release tag and completed release files. Secret values were not printed.'); process.exitCode = 1; });
module.exports = { versionTag, webhookToken, prepareRelease, testMessage, readRelease, outputFields };
