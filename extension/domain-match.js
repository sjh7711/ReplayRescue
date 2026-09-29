/* No page content, history, cookies, or complete URLs leave this extension. */
function findBlockedDomains(tabs, domains) {
  const matches = new Set();
  for (const tab of tabs) {
    for (const value of [tab.url, tab.pendingUrl]) {
      if (!value) continue;
      try {
        const url = new URL(value);
        if (!['https:', 'http:'].includes(url.protocol)) continue;
        const host = url.hostname.toLowerCase().replace(/\.$/, '');
        for (const domain of domains) {
          if (host === domain || host.endsWith('.' + domain)) matches.add(domain);
        }
      } catch { /* Blank and internal tabs are not websites. */ }
    }
  }
  return [...matches].sort();
}
if (typeof module !== 'undefined') module.exports = { findBlockedDomains };
