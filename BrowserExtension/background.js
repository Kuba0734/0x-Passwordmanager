// Password Vault – Hintergrund-Skript (Service Worker)
// Einzige Stelle, die mit der Desktop-App spricht. Popup und Content-Skripte
// schicken Nachrichten hierher; das Kopplungs-Token verlässt diesen Kontext nie.

import { getHost, matchScore, generateLocal } from "./shared.js";

const API = "http://localhost:27832/api";
const PENDING_SAVE_TTL = 3 * 60 * 1000;

class VaultError extends Error {
  constructor(code) {
    super(code);
    this.code = code;
  }
}

// ============================================================
// API-Zugriff
// ============================================================

async function getToken() {
  const { token } = await chrome.storage.local.get("token");
  return token || null;
}

async function api(path, { method = "GET", body, auth = true, timeout = 8000 } = {}) {
  const headers = { "Content-Type": "application/json" };
  if (auth) {
    const token = await getToken();
    if (token) headers["X-Vault-Token"] = token;
  }

  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), timeout);
  let res;
  try {
    res = await fetch(API + path, {
      method,
      headers,
      body: method === "GET" ? undefined : JSON.stringify(body ?? {}),
      signal: ctrl.signal,
      cache: "no-store"
    });
  } catch {
    throw new VaultError("offline");
  } finally {
    clearTimeout(timer);
  }

  let data = {};
  try { data = await res.json(); } catch { /* leerer Body */ }

  if (res.status === 401 && data.error === "not_paired") {
    await chrome.storage.local.remove("token");
    throw new VaultError("not_paired");
  }
  if (!res.ok) throw new VaultError(data.error || `http_${res.status}`);
  return data;
}

/** offline | unpaired | pairing | locked | unlocked */
async function getStatus() {
  const { pairing } = await chrome.storage.session.get("pairing");
  try {
    const ping = await api("/ping", { timeout: 2500 });
    if (!ping.paired) {
      if (await getToken()) await chrome.storage.local.remove("token"); // Token wurde in der App widerrufen
      if (pairing && !pairing.result) return { state: "pairing", code: pairing.code };
      return { state: "unpaired", pairingError: pairing?.result && pairing.result !== "ok" ? pairing.result : null };
    }
    return { state: ping.unlocked ? "unlocked" : "locked" };
  } catch {
    return { state: "offline" };
  }
}

// ============================================================
// Kopplung
// ============================================================

