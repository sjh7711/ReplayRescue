const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const host = spawn(path.join(root, 'ReplayRescue.exe'), ['--native-host'], { windowsHide: true });
let buffer = Buffer.alloc(0), done = false;
const deadline = setTimeout(() => { host.kill(); process.exitCode = 1; }, 6000);
host.stdout.on('data', data => {
  buffer = Buffer.concat([buffer, data]);
  if (done || buffer.length < 4 || buffer.length < 4 + buffer.readUInt32LE()) return;
  done = true;
  const message = JSON.parse(buffer.subarray(4, 4 + buffer.readUInt32LE()));
  const summary = { appConnected: message.appConnected, instantReplayEnabled: message.instantReplayEnabled, observedAt: message.observedAt, pollSeconds: message.pollSeconds };
  console.log(JSON.stringify(summary));
  fs.writeFileSync(path.join(root, 'data', 'bridge-inspection.json'), JSON.stringify(summary, null, 2));
  host.stdin.end();
});
host.on('close', code => { clearTimeout(deadline); if (code !== 0 || !done) process.exitCode = 1; });
