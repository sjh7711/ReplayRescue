let language = ReplayI18n.normalize(chrome.i18n.getUILanguage());
function text(key, ...args) { return ReplayI18n.text(language, key, ...args); }
function localize() {
  document.documentElement.lang = language;
  for (const [id, key] of [['replay-label', 'left'], ['connection-label', 'right'], ['retry', 'retry'], ['legend', 'legend']]) {
    document.getElementById(id).textContent = text(key);
  }
}
async function update(type = 'state') {
  try {
    const state = await chrome.runtime.sendMessage({ type });
    if (state?.language) language = ReplayI18n.normalize(state.language);
    localize();
    document.querySelector('#status').textContent = state?.message || text('connecting');
    document.querySelector('#domains').textContent = (state?.blockedDomains || []).join(', ');
    document.querySelector('#replay-dot').classList.toggle('on', state?.replayEnabled === true);
    document.querySelector('#connection-dot').classList.toggle('on', state?.connected === true);
    document.querySelector('#replay-value').textContent = text(state?.replayEnabled === true ? 'active' : state?.replayEnabled === false ? 'inactive' : 'unknown');
    document.querySelector('#connection-value').textContent = text(state?.connected ? 'connected' : 'notConnected');
    document.querySelector('#freshness').textContent = state?.connected && state?.pollSeconds ? text('freshness', state.pollSeconds) : '';
  } catch (error) {
    document.querySelector('#status').textContent = String(error);
    document.querySelector('#replay-dot').classList.remove('on');
    document.querySelector('#connection-dot').classList.remove('on');
    document.querySelector('#replay-value').textContent = text('unknown');
    document.querySelector('#connection-value').textContent = text('notConnected');
    document.querySelector('#freshness').textContent = '';
  }
}
document.querySelector('#retry').addEventListener('click', () => update('retry'));
localize();update();
setInterval(update, 1000);
