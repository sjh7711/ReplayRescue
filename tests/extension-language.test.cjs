const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../extension');
const listeners = {};
const titles = [], reports = [];
let tabs = [];
const event = name => ({ addListener(fn) { listeners[name] = fn; } });
const port = { onMessage: event('policy'), onDisconnect: event('disconnect'), postMessage(value) { reports.push(value); } };
const context = vm.createContext({ URL, console, setTimeout() {}, clearTimeout() {}, setInterval() {},
  OffscreenCanvas: class { getContext() { return { beginPath() {}, arc() {}, fill() {}, stroke() {}, getImageData() { return {}; } }; } },
  chrome: {
    i18n: { getUILanguage: () => 'ko-KR' },
    runtime: { id: 'test-extension', connectNative: () => port, getManifest: () => ({ version: '1.3.0' }),
      onMessage: event('request'), onStartup: event('startup'), onInstalled: event('installed') },
    action: { setBadgeText() {}, setIcon() {}, setTitle(value) { titles.push(value.title); } },
    tabs: { query: async () => tabs, onCreated: event('tabCreate'), onRemoved: event('tabRemove'), onUpdated: event('tabUpdate'), onReplaced: event('tabReplace') },
    windows: { getAll: async () => [{}], onRemoved: event('windowRemove'), onCreated: event('windowCreate') },
    extension: { isAllowedIncognitoAccess: async () => false },
    alarms: { create() {}, onAlarm: event('alarm') }
  }
});
context.importScripts = (...names) => names.forEach(name => vm.runInContext(fs.readFileSync(path.join(root, name), 'utf8'), context, { filename: name }));
context.importScripts('background.js');
const flush = () => new Promise(resolve => setImmediate(resolve));
function state() { let result; listeners.request({ type: 'state' }, { id: 'test-extension' }, value => { result = value; }); return result; }
async function send(language) {
  listeners.policy({ type: 'policy', domains: ['netflix.com'], version: 'netflix.com', language, appConnected: true, instantReplayEnabled: true, pollSeconds: 15 });
  await flush();
}
(async () => {
  assert.equal(state().language, 'ko');
  await send('en');
  assert.equal(state().language, 'en');
  assert.equal(state().message, 'No protected sites · Monitoring connected');
  assert.match(titles.at(-1), /Left · Instant Replay: On/);
  tabs = [{ url: 'https://www.netflix.com/browse' }];
  await send('ko');
  assert.match(state().message, /보호 사이트 열림/);
  assert.deepEqual(Array.from(reports.at(-1).blockedDomains), ['netflix.com']);
  await send('en');
  assert.equal(state().message, 'Protected site open · Recovery paused');
  assert.deepEqual(Array.from(reports.at(-1).blockedDomains), ['netflix.com']);
  listeners.disconnect();
  assert.equal(state().language, 'en');
  assert.equal(state().connected, false);
  assert.equal(state().replayEnabled, null);
  assert.match(state().message, /^Connection failed:/);

  const elements = new Map();
  const element = id => { if (!elements.has(id)) elements.set(id, { textContent: '', classList: { toggle() {}, remove() {} }, addEventListener() {} }); return elements.get(id); };
  let popupState = { language: 'en', connected: true, replayEnabled: true, pollSeconds: 15, message: 'Connected', blockedDomains: [] };
  const popup = vm.createContext({ setInterval() {}, document: { documentElement: {}, getElementById: element, querySelector: selector => element(selector.slice(1)) },
    chrome: { i18n: { getUILanguage: () => 'ko' }, runtime: { sendMessage: async () => popupState } } });
  vm.runInContext(fs.readFileSync(path.join(root, 'i18n.js'), 'utf8'), popup);
  vm.runInContext(fs.readFileSync(path.join(root, 'popup.js'), 'utf8'), popup);
  await flush();
  assert.equal(popup.document.documentElement.lang, 'en');
  assert.equal(element('retry').textContent, 'Check connection');
  assert.equal(element('replay-value').textContent, 'On');
  popupState = { ...popupState, language: 'ko', replayEnabled: false };
  await vm.runInContext('update()', popup);
  assert.equal(element('retry').textContent, '연결 다시 확인');
  assert.equal(element('replay-value').textContent, '비활성');
  console.log('Extension language handoff, protected-tab reporting, disconnect handling, and popup switching passed.');
})().catch(error => { console.error(error); process.exitCode = 1; });
