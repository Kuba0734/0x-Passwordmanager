// Password Vault – Content-Skript
// Erkennt Login-Felder, zeigt Vorschläge direkt am Feld, füllt aus und bietet
// nach dem Anmelden das Speichern an. Die gesamte UI lebt in einem geschlossenen
// Shadow-DOM, damit die Webseite weder Styles noch Inhalte manipulieren kann.

(() => {
  if (window.__passwordVaultLoaded) return;
  window.__passwordVaultLoaded = true;

  const isTop = window === window.top;
  const alive = () => !!chrome.runtime?.id; // nach Erweiterungs-Update ungültig

  function send(msg) {
    if (!alive()) return Promise.resolve(null);
    return chrome.runtime.sendMessage(msg).catch(() => null);
  }

  // ==========================================================
  // Feld-Erkennung
  // ==========================================================

  const TEXTLIKE = new Set(["text", "email", "tel", ""]);

  function isVisible(el) {
    if (!el.isConnected || el.disabled || el.readOnly) return false;
    const r = el.getBoundingClientRect();
    if (r.width < 40 || r.height < 14) return false;
    const s = getComputedStyle(el);
    return s.visibility !== "hidden" && s.display !== "none" && parseFloat(s.opacity) > 0.05;
  }

  function passwordFields() {
    return [...document.querySelectorAll('input[type="password"]')].filter(isVisible);
  }

  function looksLikeUsername(input) {
    const hint = `${input.name} ${input.id} ${input.autocomplete} ${input.placeholder} ${input.getAttribute("aria-label") || ""}`.toLowerCase();
    return /user|login|mail|account|konto|benutzer|anmeld|kunden|nummer|phone|tel/.test(hint)
      || input.autocomplete === "username" || input.type === "email";
  }

  function textInputs(scope) {
    return [...scope.querySelectorAll("input")].filter(i =>
      TEXTLIKE.has((i.getAttribute("type") || "").toLowerCase()) && isVisible(i));
  }

  /** Benutzername-Feld zu einem Passwortfeld: letztes passendes Textfeld davor. */
  function usernameFor(pw) {
    const scope = pw.form || document;
    let candidate = null;
    for (const input of textInputs(scope)) {
      if (input.compareDocumentPosition(pw) & Node.DOCUMENT_POSITION_FOLLOWING) {
        if (!candidate || looksLikeUsername(input) || !looksLikeUsername(candidate)) candidate = input;
      }
    }
    return candidate;
  }

  /** Zweistufige Logins (z.B. Google): erst nur E-Mail-Feld ohne Passwort. */
  function standaloneUsernameField() {
    return textInputs(document).find(i =>
      i.autocomplete === "username" || i.type === "email"
      || /^(email|username|login|identifier)$/i.test(i.name || i.id)) || null;
  }

  function isNewPasswordField(pw) {
    if (pw.autocomplete === "new-password") return true;
    const scope = pw.form || document;
    const pws = [...scope.querySelectorAll('input[type="password"]')].filter(isVisible);
    if (pws.length >= 2) return true;
    const hint = `${pw.name} ${pw.id} ${pw.placeholder}`.toLowerCase();
    return /new|neu|confirm|wiederhol|repeat|register|signup/.test(hint);
  }

  function loginFields() {
    const pws = passwordFields();
    if (pws.length) {
      const pw = pws.find(p => !isNewPasswordField(p)) || pws[0];
      return { username: usernameFor(pw), password: pw, all: pws };
    }
    const user = standaloneUsernameField();
    return user ? { username: user, password: null, all: [] } : null;
  }

  // ==========================================================
  // Werte setzen (kompatibel mit React, Vue, Angular …)
  // ==========================================================

  const nativeSetter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value").set;

  function setValue(input, value) {
    if (!input) return;
    input.focus({ preventScroll: true });
    nativeSetter.call(input, value);
    input.dispatchEvent(new Event("input", { bubbles: true, composed: true }));
    input.dispatchEvent(new Event("change", { bubbles: true }));
    flash(input);
  }

  function flash(input) {
    const prevTransition = input.style.transition;
    const prevShadow = input.style.boxShadow;
    input.style.transition = "box-shadow .25s ease";
    input.style.boxShadow = "0 0 0 3px rgba(124,92,255,.55), 0 0 18px rgba(6,182,212,.45)";
    setTimeout(() => {
      input.style.boxShadow = prevShadow;
      setTimeout(() => { input.style.transition = prevTransition; }, 300);
    }, 650);
  }

  function fill(username, password) {
    const fields = loginFields();
    if (!fields) return false;
    if (fields.username && username) setValue(fields.username, username);
    if (fields.password && password) setValue(fields.password, password);
    else if (!fields.password && fields.username && username) setValue(fields.username, username);
    return true;
  }

  // ==========================================================
  // Shadow-DOM-Oberfläche
  // ==========================================================

  let host, shadow;

  function ui() {
    if (shadow) return shadow;
    host = document.createElement("password-vault-ui");
    host.style.cssText = "all:initial;position:fixed;top:0;left:0;width:0;height:0;z-index:2147483647;";
    shadow = host.attachShadow({ mode: "closed" });
    shadow.innerHTML = `<style>${STYLES}</style>`;
    (document.body || document.documentElement).appendChild(host);
    return shadow;
  }

  const LOGO = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="10" width="16" height="11" rx="3"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/><circle cx="12" cy="15.5" r="1.4" fill="currentColor" stroke="none"/></svg>`;

  const STYLES = `
    :host { all: initial; }
    * { box-sizing: border-box; font-family: "Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif; }
    .badge {
      position: fixed; width: 26px; height: 26px; border-radius: 9px; border: 0; padding: 0;
      display: grid; place-items: center; cursor: pointer; color: #fff;
      background: linear-gradient(135deg,#8B5CF6,#6366F1 55%,#06B6D4);
      box-shadow: 0 4px 14px rgba(99,102,241,.45);
      animation: pop .35s cubic-bezier(.34,1.56,.64,1);
      transition: transform .15s ease, box-shadow .15s ease;
    }
    .badge:hover { transform: scale(1.1); box-shadow: 0 6px 20px rgba(99,102,241,.6); }
    .badge:active { transform: scale(.92); }
    .menu {
      position: fixed; width: 320px; max-height: 360px; overflow: auto;
      background: rgba(24,24,32,.92); color: #f4f4f7;
      backdrop-filter: blur(22px) saturate(160%);
      border: 1px solid rgba(255,255,255,.1); border-radius: 16px;
      box-shadow: 0 18px 50px rgba(0,0,0,.45);
      padding: 8px; transform-origin: top left;
      animation: drop .28s cubic-bezier(.16,1,.3,1);
    }
    .menu.closing { animation: dropOut .16s ease forwards; }
    .head { display:flex; align-items:center; gap:8px; padding:6px 8px 10px; font-size:12px; color:#a1a1b5; }
    .head .logo { width:20px; height:20px; border-radius:7px; display:grid; place-items:center; color:#fff;
      background: linear-gradient(135deg,#8B5CF6,#06B6D4); }
    .head b { color:#f4f4f7; font-weight:600; }
    .item {
      display:flex; align-items:center; gap:10px; width:100%; padding:8px; border:0; border-radius:11px;
      background: transparent; color: inherit; text-align:left; cursor:pointer;
      opacity:0; animation: rise .35s cubic-bezier(.16,1,.3,1) forwards;
      transition: background .15s ease, transform .12s ease;
    }
    .item:hover, .item:focus-visible { background: rgba(255,255,255,.08); outline: none; }
    .item:active { transform: scale(.98); }
    .avatar { width:34px; height:34px; border-radius:11px; flex:none; display:grid; place-items:center;
      font-weight:700; font-size:15px; color:#fff; }
    .meta { min-width:0; display:flex; flex-direction:column; }
    .title { font-size:13.5px; font-weight:600; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .sub { font-size:12px; color:#a1a1b5; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .mono { font-family: "Cascadia Mono", Consolas, monospace; letter-spacing:.3px; }
    .note { padding: 10px; font-size: 12.5px; color:#c4c4d4; line-height:1.45; }
    .sep { height:1px; background: rgba(255,255,255,.08); margin:6px 4px; }
    .gen .avatar { background: linear-gradient(135deg,#22C55E,#06B6D4); }

    .banner {
      position: fixed; top: 16px; right: 16px; width: 360px; color: #f4f4f7;
      background: rgba(22,22,30,.94); backdrop-filter: blur(24px) saturate(160%);
      border: 1px solid rgba(255,255,255,.1); border-radius: 20px; padding: 16px;
      box-shadow: 0 24px 60px rgba(0,0,0,.5);
      animation: slideIn .5s cubic-bezier(.16,1,.3,1);
    }
    .banner.closing { animation: slideOut .3s ease forwards; }
    .banner .row { display:flex; gap:12px; align-items:center; }
    .banner .icon { width:42px; height:42px; border-radius:14px; display:grid; place-items:center; color:#fff;
      background: linear-gradient(135deg,#8B5CF6,#6366F1 55%,#06B6D4); flex:none;
      animation: pulse 2.4s ease-in-out infinite; }
    .banner h3 { margin:0; font-size:14.5px; font-weight:650; }
    .banner p { margin:2px 0 0; font-size:12.5px; color:#a1a1b5; }
    .banner .actions { display:flex; gap:8px; margin-top:14px; }
    .btn { flex:1; height:36px; border-radius:11px; border:0; cursor:pointer; font-size:13px; font-weight:600;
      transition: transform .12s ease, filter .15s ease, background .15s ease; }
    .btn:active { transform: scale(.96); }
    .btn.primary { color:#fff; background: linear-gradient(135deg,#8B5CF6,#6366F1 55%,#06B6D4); }
    .btn.primary:hover { filter: brightness(1.12); }
    .btn.ghost { color:#e4e4ec; background: rgba(255,255,255,.08); }
    .btn.ghost:hover { background: rgba(255,255,255,.14); }
    .btn.link { flex:none; background: transparent; color:#a1a1b5; padding: 0 8px; }
    .btn.link:hover { color:#f4f4f7; }
    .close { position:absolute; top:10px; right:10px; width:26px; height:26px; border-radius:8px; border:0;
      background: transparent; color:#a1a1b5; cursor:pointer; font-size:16px; }
    .close:hover { background: rgba(255,255,255,.08); color:#fff; }
    .done { display:flex; align-items:center; gap:12px; }
    .check { width:42px; height:42px; border-radius:50%; display:grid; place-items:center;
      background:#22C55E; color:#fff; animation: pop .45s cubic-bezier(.34,1.56,.64,1); }
    .check svg path { stroke-dasharray: 24; stroke-dashoffset: 24; animation: draw .45s .15s ease forwards; }

    @keyframes pop { from { transform: scale(.3); opacity:0 } to { transform: scale(1); opacity:1 } }
    @keyframes drop { from { transform: translateY(-6px) scale(.96); opacity:0 } to { transform:none; opacity:1 } }
    @keyframes dropOut { to { transform: translateY(-6px) scale(.97); opacity:0 } }
    @keyframes rise { from { transform: translateY(8px); opacity:0 } to { transform:none; opacity:1 } }
    @keyframes slideIn { from { transform: translateX(120%) scale(.96); opacity:0 } to { transform:none; opacity:1 } }
    @keyframes slideOut { to { transform: translateX(120%); opacity:0 } }
    @keyframes pulse { 0%,100% { box-shadow: 0 0 0 0 rgba(124,92,255,.45) } 50% { box-shadow: 0 0 0 8px rgba(124,92,255,0) } }
    @keyframes draw { to { stroke-dashoffset: 0 } }
    @media (prefers-reduced-motion: reduce) { * { animation: none !important; opacity: 1 !important; } }
  `;

  const AVATAR_GRADIENTS = [
    ["#8B5CF6", "#6366F1"], ["#06B6D4", "#3B82F6"], ["#EC4899", "#F43F5E"], ["#F59E0B", "#EF4444"],
    ["#22C55E", "#10B981"], ["#14B8A6", "#06B6D4"], ["#A855F7", "#EC4899"], ["#3B82F6", "#6366F1"]
  ];

  // Gleiche Farbe wie in der App (stabiler Hash über den Titel)
  function avatarStyle(title) {
    let hash = 17;
    for (const ch of (title || "").toLowerCase()) hash = (Math.imul(hash, 31) + ch.charCodeAt(0)) | 0;
    hash = Math.abs(hash % 100000);
    const [a, b] = AVATAR_GRADIENTS[hash % AVATAR_GRADIENTS.length];
    return `background:linear-gradient(135deg,${a},${b})`;
  }

  function initial(title) {
    const m = (title || "?").match(/[\p{L}\p{N}]/u);
    return (m ? m[0] : "?").toUpperCase();
  }

  function esc(text) {
    const d = document.createElement("div");
    d.textContent = text ?? "";
    return d.innerHTML;
  }

  // ==========================================================
  // Badge am Feld + Vorschlagsmenü
  // ==========================================================

  let badge = null, menu = null, anchor = null, rafPending = false;

  function placeAt(el, target, below) {
    const r = target.getBoundingClientRect();
    if (below) {
      const top = Math.min(r.bottom + 6, window.innerHeight - 20);
      el.style.top = `${top}px`;
      el.style.left = `${Math.max(8, Math.min(r.left, window.innerWidth - 330))}px`;
    } else {
      el.style.top = `${r.top + (r.height - 26) / 2}px`;
      el.style.left = `${r.right - 26 - 6}px`;
    }
  }

  function reposition() {
    if (rafPending) return;
    rafPending = true;
    requestAnimationFrame(() => {
      rafPending = false;
      if (!anchor || !anchor.isConnected) return hideAll();
      if (badge) placeAt(badge, anchor, false);
      if (menu) placeAt(menu, anchor, true);
    });
  }

  function showBadge(input) {
    anchor = input;
    const root = ui();
    if (!badge) {
      badge = document.createElement("button");
      badge.className = "badge";
      badge.title = "Password Vault";
      badge.innerHTML = LOGO;
      badge.addEventListener("mousedown", e => e.preventDefault()); // Fokus im Feld behalten
      badge.addEventListener("click", () => (menu ? closeMenu() : openMenu()));
      root.appendChild(badge);
    }
    placeAt(badge, input, false);
  }

  function hideAll() {
    closeMenu(true);
    badge?.remove();
    badge = null;
    anchor = null;
  }

  function closeMenu(immediate) {
    if (!menu) return;
    const m = menu;
    menu = null;
    if (immediate) return m.remove();
    m.classList.add("closing");
    setTimeout(() => m.remove(), 170);
  }

  async function openMenu() {
    if (!anchor) return;
    closeMenu(true);
    const root = ui();
    menu = document.createElement("div");
    menu.className = "menu";
    menu.innerHTML = `<div class="head"><span class="logo">${LOGO}</span><span>Password Vault · <b>${esc(location.hostname.replace(/^www\./, ""))}</b></span></div>
                      <div class="note">Lade …</div>`;
    root.appendChild(menu);
    placeAt(menu, anchor, true);
    const current = menu;

    const res = await send({ type: "frameMatches" });
    if (menu !== current) return;

    const body = [];
    let delay = 0;
    const item = (html, attrs = "", cls = "item") => {
      body.push(`<button class="${cls}" ${attrs} style="animation-delay:${delay}ms">${html}</button>`);
      delay += 40;
    };

    if (!res || res.state === "offline") {
      body.push(`<div class="note">Die Password-Vault-App läuft nicht. Starte sie auf deinem PC, um Logins auszufüllen.</div>`);
    } else if (res.state === "unpaired" || res.state === "pairing") {
      body.push(`<div class="note">Die Erweiterung ist noch nicht mit der App gekoppelt. Klicke auf das Password-Vault-Symbol in der Symbolleiste.</div>`);
    } else if (res.state === "locked") {
      body.push(`<div class="note">🔒 Der Tresor ist gesperrt. Entsperre ihn über das Password-Vault-Symbol in der Symbolleiste oder in der App.</div>`);
    } else {
      for (const e of res.entries) {
        item(`<span class="avatar" style="${avatarStyle(e.title)}">${esc(initial(e.title))}</span>
              <span class="meta"><span class="title">${esc(e.title)}</span><span class="sub">${esc(e.username || "ohne Benutzername")}</span></span>`,
          `data-id="${esc(e.id)}"`);
      }
      if (res.entries.length === 0)
        body.push(`<div class="note">Keine gespeicherten Logins für diese Seite. Nach dem Anmelden kannst du sie speichern.</div>`);
    }

    // Passwortvorschlag für Registrierungs- / Passwort-ändern-Formulare
    if (anchor?.type === "password" && isNewPasswordField(anchor)) {
      const gen = await send({ type: "generate" });
      if (gen?.password && menu === current) {
        if (body.length) body.push(`<div class="sep"></div>`);
        item(`<span class="avatar">✨</span>
              <span class="meta"><span class="title">Sicheres Passwort verwenden</span><span class="sub mono">${esc(gen.password)}</span></span>`,
          `data-generated="${esc(gen.password)}"`, "item gen");
      }
    }

    if (menu !== current) return;
    current.innerHTML = current.firstElementChild.outerHTML + body.join("");
    current.querySelectorAll(".item").forEach(btn => {
      btn.addEventListener("mousedown", e => e.preventDefault());
      btn.addEventListener("click", () => onPick(btn));
    });
  }

  async function onPick(btn) {
    if (btn.dataset.generated) {
      const pw = btn.dataset.generated;
      const scope = anchor?.form || document;
      [...scope.querySelectorAll('input[type="password"]')].filter(isVisible).forEach(i => setValue(i, pw));
      closeMenu();
      return;
    }
    const creds = await send({ type: "fillFromFrame", id: btn.dataset.id });
    if (creds && !creds.error) fill(creds.username, creds.password);
    closeMenu();
  }

  // ---------- Fokus-Überwachung ----------

  document.addEventListener("focusin", e => {
    const t = e.target;
    if (!(t instanceof HTMLInputElement) || !alive()) return;
    const fields = loginFields();
    const relevant = fields && (t === fields.username || t === fields.password || fields.all.includes(t));
    if (!relevant) return;
    showBadge(t);
    // Automatisch öffnen, wenn das Feld leer ist
    if (!t.value) openMenu();
  }, true);

  document.addEventListener("focusout", e => {
    setTimeout(() => {
      const active = document.activeElement;
      if (active !== anchor && active !== host) hideAll();
    }, 150);
  }, true);

  document.addEventListener("keydown", e => {
    if (e.key === "Escape" && menu) closeMenu();
    if (e.key === "ArrowDown" && menu && e.target === anchor) {
      e.preventDefault();
      menu.querySelector(".item")?.focus();
    }
  }, true);

  window.addEventListener("scroll", reposition, true);
  window.addEventListener("resize", reposition);

  // ==========================================================
  // Mitschneiden beim Anmelden → Speichern anbieten
  // ==========================================================

  let lastCapture = "";

  function capture(scopeHint) {
    const scope = scopeHint?.form || scopeHint?.closest?.("form") || document;
    const pws = [...scope.querySelectorAll('input[type="password"]')].filter(i => i.value);
    if (pws.length === 0) return;
    // Bei "Passwort ändern": das neue (letzte) Passwort nehmen
    const pw = pws.length > 1 ? pws[pws.length - 1] : pws[0];
    const user = usernameFor(pws[0]) || standaloneUsernameField();
    const username = user?.value?.trim() || rememberedUsername || "";
    const key = username + "\u0000" + pw.value;
    if (key === lastCapture) return;
    lastCapture = key;
    send({ type: "captured", username, password: pw.value });
  }

  // Zweistufige Logins: Benutzername aus Schritt 1 merken
  let rememberedUsername = "";
  document.addEventListener("change", e => {
    const t = e.target;
    if (t instanceof HTMLInputElement && TEXTLIKE.has(t.type) && looksLikeUsername(t) && t.value)
      rememberedUsername = t.value.trim();
  }, true);

  document.addEventListener("submit", e => capture(e.target), true);

  document.addEventListener("click", e => {
    const btn = e.target instanceof Element && e.target.closest('button, input[type="submit"], [role="button"]');
    if (!btn) return;
    const text = `${btn.textContent} ${btn.value || ""} ${btn.id} ${btn.getAttribute("aria-label") || ""}`.toLowerCase();
    if (btn.type === "submit" || /log ?in|sign ?in|anmeld|einlogg|weiter|continue|next|register|registr|speichern|save|ändern/.test(text))
      capture(btn);
  }, true);

  document.addEventListener("keydown", e => {
    if (e.key === "Enter" && e.target instanceof HTMLInputElement && e.target.type === "password")
      capture(e.target);
  }, true);

  // ==========================================================
  // Speichern-Banner (nur im Haupt-Frame)
  // ==========================================================

  let banner = null;

  function showSaveBanner(p) {
    if (!isTop || !p) return;
    banner?.remove();
    const root = ui();
    banner = document.createElement("div");
    banner.className = "banner";
    const isUpdate = p.kind === "update";
    banner.innerHTML = `
      <button class="close" title="Schließen">✕</button>
      <div class="row">
        <div class="icon">${LOGO.replace('width="14" height="14"', 'width="20" height="20"')}</div>
        <div>
          <h3>${isUpdate ? "Passwort aktualisieren?" : "Login speichern?"}</h3>
          <p>${esc(isUpdate ? p.title : p.host)}${p.username ? " · " + esc(p.username) : ""}</p>
        </div>
      </div>
      <div class="actions">
        <button class="btn primary" data-a="save">${isUpdate ? "Aktualisieren" : "Speichern"}</button>
        <button class="btn ghost" data-a="dismiss">Nicht jetzt</button>
        ${isUpdate ? "" : `<button class="btn link" data-a="never">Nie</button>`}
      </div>`;
    root.appendChild(banner);

    const current = banner;
    current.querySelector(".close").addEventListener("click", () => decide(current, "dismiss"));
    current.querySelectorAll("[data-a]").forEach(b =>
      b.addEventListener("click", () => decide(current, b.dataset.a)));

    // Nach 30 s automatisch ausblenden (Angebot bleibt bis zum Seitenwechsel bestehen)
    setTimeout(() => closeBanner(current), 30_000);
  }

  async function decide(el, action) {
    const res = await send({ type: "resolvePendingSave", action });
    if (action === "save" && res?.ok) {
      el.innerHTML = `<div class="done"><div class="check"><svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg></div>
                      <div><h3>Gespeichert</h3><p>Sicher in deinem Tresor abgelegt.</p></div></div>`;
      setTimeout(() => closeBanner(el), 1800);
    } else if (action === "save") {
      el.querySelector("p").textContent = "Speichern fehlgeschlagen – ist der Tresor entsperrt?";
    } else {
      closeBanner(el);
    }
  }

  function closeBanner(el) {
    if (!el?.isConnected) return;
    el.classList.add("closing");
    setTimeout(() => el.remove(), 320);
    if (banner === el) banner = null;
  }

  // ==========================================================
  // Nachrichten vom Hintergrund-Skript
  // ==========================================================

  chrome.runtime.onMessage.addListener((msg, _sender, sendResponse) => {
    switch (msg.type) {
      case "fill":
        sendResponse({ filled: fill(msg.username, msg.password) });
        break;
      case "insertPassword": {
        const target = document.activeElement instanceof HTMLInputElement ? document.activeElement : null;
        const scope = target?.form || document;
        const pws = [...scope.querySelectorAll('input[type="password"]')].filter(isVisible);
        if (target && !pws.includes(target)) setValue(target, msg.password);
        pws.forEach(i => setValue(i, msg.password));
        sendResponse({ ok: true });
        break;
      }
      case "showSaveBanner":
        showSaveBanner(msg.pending);
        sendResponse({ ok: true });
        break;
    }
  });

  // ==========================================================
  // Start: Seite melden, ggf. Speichern-Angebot von vorheriger Seite zeigen
  // ==========================================================

  let reported = null;

  function report() {
    const hasLogin = !!loginFields();
    if (hasLogin === reported) return;
    reported = hasLogin;
    send({ type: "pageInfo", hasLogin });
  }

  report();
  if (isTop) send({ type: "getPendingSave" }).then(p => p && showSaveBanner(p));

  // Formulare, die erst später erscheinen (SPAs, Modals)
  let moTimer = null;
  new MutationObserver(() => {
    clearTimeout(moTimer);
    moTimer = setTimeout(report, 400);
  }).observe(document.documentElement, { childList: true, subtree: true });
})();
