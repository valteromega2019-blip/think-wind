const encoder = new TextEncoder();
const hex = bytes => Array.from(new Uint8Array(bytes), value => value.toString(16).padStart(2, "0")).join("");
async function digest(value) { return hex(await crypto.subtle.digest("SHA-256", encoder.encode(value))); }
async function keyed(secret, value) {
  const key = await crypto.subtle.importKey("raw", encoder.encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  return hex(await crypto.subtle.sign("HMAC", key, encoder.encode(value)));
}
function reply(body, status = 200) {
  return Response.json(body, { status, headers: { "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff", "Referrer-Policy": "no-referrer" } });
}
async function json(request) {
  if (!request.headers.get("content-type")?.toLowerCase().startsWith("application/json")) throw new Error("invalid");
  const reader = request.body?.getReader();
  if (!reader) throw new Error("invalid");
  let size = 0; const chunks = [];
  try {
    while (true) {
      const { value, done } = await reader.read(); if (done) break;
      size += value.byteLength;
      if (size > 2048) { await reader.cancel(); throw new Error("invalid"); }
      chunks.push(value);
    }
  } finally { reader.releaseLock(); }
  const bytes = new Uint8Array(size); let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
  return JSON.parse(new TextDecoder().decode(bytes));
}
function bearer(request) {
  const token = request.headers.get("authorization")?.match(/^Bearer ([A-Za-z0-9-]{32,128})$/)?.[1];
  return token ?? "";
}
async function authorized(request, env, id) {
  const token = bearer(request); if (!token) return false;
  const record = await env.DB.prepare("SELECT token_hash FROM registrations WHERE id = ?").bind(id).first();
  return Boolean(record && record.token_hash === await digest(token));
}
const validId = value => typeof value === "string" && /^[0-9a-f-]{36}$/.test(value);
const versionOf = body => typeof body.version === "string" && /^[0-9A-Za-z.+-]{1,40}$/.test(body.version) ? body.version : "unknown";
export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (request.method === "GET" && url.pathname === "/health") return reply({ service: "orby-registration" });
    if (!env.DB || !env.EMAIL_HASH_KEY || !env.ADMIN_TOKEN || !env.REGISTRATION_LIMIT) return reply({ error: "unavailable" }, 503);
    try {
      if (request.method === "POST" && url.pathname === "/v1/register") {
        // Browser cross-origin registrations are not supported. Desktop requests
        // carry no Origin and still receive a per-network abuse limit.
        if (request.headers.has("origin")) return reply({ error: "forbidden" }, 403);
        const networkKey = await keyed(env.EMAIL_HASH_KEY, "network:" + (request.headers.get("CF-Connecting-IP") ?? "unknown"));
        if (!(await env.REGISTRATION_LIMIT.limit({ key: networkKey })).success) return reply({ error: "try_later" }, 429);
        let body; try { body = await json(request); } catch { return reply({ error: "invalid_request" }, 400); }
        const email = typeof body?.email === "string" ? body.email.trim().toLowerCase() : "";
        if (email.length > 254 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return reply({ error: "invalid_request" }, 400);
        const id = crypto.randomUUID(), token = crypto.randomUUID() + crypto.randomUUID();
        const now = Math.floor(Date.now() / 1000);
        await env.DB.prepare("INSERT INTO registrations (id, email_key, token_hash, created_at, last_seen, version) VALUES (?, ?, ?, ?, ?, ?)")
          .bind(id, await keyed(env.EMAIL_HASH_KEY, "email:" + email), await digest(token), now, now, versionOf(body)).run();
        return reply({ id, token }, 201);
      }
      if (request.method === "POST" && url.pathname === "/v1/activity") {
        let body; try { body = await json(request); } catch { return reply({ error: "invalid_request" }, 400); }
        if (!validId(body?.id) || !await authorized(request, env, body.id)) return reply({ error: "unauthorized" }, 401);
        const now = Math.floor(Date.now() / 1000);
        await env.DB.prepare("UPDATE registrations SET last_seen = ?, version = ? WHERE id = ? AND last_seen < ?")
          .bind(now, versionOf(body), body.id, now - 86400).run();
        return reply({ ok: true });
      }
      if (request.method === "DELETE" && url.pathname.startsWith("/v1/registration/")) {
        const id = url.pathname.slice("/v1/registration/".length);
        if (!validId(id) || !bearer(request)) return reply({ error: "unauthorized" }, 401);
        const existing = await env.DB.prepare("SELECT token_hash FROM registrations WHERE id = ?").bind(id).first();
        if (!existing) return reply({ ok: true }); // Idempotent after a lost response.
        if (existing.token_hash !== await digest(bearer(request))) return reply({ error: "unauthorized" }, 401);
        // A person may have several installations. Their receipt permits deleting
        // only this installation, never somebody else's registration or activity.
        await env.DB.prepare("DELETE FROM registrations WHERE id = ?").bind(id).run();
        return reply({ ok: true });
      }
      if (request.method === "GET" && url.pathname === "/admin/stats") {
        const token = bearer(request);
        if (!token || await digest(token) !== await digest(env.ADMIN_TOKEN)) return reply({ error: "unauthorized" }, 401);
        const since = Math.floor(Date.now() / 1000) - 30 * 86400;
        const totals = await env.DB.prepare("SELECT COUNT(DISTINCT email_key) AS registered_people, COUNT(*) AS installations, COUNT(DISTINCT CASE WHEN last_seen >= ? THEN email_key END) AS active_30_days FROM registrations")
          .bind(since).first();
        return reply({ ...totals, notice: "Contagem aproximada: cadastro opcional e e-mails não verificados. Não representa todos os usuários do Orby." });
      }
      return reply({ error: "not_found" }, 404);
    } catch {
      // Never log request bodies, email, bearer tokens or database exceptions.
      return reply({ error: "unavailable" }, 503);
    }
  }
};
