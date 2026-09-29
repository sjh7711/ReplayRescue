(function (root) {
  const messages = {
    connecting: ['Connecting to the desktop app', '데스크톱 앱 연결 중'],
    disconnected: ['Disconnected', '연결 종료'],
    connectionFailed: ['Connection failed: {0}', '연결 실패: {0}'],
    waiting: ['Waiting for the desktop app', '데스크톱 앱 응답 대기'],
    runApp: ['Start Replay Rescue on your desktop', '데스크톱 앱을 실행해 주세요'],
    protected: ['Protected site open · Recovery paused', '보호 사이트 열림 · 자동 복구 대기'],
    clear: ['No protected sites · Monitoring connected', '보호 사이트 없음 · 감시 연결 정상'],
    tabError: ['Could not check tabs: {0}', '탭 확인 오류: {0}'],
    error: ['{0}', '{0}'],
    active: ['On', '활성'], inactive: ['Off', '비활성'], unknown: ['Unknown', '확인 불가'],
    connected: ['Connected', '연결됨'], notConnected: ['Disconnected', '연결 안 됨'],
    left: ['Left · Instant Replay', '왼쪽 · 즉시 리플레이'],
    right: ['Right · Desktop app', '오른쪽 · 앱 연결'],
    retry: ['Check connection', '연결 다시 확인'],
    freshness: ['App checks every {0}s · Delivery may take up to 5s more', '앱 확인 간격 {0}초 · 상태 표시 전달은 최대 약 5초 추가'],
    legend: ['Green: on / connected. Red: off, unknown, or disconnected. The left light shows the Instant Replay setting. Check the desktop app for actual recording status.', '초록: 활성 / 연결됨 · 빨강: 비활성 또는 확인 불가 / 연결 안 됨. 왼쪽은 즉시 리플레이의 켜짐 설정입니다. 실제 게임 녹화 상태는 데스크톱 앱에서 확인하세요.']
  };
  function normalize(language) { return String(language).toLowerCase().startsWith('ko') ? 'ko' : 'en'; }
  function text(language, key, ...args) {
    const pair = messages[key] || messages.error;
    return pair[normalize(language) === 'ko' ? 1 : 0].replace(/\{(\d+)\}/g, (_, index) => String(args[Number(index)] ?? ''));
  }
  const api = { normalize, text };
  root.ReplayI18n = api;
  if (typeof module !== 'undefined') module.exports = api;
})(globalThis);
