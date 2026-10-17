import { getHost, matchScore, generateLocal, strength } from "../shared.js";

const $ = sel => document.querySelector(sel);
const $$ = sel => [...document.querySelectorAll(sel)];

const state = {
  view: "loading",
  tab: null,          // aktiver Browser-Tab
  pageUrl: "",
  all: [],            // alle Einträge (ohne Passwörter)
  site: [],           // Treffer für die aktuelle Seite
  currentTab: "site",
  gen: { length: 20, upper: true, lower: true, digits: true, symbols: true, value: "" }
};

function send(msg) {
  return chrome.runtime.sendMessage(msg).then(res => {
    if (res?.error) throw Object.assign(new Error(res.error), { code: res.error });
    return res;
  });
}

// ============================================================
// Ansichten
// ============================================================

function showView(name) {
  if (state.view === name) return;
  state.view = name;
  $$(".view").forEach(v => v.classList.toggle("active", v.dataset.view === name));
  $("#lockBtn").hidden = name !== "main";
}

function setState(label, kind) {
  $("#stateLabel span").textContent = label;
  $("#stateLabel .dot").className = "dot " + (kind || "");
}

async function refresh() {
  let status;
  try { status = await send({ type: "status" }); }
  catch { status = { state: "offline" }; }

  switch (status.state) {
    case "offline":
      setState("App nicht erreichbar", "err");
      showView("offline");
      break;
    case "unpaired":
      setState("Nicht gekoppelt", "warn");
      $("#pairError").hidden = !status.pairingError;
      $("#pairError").textContent = pairingErrorText(status.pairingError);
      showView("unpaired");
      break;
    case "pairing":
      setState("Warte auf Bestätigung …", "warn");
      renderCode(status.code);
      showView("pairing");
      break;
    case "locked":
      setState("Gesperrt", "warn");
      showView("locked");
      setTimeout(() => $("#masterInput").focus(), 250);
      break;
    case "unlocked":
      setState("Entsperrt", "ok");
      await loadEntries();
      showView("main");
      break;
  }
}

function pairingErrorText(code) {
  switch (code) {
    case "denied": return "Die Kopplung wurde in der App abgelehnt.";
    case "pairing_in_progress": return "In der App ist bereits eine Kopplung offen.";
    case "offline": return "Die App war nicht erreichbar oder hat nicht rechtzeitig geantwortet.";
    default: return code ? "Kopplung fehlgeschlagen. Bitte erneut versuchen." : "";
  }
}

// ============================================================
// Kopplung
// ============================================================

function renderCode(code) {
  const el = $("#pairCode");
  if (el.dataset.code === code) return;
  el.dataset.code = code;
  el.innerHTML = [...code].map((d, i) => `<span style="animation-delay:${i * 70}ms">${d}</span>`).join("");
}

$("#pairBtn").addEventListener("click", async () => {
  const btn = $("#pairBtn");
  btn.disabled = true;
  try {
    const { code } = await send({ type: "startPairing" });
    renderCode(code);
    setState("Warte auf Bestätigung …", "warn");
    showView("pairing");
  } catch (e) {
    $("#pairError").textContent = "Kopplung konnte nicht gestartet werden (" + (e.code || e.message) + ").";
    $("#pairError").hidden = false;
  } finally {
    btn.disabled = false;
  }
});

$("#cancelPairBtn").addEventListener("click", async () => {
  await send({ type: "cancelPairing" });
  refresh();
});

// Ergebnis der Kopplung kommt asynchron vom Hintergrund-Skript
chrome.storage.onChanged.addListener((changes, area) => {
  if (area === "session" && changes.pairing?.newValue?.result) {
    if (changes.pairing.newValue.result === "ok") toast("Erfolgreich gekoppelt");
    refresh();
  }
});

$("#retryBtn").addEventListener("click", () => {
  showView("loading");
  setTimeout(refresh, 300);
});

// ============================================================
// Entsperren
// ============================================================

$("#eyeBtn").addEventListener("click", () => {
  const input = $("#masterInput");
  input.type = input.type === "password" ? "text" : "password";
  input.focus();
});

