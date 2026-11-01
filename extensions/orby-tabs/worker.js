const host = "com.think.orby_tabs";
let pending;
let queue = Promise.resolve();
let lastCapture = 0;
let captureEpoch = 0;

async function identity() {
  const stored = await chrome.storage.session.get("session");
  if (stored.session) return stored.session;
  const session = crypto.randomUUID();
  await chrome.storage.session.set({ session });
  return session;
}
async function deliver(message) {
  try {
    const response = await chrome.runtime.sendNativeMessage(host, { version: 1, session: await identity(), ...message });
    await chrome.storage.session.set({ bridgeStatus: response.ok ? "Conectado ao Orby" : "O Orby recusou os dados" });
  } catch {
    await chrome.storage.session.set({ bridgeStatus: "Conector não registrado. Conecte esta extensão em Personalizar → Aplicativos no Orby." });
  }
}
function eligible(tab, excluded) {
  if (tab.incognito || !tab.url) return false;
  try {
    const url = new URL(tab.url);
    return ["https:", "http:"].includes(url.protocol) && !excluded.some(domain => url.hostname === domain || url.hostname.endsWith("." + domain));
  } catch { return false; }
}
async function publish(windowId) {
  const { enabled, excluded = [] } = await chrome.storage.local.get(["enabled", "excluded"]);
  if (!enabled || !await chrome.permissions.contains({ origins: ["<all_urls>"] })) return;
  // Do no capture work while a game or another application owns the foreground.
  try { if (!(await chrome.windows.get(windowId)).focused) return; } catch { return; }
  let tabs;
  try { tabs = await chrome.tabs.query({ windowId }); } catch { return; }
  const active = tabs.find(tab => tab.active);
  const stored = await chrome.storage.session.get("shots");
  const shots = stored.shots || {};
  const now = Date.now();
  const existing = new Set((await chrome.tabs.query({})).map(tab => String(tab.id)));
  for (const id of Object.keys(shots)) if (!existing.has(id) || now - shots[id].time > 900000) delete shots[id];
  if (active && eligible(active, excluded) && active.status === "complete" && now - lastCapture >= 1100) {
    try {
      lastCapture = now;
      const before = active.id;
      const epoch = captureEpoch;
      const data = await chrome.tabs.captureVisibleTab(windowId, { format: "jpeg", quality: 55 });
      const current = (await chrome.tabs.query({ windowId, active: true }))[0];
      // Never attach another tab's pixels to a title during rapid switching.
      if (current?.id === before && current.url === active.url && captureEpoch === epoch) {
        const bitmap = await createImageBitmap(await (await fetch(data)).blob());
        const canvas = new OffscreenCanvas(320, 180);
        const context = canvas.getContext("2d");
        context.fillStyle = "#151b24"; context.fillRect(0, 0, 320, 180);
        const scale = Math.min(320 / bitmap.width, 180 / bitmap.height);
        const width = bitmap.width * scale, height = bitmap.height * scale;
        context.drawImage(bitmap, (320 - width) / 2, (180 - height) / 2, width, height);
        bitmap.close();
        const blob = await canvas.convertToBlob({ type: "image/jpeg", quality: 0.65 });
        const bytes = new Uint8Array(await blob.arrayBuffer());
        if (bytes.length <= 100000 && captureEpoch === epoch) {
          let binary = "";
          for (const byte of bytes) binary += String.fromCharCode(byte);
          shots[before] = { image: "data:image/jpeg;base64," + btoa(binary), title: active.title || "Aba", url: active.url, time: Date.now() };
        }
      }
    } catch { /* Restricted or protected pages remain title-only. */ }
  }
  // Remove outdated screenshots when a tab navigates, even if capture failed.
  for (const tab of tabs) if (!eligible(tab, excluded) || (shots[tab.id] && shots[tab.id].url !== tab.url)) delete shots[tab.id];
  const keep = Object.entries(shots).sort((a, b) => b[1].time - a[1].time).slice(0, 32);
  await chrome.storage.session.set({ shots: Object.fromEntries(keep) });
  const shown = tabs.filter(tab => eligible(tab, excluded))
    .sort((a, b) => Number(b.active) - Number(a.active) || (shots[b.id]?.time || 0) - (shots[a.id]?.time || 0)).slice(0, 4);
  await deliver({ type: "snapshot", windowId, activeTitle: active?.title || "", tabs: shown.map(tab => ({
    id: tab.id, title: (tab.title || "Aba").slice(0, 220), selected: tab.active,
    image: shots[tab.id]?.image || null, capturedAt: shots[tab.id]?.time || 0
  })) });
}
function schedule(windowId) {
  clearTimeout(pending);
  pending = setTimeout(() => { queue = queue.then(() => publish(windowId)).catch(() => {}); }, 1200);
}
chrome.tabs.onActivated.addListener(info => { captureEpoch++; schedule(info.windowId); });
chrome.tabs.onUpdated.addListener((id, change, tab) => { if (tab.active && (change.status === "complete" || change.url || change.title)) { captureEpoch++; schedule(tab.windowId); } });
chrome.tabs.onRemoved.addListener((id, info) => { if (!info.isWindowClosing) schedule(info.windowId); });
chrome.windows.onFocusChanged.addListener(id => { if (id >= 0) schedule(id); });
chrome.windows.onRemoved.addListener(windowId => { queue = queue.then(async () => { if ((await chrome.storage.local.get("enabled")).enabled) await deliver({ type: "remove", windowId }); }).catch(() => {}); });
chrome.runtime.onMessage.addListener((message, sender, reply) => {
  if (sender.id !== chrome.runtime.id || sender.tab || !sender.url?.startsWith(chrome.runtime.getURL(""))) return;
  if (message.type === "refresh") chrome.windows.getLastFocused().then(win => { schedule(win.id); reply({ ok: true }); });
  else if (message.type === "clear") {
    queue = queue.then(async () => { await chrome.storage.session.remove("shots"); await deliver({ type: "clear" }); });
    queue.then(() => reply({ ok: true })).catch(() => reply({ ok: false }));
  } else return;
  return true;
});
