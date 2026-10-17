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

  // Hervorhebung als Ring in der eigenen Oberfläche – animiert nur opacity
  async function flash(input) {
    const root = await ui();
    const r = input.getBoundingClientRect();
    const ring = document.createElement("div");
    ring.className = "flash";
    ring.style.cssText = `top:${r.top - 3}px;left:${r.left - 3}px;width:${r.width + 6}px;height:${r.height + 6}px`;
    root.appendChild(ring);
    setTimeout(() => ring.remove(), 900);
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

  // Tokens aus src/ui/tokens.css; die Oberfläche erscheint erst, wenn sie geladen sind
  let uiReady = null, host = null;

  function ui() {
    return uiReady ??= (async () => {
      const tokens = await fetch(chrome.runtime.getURL("src/ui/tokens.css")).then(r => r.text()).catch(() => "");
      loadFont();
      host = document.createElement("password-vault-ui");
      host.style.cssText = "all:initial;position:fixed;top:0;left:0;width:0;height:0;z-index:2147483647;";
      const root = host.attachShadow({ mode: "closed" });
      root.innerHTML = `<style>${tokens}${STYLES}</style>`;
      applyMotion();
      (document.body || document.documentElement).appendChild(host);
      return root;
    })();
  }

  // @font-face greift im Shadow-DOM nicht – daher eigener Name, damit die Seite unberührt bleibt
  function loadFont() {
    const face = new FontFace("PV Inter", `url(${chrome.runtime.getURL("src/ui/fonts/InterVariable.woff2")})`, { weight: "100 900" });
    face.load().then(f => document.fonts.add(f)).catch(() => {});
  }

  async function applyMotion() {
    const { reduceMotion } = await chrome.storage.local.get("reduceMotion").catch(() => ({}));
    host?.classList.toggle("reduce-motion", !!reduceMotion);
  }
  chrome.storage.onChanged.addListener((changes, area) => {
    if (area === "local" && "reduceMotion" in changes) applyMotion();
  });

  const LOGO = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="10" width="16" height="11" rx="3"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/><circle cx="12" cy="15.5" r="1.4" fill="currentColor" stroke="none"/></svg>`;
  const ICON_LOCK = `<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="4" y="10" width="16" height="11" rx="3"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/></svg>`;
  const ICON_SPARK = `<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3v4M12 17v4M3 12h4M17 12h4M6 6l2.5 2.5M15.5 15.5L18 18M6 18l2.5-2.5M15.5 8.5L18 6"/></svg>`;
  const ICON_CLOSE = `<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18"/></svg>`;

  const STYLES = `
    :host { all: initial; --font: "PV Inter", "Inter", system-ui, -apple-system, "Segoe UI", sans-serif; }
    * { box-sizing: border-box; font-family: var(--font); -webkit-font-smoothing: antialiased; }
    svg { display: block; flex: none; }

    .badge {
      position: fixed; width: 26px; height: 26px; border-radius: var(--r-sm); border: 0; padding: 0;
      display: grid; place-items: center; cursor: pointer;
      color: var(--on-accent); background: var(--accent);
      box-shadow: var(--shadow);
      animation: pop var(--dur) var(--ease) both;
      transition: transform var(--dur-fast) var(--ease), box-shadow var(--dur) var(--ease);
    }
    .badge:hover { transform: translateY(-2px); box-shadow: var(--glow), var(--shadow); }
    .badge:active { transform: scale(0.97); }

    /* Vorschlagsmenü wie ein Modal: Fade + Scale 0.95 -> 1 + leicht nach oben */
    .menu {
      position: fixed; width: 320px; max-height: 360px; overflow: auto;
      padding: var(--s-2); border-radius: var(--r-xl);
      color: var(--text); background: var(--glass-strong);
      backdrop-filter: var(--blur);
      border: 1px solid var(--border); box-shadow: var(--shadow-lg);
      transform-origin: top left;
      animation: modalIn var(--dur) var(--ease) both;
      scrollbar-width: thin; scrollbar-color: rgba(255,255,255,.15) transparent;
    }
    .menu.closing { animation: modalOut var(--dur-fast) var(--ease) both; }
    .head { display: flex; align-items: center; gap: var(--s-2); padding: 6px var(--s-2) var(--s-3); font-size: var(--fs-sm); color: var(--text-2); }
    .head .logo { width: 20px; height: 20px; border-radius: 6px; display: grid; place-items: center; color: var(--on-accent); background: var(--accent); }
    .head b { color: var(--text); font-weight: 600; }
    .item {
      position: relative; display: flex; align-items: center; gap: var(--s-3); width: 100%;
      padding: var(--s-2); border: 0; border-radius: var(--r-lg);
      background: transparent; color: inherit; text-align: left; cursor: pointer;
      animation: rise var(--dur-slow) var(--ease) both;
      transition: transform var(--dur-fast) var(--ease), box-shadow var(--dur) var(--ease);
    }
    .item::before {
      content: ""; position: absolute; inset: 0; border-radius: inherit; pointer-events: none;
      background: var(--surface-hover); opacity: 0; transition: opacity var(--dur-fast) var(--ease);
    }
    .item:hover, .item:focus-visible { outline: none; transform: translateY(-2px); box-shadow: var(--glow); }
    .item:hover::before, .item:focus-visible::before { opacity: 1; }
    .item:active { transform: scale(0.97); }
    .item > * { position: relative; }
    .avatar {
      width: 34px; height: 34px; border-radius: var(--r-md); flex: none;
      display: grid; place-items: center;
      font-weight: 600; font-size: var(--fs-lg); color: var(--text);
      border: 1px solid var(--border);
    }
    .meta { min-width: 0; display: flex; flex-direction: column; }
    .title { font-size: var(--fs-md); font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .sub { font-size: var(--fs-sm); color: var(--text-2); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .mono { font-family: var(--font-mono); letter-spacing: .3px; }
    .note { display: flex; gap: var(--s-2); padding: var(--s-3); font-size: var(--fs-sm); color: var(--text-2); line-height: 1.5; }
    .sep { height: 1px; background: var(--border); margin: 6px var(--s-1); }
    .gen .avatar { background: var(--accent); color: var(--on-accent); border-color: transparent; }

    /* Speichern-Angebot wie ein Toast: gleitet seitlich herein, Leiste zeigt die Restzeit */
    .banner {
      position: fixed; top: var(--s-4); right: var(--s-4); width: 360px; overflow: hidden;
      padding: var(--s-4); border-radius: var(--r-xl);
      color: var(--text); background: var(--glass-strong);
      backdrop-filter: var(--blur);
      border: 1px solid var(--border-strong); box-shadow: var(--shadow-lg);
      animation: slideIn var(--dur-slow) var(--ease) both;
    }
    .banner.closing { animation: slideOut var(--dur) var(--ease) both; }
    .banner .row { display: flex; gap: var(--s-3); align-items: center; }
    .banner .icon {
      width: 40px; height: 40px; border-radius: var(--r-lg); flex: none;
      display: grid; place-items: center; color: var(--on-accent); background: var(--accent);
    }
    .banner h3 { margin: 0; font-size: var(--fs-lg); font-weight: 600; letter-spacing: -0.01em; }
    .banner p { margin: 2px 0 0; font-size: var(--fs-sm); color: var(--text-2); }
    .banner .actions { display: flex; gap: var(--s-2); margin-top: var(--s-4); }
    .banner .timer {
      position: absolute; left: 0; right: 0; bottom: 0; height: 2px; background: var(--text-3);
      transform-origin: left center; animation: drain 30s linear both;
    }
    .btn {
      flex: 1; height: 36px; border-radius: var(--r-md); border: 1px solid var(--border); cursor: pointer;
      font-size: var(--fs-md); font-weight: 600;
      transition: transform var(--dur-fast) var(--ease), box-shadow var(--dur) var(--ease);
    }
    .btn:hover { transform: translateY(-2px); box-shadow: var(--glow); }
    .btn:active { transform: scale(0.97); }
    .btn.primary { color: var(--on-accent); background: var(--accent); border-color: transparent; }
    .btn.ghost { color: var(--text); background: var(--surface); }
    .btn.link { flex: none; background: transparent; border-color: transparent; color: var(--text-2); padding: 0 var(--s-2); }
    .btn.link:hover { color: var(--text); box-shadow: none; }
    .close {
      position: absolute; top: var(--s-2); right: var(--s-2); width: 28px; height: 28px; border-radius: var(--r-sm); border: 0;
      display: grid; place-items: center; background: transparent; color: var(--text-2); cursor: pointer;
      transition: transform var(--dur-fast) var(--ease);
    }
    .close:hover { color: var(--text); transform: translateY(-2px); }
    .close:active { transform: scale(0.97); }
    .done { display: flex; align-items: center; gap: var(--s-3); }
    .check {
      width: 40px; height: 40px; border-radius: 50%; flex: none; display: grid; place-items: center;
      color: var(--on-accent); background: var(--success);
      animation: pop var(--dur-slow) var(--ease) both;
    }

    .flash {
      position: fixed; pointer-events: none; border-radius: var(--r-md);
      border: 2px solid var(--accent); box-shadow: 0 0 18px rgba(255,255,255,.35);
      animation: flash 900ms var(--ease) both;
    }

    @keyframes pop { from { transform: scale(.95); opacity: 0 } to { transform: none; opacity: 1 } }
    @keyframes modalIn { from { transform: translateY(6px) scale(.95); opacity: 0 } to { transform: none; opacity: 1 } }
    @keyframes modalOut { to { transform: translateY(6px) scale(.95); opacity: 0 } }
    @keyframes rise { from { transform: translateY(8px); opacity: 0 } to { transform: none; opacity: 1 } }
    @keyframes slideIn { from { transform: translateX(calc(100% + 24px)); opacity: 0 } to { transform: none; opacity: 1 } }
    @keyframes slideOut { to { transform: translateX(calc(100% + 24px)); opacity: 0 } }
    @keyframes drain { from { transform: scaleX(1) } to { transform: scaleX(0) } }
    @keyframes flash { 0% { opacity: 0 } 20% { opacity: 1 } 100% { opacity: 0 } }
  `;

  // Monochrom: stabiler Grauton pro Titel (gleiche Formel wie im Popup)
  function avatarStyle(title) {
    let hash = 17;
    for (const ch of (title || "").toLowerCase()) hash = (Math.imul(hash, 31) + ch.charCodeAt(0)) | 0;
    return `background:hsl(0 0% ${10 + Math.abs(hash % 6) * 3}%)`;
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

  async function showBadge(input) {
    anchor = input;
    const root = await ui();
    if (anchor !== input) return;
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
    setTimeout(() => m.remove(), 160);
  }

  async function openMenu() {
    if (!anchor) return;
    closeMenu(true);
    const root = await ui();
    if (!anchor || menu) return;
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
      delay = Math.min(delay + 45, 450); // max. ~10 Elemente staffeln
    };

    if (!res || res.state === "offline") {
      body.push(`<div class="note">Die Password-Vault-App läuft nicht. Starte sie auf deinem PC, um Logins auszufüllen.</div>`);
    } else if (res.state === "unpaired" || res.state === "pairing") {
      body.push(`<div class="note">Die Erweiterung ist noch nicht mit der App gekoppelt. Klicke auf das Password-Vault-Symbol in der Symbolleiste.</div>`);
    } else if (res.state === "locked") {
      body.push(`<div class="note">${ICON_LOCK}<span>Der Tresor ist gesperrt. Entsperre ihn über das Password-Vault-Symbol in der Symbolleiste oder in der App.</span></div>`);
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
        item(`<span class="avatar">${ICON_SPARK}</span>
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

  async function showSaveBanner(p) {
    if (!isTop || !p) return;
    const root = await ui();
    banner?.remove();
    banner = document.createElement("div");
    banner.className = "banner";
    const isUpdate = p.kind === "update";
    banner.innerHTML = `
      <button class="close" title="Schließen" aria-label="Schließen">${ICON_CLOSE}</button>
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
      </div>
      <i class="timer"></i>`;
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
      el.innerHTML = `<div class="done"><div class="check"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12.5l4.5 4.5L19 7.5"/></svg></div>
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
    setTimeout(() => el.remove(), 230);
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