$("#unlockForm").addEventListener("submit", async e => {
  e.preventDefault();
  const input = $("#masterInput");
  const btn = $("#unlockBtn");
  const err = $("#unlockError");
  if (!input.value) return shake(input.parentElement);

  btn.classList.add("busy");
  btn.disabled = true;
  err.hidden = true;
  try {
    await send({ type: "unlock", password: input.value });
    input.value = "";
    $("#lockHero").classList.add("open");
    setTimeout(() => {
      $("#lockHero").classList.remove("open");
      refresh();
    }, 450);
  } catch (ex) {
    err.textContent = {
      wrong_password: "Falsches Master-Passwort.",
      too_many_attempts: "Zu viele Versuche – bitte 30 Sekunden warten.",
      no_vault: "Lege zuerst in der App einen Tresor an.",
      offline: "Die App ist nicht erreichbar."
    }[ex.code] || "Entsperren fehlgeschlagen.";
    err.hidden = false;
    shake($("#lockHero"));
    shake(input.parentElement);
    input.select();
  } finally {
    btn.classList.remove("busy");
    btn.disabled = false;
  }
});

$("#lockBtn").addEventListener("click", async () => {
  await send({ type: "lock" }).catch(() => {});
  toast("Tresor gesperrt");
  refresh();
});

// ============================================================
// Einträge
// ============================================================

async function loadEntries() {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  state.tab = tab;
  state.pageUrl = tab?.url || "";
  const host = getHost(state.pageUrl);

  const [{ entries: all }, site] = await Promise.all([
    send({ type: "entries" }),
    host ? send({ type: "entries", url: state.pageUrl }).then(r => r.entries) : Promise.resolve([])
  ]);
  state.all = all;
  state.site = site;

  $("#siteHost").textContent = host ? `Logins für ${host}` : "Keine Webseite geöffnet";
  $("#siteEmptyText").textContent = host
    ? "Melde dich einfach an – die Erweiterung bietet dann das Speichern an."
    : "Öffne eine Webseite, um passende Logins zu sehen.";
  $("#addHereBtn").hidden = !host;

  renderList($("#siteList"), state.site, { canFill: true });
  $("#siteEmpty").hidden = state.site.length > 0;
  renderAll();

  // Ohne Treffer direkt "Alle" zeigen
  if (state.site.length === 0 && state.currentTab === "site" && state.all.length > 0 && !host)
    switchTab("all");
}

function renderAll() {
  const q = $("#search").value.trim().toLowerCase();
  const list = q
    ? state.all.filter(e => [e.title, e.username, e.url].some(v => (v || "").toLowerCase().includes(q)))
    : state.all;
  renderList($("#allList"), list, { canFill: true, mismatchAware: true });
  $("#allEmpty").hidden = list.length > 0;
}

// Monochrom: stabiler Grauton pro Titel, damit Einträge trotzdem unterscheidbar bleiben
function avatarBg(title) {
  let hash = 17;
  for (const ch of (title || "").toLowerCase()) hash = (Math.imul(hash, 31) + ch.charCodeAt(0)) | 0;
  const light = 10 + Math.abs(hash % 6) * 3;
  return `hsl(0 0% ${light}%)`;
}

function initial(title) {
  const m = (title || "?").match(/[\p{L}\p{N}]/u);
  return (m ? m[0] : "?").toUpperCase();
}

function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (k === "class") node.className = v;
    else if (k === "style") node.style.cssText = v;
    else if (k.startsWith("on")) node.addEventListener(k.slice(2), v);
    else if (v !== false && v != null) node.setAttribute(k, v === true ? "" : v);
  }
  for (const c of children.flat()) if (c != null) node.append(c);
  return node;
}

const ICONS = {
  user: '<svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></svg>',
  key: '<svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2"/></svg>',
  eye: '<svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/></svg>',
  open: '<svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5"/></svg>'
};

function iconBtn(icon, title, onclick) {
  const b = el("button", { class: "icon-btn", title, "aria-label": title, onclick: e => { e.stopPropagation(); onclick(e); } });
  b.innerHTML = ICONS[icon];
  return b;
}