function browserName() {
  const ua = navigator.userAgent;
  if (/OPR\/|Opera/.test(ua)) return "Opera";
  if (/Edg\//.test(ua)) return "Microsoft Edge";
  if (/Vivaldi/.test(ua)) return "Vivaldi";
  if (navigator.brave) return "Brave";
  return "Google Chrome";
}

async function startPairing() {
  const code = String(crypto.getRandomValues(new Uint32Array(1))[0] % 1_000_000).padStart(6, "0");
  await chrome.storage.session.set({ pairing: { code, started: Date.now() } });

  // Der Service Worker darf während der Bestätigung (bis 90 s) nicht einschlafen
  const keepAlive = setInterval(() => chrome.runtime.getPlatformInfo(), 20_000);

  (async () => {
    try {
      const res = await api("/pair", {
        method: "POST",
        body: { code, browser: browserName() },
        auth: false,
        timeout: 100_000
      });
      await chrome.storage.local.set({ token: res.token });
      await chrome.storage.session.set({ pairing: { code, result: "ok" } });
    } catch (e) {
      await chrome.storage.session.set({ pairing: { code, result: e.code || "failed" } });
    } finally {
      clearInterval(keepAlive);
    }
  })();

  return code;
}

// ============================================================
// Frames pro Tab (für sicheres Ausfüllen nur im passenden Frame)
// ============================================================

// Im Sitzungsspeicher statt im RAM, weil der Service Worker jederzeit neu starten kann.
// Format: frames_<tabId> = { "<frameId>": { url, hasLogin } }

async function getFrames(tabId) {
  const key = `frames_${tabId}`;
  const { [key]: stored } = await chrome.storage.session.get(key);
  return new Map(Object.entries(stored ?? {}).map(([id, f]) => [Number(id), f]));
}

// Mehrere Frames melden sich gleichzeitig → Schreibzugriffe pro Tab nacheinander ausführen
const frameLocks = new Map();

function rememberFrame(sender, info) {
  if (!sender.tab) return Promise.resolve();
  const tabId = sender.tab.id;
  const next = (frameLocks.get(tabId) ?? Promise.resolve())
    .then(() => writeFrame(sender, info))
    .catch(() => {});
  frameLocks.set(tabId, next);
  return next;
}

async function writeFrame(sender, info) {
  const key = `frames_${sender.tab.id}`;
  let tabFrames = await getFrames(sender.tab.id);
  // Neue Seite im Haupt-Frame → alte Frames vergessen
  if (sender.frameId === 0 && tabFrames.get(0)?.url !== sender.url) tabFrames = new Map();
  tabFrames.set(sender.frameId, { url: sender.url, hasLogin: !!info.hasLogin });
  await chrome.storage.session.set({ [key]: Object.fromEntries(tabFrames) });
}

chrome.tabs.onRemoved.addListener(tabId => {
  chrome.storage.session.remove([`save_${tabId}`, `frames_${tabId}`]);
});

async function updateBadge(tabId, url) {
  try {
    const { entries } = await api("/entries?url=" + encodeURIComponent(url));
    const n = entries.length;
    await chrome.action.setBadgeBackgroundColor({ tabId, color: "#FFFFFF" });
    await chrome.action.setBadgeTextColor?.({ tabId, color: "#0A0A0A" });
    await chrome.action.setBadgeText({ tabId, text: n > 0 ? String(n) : "" });
  } catch {
    await chrome.action.setBadgeText({ tabId, text: "" }).catch(() => {});
  }
}

/**
 * Schickt Zugangsdaten nur an Frames, deren Herkunft zum Eintrag passt.
 * Ausnahme: der Benutzer wählt im Popup bewusst einen fremden Eintrag → nur Haupt-Frame.
 */
async function fillTab(tabId, entry, { allowMismatch = false } = {}) {
  const tabFrames = await getFrames(tabId);
  let targets = [...tabFrames.entries()]
    .filter(([, f]) => f.hasLogin && matchScore(entry.url, f.url) > 0)
    .map(([id]) => id);

  if (targets.length === 0) {
    const top = tabFrames.get(0);
    if (allowMismatch || (top && matchScore(entry.url, top.url) > 0)) targets = [0];
  }

  let filled = false;
  for (const frameId of targets) {
    try {
      const res = await chrome.tabs.sendMessage(tabId,
        { type: "fill", username: entry.username, password: entry.password },
        { frameId });
      if (res?.filled) filled = true;
    } catch { /* Frame inzwischen weg */ }
  }
  return filled;
}

// ============================================================
// Speichern neuer / geänderter Logins
// ============================================================

async function handleCaptured(sender, { username, password }) {
  if (!sender.tab || !password) return;
  const url = sender.url;
  const host = getHost(url);
  if (!host) return;

  const { neverSave = [] } = await chrome.storage.local.get("neverSave");
  if (neverSave.includes(host)) return;

  let matches;
  try {
    ({ entries: matches } = await api("/entries?url=" + encodeURIComponent(url)));
  } catch {
    return; // gesperrt / offline → nichts anbieten
  }

  const sameUser = matches.find(e =>
    (e.username || "").toLowerCase() === (username || "").toLowerCase());

  let pending;
  if (sameUser) {
    const full = await api("/entries/" + encodeURIComponent(sameUser.id));
    if (full.password === password) return; // nichts Neues
    pending = { kind: "update", entryId: sameUser.id, title: sameUser.title };
  } else {
    pending = { kind: "save", title: host };
  }

  Object.assign(pending, { url, host, username, password, created: Date.now() });
  await chrome.storage.session.set({ [`save_${sender.tab.id}`]: pending });

  // Single-Page-Apps navigieren oft nicht → Banner direkt anzeigen
  chrome.tabs.sendMessage(sender.tab.id, { type: "showSaveBanner", pending: publicPending(pending) }, { frameId: 0 })
    .catch(() => {});
}

function publicPending(p) {
  // Das Passwort selbst geht nicht zurück an die Seite
  return { kind: p.kind, host: p.host, username: p.username, title: p.title };
}

async function getPendingSave(tabId, url) {
  const key = `save_${tabId}`;
  const { [key]: pending } = await chrome.storage.session.get(key);
  if (!pending) return null;
  if (Date.now() - pending.created > PENDING_SAVE_TTL || getHost(url) !== pending.host) {
    await chrome.storage.session.remove(key);
    return null;
  }
  return pending;
}

async function resolvePendingSave(tabId, action) {
  const key = `save_${tabId}`;
  const { [key]: pending } = await chrome.storage.session.get(key);
  await chrome.storage.session.remove(key);
  if (!pending) return { ok: false };

  if (action === "never") {
    const { neverSave = [] } = await chrome.storage.local.get("neverSave");
    if (!neverSave.includes(pending.host)) neverSave.push(pending.host);
    await chrome.storage.local.set({ neverSave });
    return { ok: true };
  }
  if (action !== "save") return { ok: true };

  if (pending.kind === "update") {
    await api("/entries/" + encodeURIComponent(pending.entryId), {
      method: "PUT", body: { password: pending.password }
    });
  } else {
    const origin = new URL(pending.url).origin;
    await api("/entries", {
      method: "POST",
      body: { title: prettyTitle(pending.host), username: pending.username, password: pending.password, url: origin }
    });
  }
  return { ok: true };
}

function prettyTitle(host) {
  const parts = host.split(".");
  const name = parts.length >= 2 ? parts[parts.length - 2] : host;
  return name.charAt(0).toUpperCase() + name.slice(1);
}

// ============================================================
// Nachrichten von Popup und Content-Skripten
// ============================================================

// Popup/Erweiterungsseiten haben eine chrome-extension://-URL; Content-Skripte melden
// immer die URL der Webseite und können diese Prüfung daher nicht bestehen.
const isFromExtensionPage = sender =>
  sender.id === chrome.runtime.id && !!sender.url?.startsWith(chrome.runtime.getURL(""));

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  handle(msg, sender)
    .then(sendResponse)
    .catch(e => sendResponse({ error: e.code || e.message || "error" }));
  return true; // asynchrone Antwort
});

