const status = document.getElementById("status");
const excluded = document.getElementById("excluded");
document.getElementById("identity").value = chrome.runtime.id;
chrome.storage.local.get(["enabled", "excluded"]).then(value => {
  excluded.value = (value.excluded || []).join("\n");
  status.textContent = value.enabled ? "Prévias ativadas" : "Prévias desativadas";
});
chrome.storage.session.get("bridgeStatus").then(value => { if (value.bridgeStatus) status.textContent += " · " + value.bridgeStatus; });
document.getElementById("copy").onclick = () => navigator.clipboard.writeText(chrome.runtime.id);
document.getElementById("enable").onclick = async () => {
  const allowed = await chrome.permissions.request({ origins: ["<all_urls>"] });
  if (!allowed) { status.textContent = "Sem autorização: nenhuma captura ativada."; return; }
  const domains = excluded.value.split(/\s+/).map(value => value.toLowerCase().replace(/^https?:\/\//, "").split("/")[0]).filter(value => /^[a-z0-9.-]+$/.test(value));
  await chrome.storage.local.set({ enabled: true, excluded: domains });
  await chrome.runtime.sendMessage({ type: "clear" });
  await chrome.runtime.sendMessage({ type: "refresh" });
  status.textContent = "Ativado. Visite as abas para registrar suas prévias.";
};
document.getElementById("disable").onclick = async () => {
  await chrome.storage.local.set({ enabled: false });
  await chrome.runtime.sendMessage({ type: "clear" });
  await chrome.permissions.remove({ origins: ["<all_urls>"] });
  status.textContent = "Desativado. Capturas locais apagadas se o conector estiver acessível.";
};