function renderList(ul, entries, { canFill, mismatchAware } = {}) {
  ul.innerHTML = "";
  entries.forEach((e, i) => {
    const matches = matchScore(e.url, state.pageUrl) > 0;
    const showFill = canFill && getHost(state.pageUrl) && (!mismatchAware || matches);

    const acts = el("span", { class: "acts" },
      showFill ? el("button", { class: "fill-btn", onclick: ev => { ev.stopPropagation(); fillEntry(e, false); } }, "Ausfüllen") : null,
      iconBtn("user", "Benutzername kopieren", () => copy(e.username, "Benutzername")),
      iconBtn("key", "Passwort kopieren", () => copyPassword(e))
    );

    const details = el("div", { class: "details" }, el("div"));
    const li = el("li", { class: "entry" + (showFill && ul.id === "siteList" ? " always" : ""), style: `--i:${i}` },
      el("div", { class: "row", tabindex: "0", onclick: () => toggleDetails(li, e, details),
                  onkeydown: ev => { if (ev.key === "Enter") toggleDetails(li, e, details); } },
        el("span", { class: "avatar", style: `background:${avatarBg(e.title)}` }, initial(e.title)),
        el("span", { class: "meta" },
          el("b", {}, e.title, e.favorite ? el("span", { class: "star" }, "★") : null),
          el("small", {}, e.username || e.host || "—")),
        acts),
      details);
    ul.append(li);
  });
}

async function toggleDetails(li, entry, details) {
  const open = !li.classList.contains("open");
  li.parentElement.querySelectorAll(".entry.open").forEach(x => x !== li && x.classList.remove("open"));
  if (!open) return li.classList.remove("open");

  const inner = details.firstElementChild;
  if (!inner.childElementCount) {
    const full = await send({ type: "entry", id: entry.id });
    const pwSpan = el("span", { class: "v pw" }, "•".repeat(Math.min(Math.max(full.password.length, 8), 16)));
    let shown = false;
    inner.append(
      el("div", { class: "kv" },
        el("div", { style: "flex:1;min-width:0" }, el("div", { class: "k" }, "Benutzer"), el("div", { class: "v" }, full.username || "—")),
        iconBtn("user", "Kopieren", () => copy(full.username, "Benutzername"))),
      el("div", { class: "kv" },
        el("div", { style: "flex:1;min-width:0" }, el("div", { class: "k" }, "Passwort"), pwSpan),
        iconBtn("eye", "Anzeigen", () => {
          shown = !shown;
          if (shown) colorize(pwSpan, full.password);
          else pwSpan.textContent = "•".repeat(Math.min(Math.max(full.password.length, 8), 16));
        }),
        iconBtn("key", "Kopieren", () => copy(full.password, "Passwort"))),
      full.url ? el("div", { class: "kv" },
        el("div", { style: "flex:1;min-width:0" }, el("div", { class: "k" }, "Website"), el("div", { class: "v" }, full.url)),
        iconBtn("open", "Öffnen", () => openUrl(full.url))) : null
    );

    // Eintrag passt nicht zur Seite → bewusst "Trotzdem ausfüllen"
    if (getHost(state.pageUrl) && matchScore(full.url, state.pageUrl) === 0) {
      inner.append(el("div", { class: "warn-fill" },
        el("span", {}, "Gehört nicht zu dieser Seite"),
        el("button", { class: "btn ghost small", onclick: () => fillEntry(entry, true) }, "Trotzdem ausfüllen")));
    }
  }
  li.classList.add("open");
}

function colorize(target, password) {
  target.textContent = "";
  for (const ch of password) {
    const cls = /\d/.test(ch) ? "d" : /[\p{L}]/u.test(ch) ? "" : "s";
    target.append(cls ? el("span", { class: cls }, ch) : ch);
  }
}

async function fillEntry(entry, allowMismatch) {
  if (!state.tab?.id) return;
  try {
    const { filled } = await send({ type: "fill", id: entry.id, tabId: state.tab.id, allowMismatch });
    if (filled) {
      toast("Ausgefüllt");
      setTimeout(() => window.close(), 650);
    } else {
      toast("Kein Login-Formular gefunden", true);
    }
  } catch {
    toast("Ausfüllen fehlgeschlagen", true);
  }
}

