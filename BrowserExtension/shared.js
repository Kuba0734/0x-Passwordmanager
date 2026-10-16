// Gemeinsame Hilfsfunktionen für Hintergrund-Skript und Popup.

const SECOND_LEVEL = new Set([
  "co.uk", "org.uk", "ac.uk", "gov.uk", "com.au", "net.au", "org.au",
  "co.at", "or.at", "co.nz", "co.jp", "com.br", "com.tr", "co.za", "com.mx"
]);

/** Hostname ohne "www." – oder null bei unbrauchbarer URL (wie UrlMatcher.GetHost in der App). */
export function getHost(url) {
  if (!url || typeof url !== "string") return null;
  let candidate = url.trim();
  if (!candidate.includes("://")) candidate = "https://" + candidate;
  try {
    const u = new URL(candidate);
    if (u.protocol !== "http:" && u.protocol !== "https:") return null;
    const host = u.hostname.toLowerCase();
    return host.startsWith("www.") ? host.slice(4) : host;
  } catch {
    return null;
  }
}

export function getBaseDomain(host) {
  if (!host) return null;
  if (/^[\d.]+$/.test(host) || host.includes(":") || !host.includes(".")) return host;
  const labels = host.split(".");
  if (labels.length <= 2) return host;
  const lastTwo = labels.slice(-2).join(".");
  return SECOND_LEVEL.has(lastTwo) ? labels.slice(-3).join(".") : lastTwo;
}

/** 0 = kein Treffer, 1 = gleiche Basisdomain, 2 = Subdomain, 3 = exakt. */
export function matchScore(entryUrl, pageUrl) {
  const a = getHost(entryUrl);
  const b = getHost(pageUrl);
  if (!a || !b) return 0;
  if (a === b) return 3;
  const base = getBaseDomain(a);
  if (!base || base !== getBaseDomain(b)) return 0;
  return b.endsWith("." + a) || a.endsWith("." + b) ? 2 : 1;
}

const LOWER = "abcdefghijkmnopqrstuvwxyz";
const UPPER = "ABCDEFGHJKLMNPQRSTUVWXYZ";
const DIGITS = "23456789";
const SYMBOLS = "!@#$%^&*()_+-=[]{}:,.?";

function randomIndex(max) {
  // Rejection Sampling gegen Modulo-Bias
  const limit = Math.floor(0x100000000 / max) * max;
  const buf = new Uint32Array(1);
  do { crypto.getRandomValues(buf); } while (buf[0] >= limit);
  return buf[0] % max;
}

/** Lokaler Fallback-Generator, falls die App nicht erreichbar ist. */
export function generateLocal({ length = 20, lower = true, upper = true, digits = true, symbols = true } = {}) {
  const groups = [];
  if (lower) groups.push(LOWER);
  if (upper) groups.push(UPPER);
  if (digits) groups.push(DIGITS);
  if (symbols) groups.push(SYMBOLS);
  if (groups.length === 0) groups.push(LOWER);
  length = Math.max(groups.length, Math.min(128, length));
  const pool = groups.join("");
  const chars = groups.map(g => g[randomIndex(g.length)]);
  while (chars.length < length) chars.push(pool[randomIndex(pool.length)]);
  for (let i = chars.length - 1; i > 0; i--) {
    const j = randomIndex(i + 1);
    [chars[i], chars[j]] = [chars[j], chars[i]];
  }
  return chars.join("");
}

/** Grobe Stärke-Einschätzung (wie PasswordStrength in der App). */
export function strength(password) {
  if (!password) return { score: 0, label: "Kein Passwort", level: 0, bits: 0 };
  let pool = 0;
  if (/[a-z]/.test(password)) pool += 26;
  if (/[A-Z]/.test(password)) pool += 26;
  if (/\d/.test(password)) pool += 10;
  if (/[^a-zA-Z0-9]/.test(password)) pool += 32;
  let effective = 0;
  for (let i = 0; i < password.length; i++) {
    const prev = password.charCodeAt(i - 1), cur = password.charCodeAt(i);
    if (i > 0 && cur === prev) effective += 0.3;
    else if (i > 0 && Math.abs(cur - prev) === 1) effective += 0.5;
    else effective += 1;
  }
  let bits = effective * Math.log2(Math.max(pool, 1));
  if (/(password|passwort|123456|qwertz|qwerty|admin|hallo)/i.test(password)) bits *= 0.4;
  const level = bits < 28 ? 1 : bits < 40 ? 2 : bits < 60 ? 3 : bits < 90 ? 4 : 5;
  const label = ["Kein Passwort", "Sehr schwach", "Schwach", "Mittel", "Stark", "Sehr stark"][level];
  return { score: Math.min(100, Math.round(bits)), label, level, bits: Math.round(bits) };
}
