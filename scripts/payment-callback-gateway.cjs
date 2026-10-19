const http = require('node:http');

function createGateway({ apiOrigin = 'http://127.0.0.1:5087', limit = 120 } = {}) {
  let windowStart = Date.now(), requests = 0;
  return http.createServer((req, res) => {
    const reply = (status, text) => { res.writeHead(status, { 'Content-Type': 'text/plain; charset=utf-8', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff', 'Referrer-Policy': 'no-referrer' }); res.end(text); };
    if (req.method === 'GET' && (req.url === '/' || req.url === '/healthz')) return reply(200, 'Kahve payment callback gateway ready');
    if (req.method !== 'POST' || !/^\/api\/payments\/iyzico\/callback\/[a-f0-9]{32}$/.test(req.url)) return reply(404, 'Not found');
    if (!/^application\/x-www-form-urlencoded(?:\s*;|$)/i.test(req.headers['content-type'] ?? '')) return reply(415, 'Unsupported content type');
    if (Date.now() - windowStart >= 60000) { windowStart = Date.now(); requests = 0; }
    if (++requests > limit) return reply(429, 'Too many requests');
    const chunks = []; let size = 0, exceeded = false;
    req.on('data', chunk => { size += chunk.length; if (size > 65536) { if (!exceeded) reply(413, 'Request too large'); exceeded = true; chunks.length = 0; } else if (!exceeded) chunks.push(chunk); });
    req.on('end', () => {
      if (exceeded) return;
      const body = Buffer.concat(chunks);
      const upstream = http.request(new URL(req.url, apiOrigin), { method: 'POST', timeout: 25000, headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'Content-Length': body.length } }, response => {
        // Callback acknowledgement only. Never reflect redirects, cookies or application response bodies.
        response.resume(); response.on('end', () => {
          if ([302, 303].includes(response.statusCode)) {
            const location = response.headers.location ?? '';
            if (!/^http:\/\/127\.0\.0\.1:5187\/iyzico\/pay\/[A-Za-z0-9_-]{43}$/.test(location)) return reply(502, 'Invalid return address');
            res.writeHead(303, { Location: location, 'Cache-Control': 'no-store', 'Referrer-Policy': 'no-referrer' }); return res.end();
          }
          reply(response.statusCode ?? 502, response.statusCode >= 200 && response.statusCode < 300 ? 'OK' : 'Callback was not accepted');
        });
      });
      upstream.on('timeout', () => upstream.destroy());
      upstream.on('error', () => { if (!res.writableEnded) reply(502, 'Callback service unavailable'); });
      upstream.end(body);
    });
    req.on('error', () => { if (!res.writableEnded) reply(400, 'Invalid request'); });
  });
}
module.exports = { createGateway };
if (require.main === module) {
  const port = Number(process.env.PAYMENT_GATEWAY_PORT ?? 5190);
  createGateway().listen(port, '127.0.0.1');
}