async function handle(msg, sender) {
  // ---------- Content-Skripte ----------
  switch (msg.type) {
    case "pageInfo": {
      await rememberFrame(sender, msg);
      if (sender.frameId === 0) updateBadge(sender.tab.id, sender.url);
      const status = await getStatus();
      return { state: status.state };
    }
    case "frameMatches": {
      // Treffer für genau die Herkunft des anfragenden Frames (ohne Passwörter)
      const status = await getStatus();
      if (status.state !== "unlocked") return { state: status.state, entries: [] };
      const { entries } = await api("/entries?url=" + encodeURIComponent(sender.url));
      return { state: "unlocked", entries };
    }
    case "fillFromFrame": {
      // Ein Frame darf nur Passwörter erhalten, die zu seiner eigenen Herkunft passen
      const entry = await api("/entries/" + encodeURIComponent(msg.id));
      if (matchScore(entry.url, sender.url) === 0) throw new VaultError("origin_mismatch");
      return { username: entry.username, password: entry.password };
    }
    case "captured":
      await handleCaptured(sender, msg);
      return { ok: true };
    case "getPendingSave": {
      if (!sender.tab || sender.frameId !== 0) return null;
      const p = await getPendingSave(sender.tab.id, sender.url);
      return p ? publicPending(p) : null;
    }
    case "resolvePendingSave":
      if (!sender.tab) return { ok: false };
      return resolvePendingSave(sender.tab.id, msg.action);
    case "generate":
      return { password: await generate() };
  }

  // ---------- Nur Erweiterungsseiten (Popup) ----------
  if (!isFromExtensionPage(sender)) throw new VaultError("forbidden");

  switch (msg.type) {
    case "status":
      return getStatus();
    case "startPairing":
      return { code: await startPairing() };
    case "cancelPairing":
      await chrome.storage.session.remove("pairing");
      return { ok: true };
    case "unpair":
      await chrome.storage.local.remove("token");
      await chrome.storage.session.remove("pairing");
      return { ok: true };
    case "unlock":
      return api("/unlock", { method: "POST", body: { password: msg.password }, timeout: 20000 });
    case "lock":
      return api("/lock", { method: "POST" });
    case "entries": {
      const params = new URLSearchParams();
      if (msg.url) params.set("url", msg.url);
      if (msg.q) params.set("q", msg.q);
      return api("/entries?" + params.toString());
    }
    case "entry":
      return api("/entries/" + encodeURIComponent(msg.id));
    case "create":
      return api("/entries", { method: "POST", body: msg.entry });
    case "fill": {
      const entry = await api("/entries/" + encodeURIComponent(msg.id));
      const filled = await fillTab(msg.tabId, entry, { allowMismatch: !!msg.allowMismatch });
      return { filled };
    }
    case "insertPassword": {
      await chrome.tabs.sendMessage(msg.tabId, { type: "insertPassword", password: msg.password }, { frameId: 0 });
      return { ok: true };
    }
    case "neverSaveList": {
      const { neverSave = [] } = await chrome.storage.local.get("neverSave");
      return { neverSave };
    }
    case "clearNeverSave":
      await chrome.storage.local.set({ neverSave: [] });
      return { ok: true };
  }

  throw new VaultError("unknown_message");
}

async function generate() {
  try {
    const { password } = await api("/generate");
    return password;
  } catch {
    return generateLocal();
  }
}

// ============================================================
// Kontextmenü & Tastenkürzel
// ============================================================

chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: "pv-generate",
    title: "Sicheres Passwort einfügen",
    contexts: ["editable"]
  });
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId !== "pv-generate" || !tab?.id) return;
  const password = await generate();
  chrome.tabs.sendMessage(tab.id, { type: "insertPassword", password }, { frameId: info.frameId ?? 0 })
    .catch(() => {});
});

chrome.commands.onCommand.addListener(async command => {
  if (command !== "autofill") return;
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab?.id) return;
  const top = (await getFrames(tab.id)).get(0);
  if (!top) return;
  try {
    const { entries } = await api("/entries?url=" + encodeURIComponent(top.url));
    if (entries.length === 0) return;
    const entry = await api("/entries/" + encodeURIComponent(entries[0].id));
    await fillTab(tab.id, entry);
  } catch { /* gesperrt oder offline */ }
});
