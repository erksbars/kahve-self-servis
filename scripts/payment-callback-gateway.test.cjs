const { test } = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const { createGateway } = require('./payment-callback-gateway.cjs');
async function listen(server) { await new Promise(resolve => server.listen(0, '127.0.0.1', resolve)); return `http://127.0.0.1:${server.address().port}`; }
async function close(server) { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
test('gateway never exposes menu, admin, auth, files or arbitrary proxy paths', async () => {
  const server = createGateway(); const url = await listen(server);
  try { assert.equal((await fetch(url + '/healthz')).status, 200); for (const path of ['/api/session/me', '/admin', '/.env', '/b/demo-moda', '/api/payments/iyzico/callback/short', '/api/payments/iyzico/callback/' + 'a'.repeat(32) + '?token=secret']) assert.equal((await fetch(url + path)).status, 404); } finally { await close(server); }
});
test('callback forwards only the form to fixed local API, without cookies, auth or forwarded headers', async () => {
  let seen; const upstream = http.createServer((req, res) => { const chunks = []; req.on('data', c => chunks.push(c)); req.on('end', () => { seen = { url: req.url, headers: req.headers, body: Buffer.concat(chunks).toString() }; res.writeHead(200, { 'Set-Cookie': 'private=value' }); res.end('private provider response'); }); });
  const api = await listen(upstream); const server = createGateway({ apiOrigin: api }); const url = await listen(server); const path = '/api/payments/iyzico/callback/' + 'a'.repeat(32);
  try { const response = await fetch(url + path, { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded', Cookie: 'owner=secret', Authorization: 'Bearer private', 'X-Forwarded-Host': 'evil.test' }, body: 'token=test' }); assert.equal(response.status, 200); assert.equal(await response.text(), 'OK'); assert.equal(response.headers.get('set-cookie'), null); assert.equal(seen.body, 'token=test'); assert.equal(seen.url, path); assert.equal(seen.headers.cookie, undefined); assert.equal(seen.headers.authorization, undefined); assert.equal(seen.headers['x-forwarded-host'], undefined); } finally { await close(server); await close(upstream); }
});
test('wrong content type and oversized callback do not reach API', async () => {
  const server = createGateway(); const url = await listen(server); const path = '/api/payments/iyzico/callback/' + 'a'.repeat(32);
  try { assert.equal((await fetch(url + path, { method: 'POST', body: 'token=test' })).status, 415); assert.equal((await fetch(url + path, { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: 'x'.repeat(70000) })).status, 413); } finally { await close(server); }
});
test('callback flood is limited before upstream access', async () => {
  const upstream = http.createServer((req, res) => { req.resume(); res.end('OK'); }); const api = await listen(upstream); const server = createGateway({ apiOrigin: api, limit: 1 }); const url = await listen(server); const path = '/api/payments/iyzico/callback/' + 'a'.repeat(32);
  try { const request = { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: 'token=test' }; assert.equal((await fetch(url + path, request)).status, 200); assert.equal((await fetch(url + path, request)).status, 429); } finally { await close(server); await close(upstream); }
});
test('callback browser return allows only the local iyzico receipt capability route', async () => {
  let location = 'http://127.0.0.1:5187/iyzico/pay/' + 'a'.repeat(43);
  const upstream = http.createServer((req, res) => { req.resume(); res.writeHead(302, { Location: location, 'Set-Cookie': 'private=value' }); res.end(); }); const api = await listen(upstream); const server = createGateway({ apiOrigin: api }); const url = await listen(server); const path = '/api/payments/iyzico/callback/' + 'a'.repeat(32);
  try { const request = { method: 'POST', redirect: 'manual', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: 'token=test' }; const allowed = await fetch(url + path, request); assert.equal(allowed.status, 303); assert.equal(allowed.headers.get('location'), location); assert.equal(allowed.headers.get('set-cookie'), null); location = 'https://evil.test'; assert.equal((await fetch(url + path, request)).status, 502); } finally { await close(server); await close(upstream); }
});
