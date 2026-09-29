importScripts('i18n.js', 'domain-match.js', 'status-icon.js');
let port = null;
let policy = null;
let reporting = false;
let dirty = false;
let retryTimer;
let state = { language: ReplayI18n.normalize(chrome.i18n.getUILanguage()), connected: false, replayEnabled: null, blockedDomains: [], messageKey: 'connecting' };
let lastNativeMessageAt = 0;
function text(key, ...args) { return ReplayI18n.text(state.language, key, ...args); }
function snapshot() { return { ...state, message: text(state.messageKey, state.messageArg) }; }

function connect() {
  if (port) return;
  clearTimeout(retryTimer);
  try {
    const current = chrome.runtime.connectNative('com.local.replayrescue');
    port = current;
    current.onMessage.addListener(message => {
      if (message.type !== 'policy' || !Array.isArray(message.domains)) return;
      policy = message;
      lastNativeMessageAt = Date.now();
      state.connected = message.appConnected === true;
      state.replayEnabled = state.connected && typeof message.instantReplayEnabled === 'boolean' ? message.instantReplayEnabled : null;
      state.observedAt = message.observedAt;
      state.pollSeconds = message.pollSeconds;
      if (message.language) state.language = ReplayI18n.normalize(message.language);
      renderIndicators();
      report();
    });
    current.onDisconnect.addListener(() => {
      const reason = chrome.runtime.lastError?.message || text('disconnected');
      if (port !== current) return;
      port = null;
      policy = null;
      state = { ...state, connected: false, replayEnabled: null, blockedDomains: [], messageKey: 'connectionFailed', messageArg: reason };
      renderIndicators();
      retryTimer = setTimeout(connect, 10000);
    });
  } catch (error) {
    state.messageKey = 'error'; state.messageArg = String(error);
    state.connected = false;
    state.replayEnabled = null;
    renderIndicators();
    port = null;
    retryTimer = setTimeout(connect, 10000);
  }
}

async function renderIndicators() {
  const title = 'Replay Rescue\n' + text('left') + ': ' + replayLabel(state.replayEnabled, state.language)
    + '\n' + text('right') + ': ' + text(state.connected ? 'connected' : 'notConnected');
  try {
    await Promise.all([
      chrome.action.setBadgeText({ text: '' }),
      chrome.action.setIcon({ imageData: {
        16: makeStatusIcon(state.replayEnabled, state.connected, 16),
        32: makeStatusIcon(state.replayEnabled, state.connected, 32)
      } }),
      chrome.action.setTitle({ title })
    ]);
  } catch { /* An extension reload can invalidate an outstanding UI update. */ }
}

function expireConnection() {
  if (port && Date.now() - lastNativeMessageAt > 15000) {
    state.connected = false;
    state.replayEnabled = null;
    state.messageKey = 'waiting';
    renderIndicators();
  }
}

async function report() {
  if (!port || !policy) { connect(); return; }
  if (reporting) { dirty = true; return; }
  reporting = true;
  const currentPort = port;
  const currentPolicy = policy;
  try {
    const [tabs, windows, incognitoAccess] = await Promise.all([
      chrome.tabs.query({}),
      chrome.windows.getAll({ windowTypes: ['normal', 'popup'] }),
      chrome.extension.isAllowedIncognitoAccess()
    ]);
    if (currentPort !== port || currentPolicy !== policy) { dirty = true; return; }
    const blockedDomains = findBlockedDomains(tabs, currentPolicy.domains);
    currentPort.postMessage({ type: 'report', version: currentPolicy.version, blockedDomains,
      windowCount: windows.length, incognitoAccess, extensionVersion: chrome.runtime.getManifest().version });
    state = { ...state, blockedDomains, incognitoAccess,
      messageKey: !state.connected ? 'runApp' : blockedDomains.length ? 'protected' : 'clear' };
  } catch (error) {
    state.messageKey = 'tabError'; state.messageArg = String(error);
  } finally {
    reporting = false;
    if (dirty) { dirty = false; setTimeout(report, 50); }
  }
}

chrome.tabs.onCreated.addListener(report);
chrome.tabs.onRemoved.addListener(report);
chrome.tabs.onUpdated.addListener(report);
chrome.tabs.onReplaced.addListener(report);
chrome.windows.onRemoved.addListener(report);
chrome.windows.onCreated.addListener(report);
chrome.alarms.onAlarm.addListener(() => { expireConnection(); connect(); report(); });
chrome.runtime.onStartup.addListener(connect);
chrome.runtime.onInstalled.addListener(() => { chrome.alarms.create('reconnect', { periodInMinutes: 0.5 }); connect(); });
chrome.runtime.onMessage.addListener((message, sender, reply) => {
  if (sender.id !== chrome.runtime.id) return;
  if (message.type === 'state') { expireConnection(); reply(snapshot()); }
  if (message.type === 'retry') { connect(); report(); reply(snapshot()); }
});
chrome.alarms.create('reconnect', { periodInMinutes: 0.5 });
setInterval(expireConnection, 5000);
renderIndicators();
connect();