async function copyPassword(entry) {
  try {
    const full = await send({ type: "entry", id: entry.id });
    await copy(full.password, "Passwort");
  } catch {
    toast("Kopieren fehlgeschlagen", true);
  }
}

async function copy(text, what) {
  if (!text) return toast(`${what} ist leer`, true);
  await navigator.clipboard.writeText(text);
  toast(`${what} kopiert`);
}

function openUrl(url) {
  const full = url.includes("://") ? url : "https://" + url;
  if (/^https?:\/\//i.test(full)) chrome.tabs.create({ url: full });
}

// ============================================================
// Tabs & Suche
// ============================================================

const TAB_ORDER = ["site", "all", "gen"];

function switchTab(name) {
  const prevIndex = TAB_ORDER.indexOf(state.currentTab);
  state.currentTab = name;
  const idx = TAB_ORDER.indexOf(name);
  if (idx >= 0) {
    $("#tabPill").style.transform = `translateX(${idx * 100}%)`;
    $$(".tabs button").forEach(b => b.classList.toggle("on", b.dataset.tab === name));
  }
  $$(".panel").forEach(p => {
    const on = p.dataset.panel === name;
    const pIndex = TAB_ORDER.indexOf(p.dataset.panel);
    p.classList.toggle("left", !on && pIndex !== -1 && pIndex < Math.max(idx, prevIndex));
    p.classList.toggle("on", on);
  });
  if (name === "gen" && !state.gen.value) generate();
}

$$(".tabs button").forEach(b => b.addEventListener("click", () => switchTab(b.dataset.tab)));

$("#search").addEventListener("input", () => {
  if (state.currentTab !== "all") switchTab("all");
  renderAll();
});

// ============================================================
// Generator
// ============================================================

let scrambleTimer = null;

function generate() {
  const g = state.gen;
  g.value = generateLocal(g);
  const s = strength(g.value);
  $("#genBar").style.transform = `scaleX(${Math.max(4, s.score) / 100})`;
  $("#genLabel").textContent = s.label;
  $("#genBits").textContent = `≈ ${s.bits} Bit`;

  // Zeichen "rasten" nacheinander ein
  clearInterval(scrambleTimer);
  const out = $("#genPw");
  const noise = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%&*";
  let step = 0;
  scrambleTimer = setInterval(() => {
    step += Math.max(1, Math.ceil(g.value.length / 14));
    if (step >= g.value.length) {
      clearInterval(scrambleTimer);
      colorize(out, g.value);
      return;
    }
    out.textContent = g.value.slice(0, step) +
      [...g.value.slice(step)].map(() => noise[Math.floor(Math.random() * noise.length)]).join("");
  }, 28);
}

$("#genNewBtn").addEventListener("click", () => {
  const icon = $("#genSpin");
  icon.classList.remove("spin");
  void icon.getBoundingClientRect();
  icon.classList.add("spin");
  generate();
});
$("#genCopyBtn").addEventListener("click", () => copy(state.gen.value, "Passwort"));
$("#genInsertBtn").addEventListener("click", async () => {
  if (!state.tab?.id) return;
  try {
    await send({ type: "insertPassword", tabId: state.tab.id, password: state.gen.value });
    toast("In die Seite eingefügt");
  } catch {
    toast("Kein Eingabefeld gefunden", true);
  }
});
$("#genRange").addEventListener("input", e => {
  state.gen.length = Number(e.target.value);
  $("#genLen").textContent = e.target.value;
  generate();
});
$$("#genChips .chip").forEach(chip => chip.addEventListener("click", () => {
  const opt = chip.dataset.opt;
  const active = $$("#genChips .chip.on").length;
  if (chip.classList.contains("on") && active === 1) return shake(chip); // mindestens eine Gruppe
  chip.classList.toggle("on");
  state.gen[opt] = chip.classList.contains("on");
  generate();
}));

// ============================================================
// Neuer Login aus dem Popup
// ============================================================

$("#addHereBtn").addEventListener("click", () => {
  const host = getHost(state.pageUrl) || "";
  const name = host.split(".").slice(-2, -1)[0] || host;
  $("#addTitle").value = name ? name[0].toUpperCase() + name.slice(1) : "";
  $("#addUser").value = "";
  $("#addPw").value = generateLocal();
  try { $("#addUrl").value = new URL(state.pageUrl).origin; } catch { $("#addUrl").value = ""; }
  switchTab("add");
  setTimeout(() => $("#addUser").focus(), 300);
});
$("#addGenBtn").addEventListener("click", () => { $("#addPw").value = generateLocal(state.gen); });
$("#addCancelBtn").addEventListener("click", () => switchTab("site"));
$("#addForm").addEventListener("submit", async e => {
  e.preventDefault();
  const entry = {
    title: $("#addTitle").value.trim(),
    username: $("#addUser").value.trim(),
    password: $("#addPw").value,
    url: $("#addUrl").value.trim()
  };
  if (!entry.title) return shake($("#addTitle"));
  try {
    await send({ type: "create", entry });
    toast("Login gespeichert");
    await loadEntries();
    switchTab("site");
  } catch {
    toast("Speichern fehlgeschlagen", true);
  }
});

// ============================================================
// Einstellungen
// ============================================================

let viewBeforeSettings = "loading";

$("#settingsBtn").addEventListener("click", async () => {
  if (state.view === "settings") return $("#backBtn").click();
  viewBeforeSettings = state.view;
  $("#setConn").textContent = $("#stateLabel span").textContent + " · localhost:27832";
  const { neverSave } = await send({ type: "neverSaveList" });
  $("#neverText").textContent = neverSave.length
    ? `${neverSave.length} Seite(n): ${neverSave.slice(0, 3).join(", ")}${neverSave.length > 3 ? " …" : ""}`
    : "Keine Seiten ausgeschlossen.";
  $("#clearNeverBtn").disabled = neverSave.length === 0;
  showView("settings");
});
$("#backBtn").addEventListener("click", () => {
  state.view = null;
  showView(viewBeforeSettings === "settings" ? "loading" : viewBeforeSettings);
  refresh();
});
$("#unpairBtn").addEventListener("click", async () => {
  await send({ type: "unpair" });
  toast("Kopplung aufgehoben");
  state.view = null;
  refresh();
});
$("#clearNeverBtn").addEventListener("click", async () => {
  await send({ type: "clearNeverSave" });
  $("#neverText").textContent = "Keine Seiten ausgeschlossen.";
  $("#clearNeverBtn").disabled = true;
  toast("Liste geleert");
});

// ============================================================
// Kleinkram
// ============================================================

const TOAST_ICONS = {
  ok: '<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg>',
  err: '<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><circle cx="12" cy="12" r="9"/><path d="M12 7.5v5.5M12 16.5v.01"/></svg>'
};

let toastTimer = null;
function toast(text, isError = false) {
  const t = $("#toast");
  $("#toastText").textContent = text;
  $("#toastIcon").innerHTML = TOAST_ICONS[isError ? "err" : "ok"];
  t.classList.toggle("err", isError);
  // Ablaufleiste neu starten, auch wenn schon ein Toast sichtbar ist
  t.classList.remove("show");
  void t.offsetWidth;
  t.classList.add("show");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => t.classList.remove("show"), 2400);
}

// Eigene Einstellung zusätzlich zu prefers-reduced-motion; gilt auch für die Seiten-Oberfläche
function applyMotion(reduce) {
  document.documentElement.classList.toggle("reduce-motion", reduce);
  $("#reduceMotion").checked = reduce;
}
chrome.storage.local.get("reduceMotion").then(r => applyMotion(!!r.reduceMotion));
$("#reduceMotion").addEventListener("change", e => {
  applyMotion(e.target.checked);
  chrome.storage.local.set({ reduceMotion: e.target.checked });
});

function shake(node) {
  node.classList.remove("shake");
  void node.getBoundingClientRect();
  node.classList.add("shake");
}

refresh();
