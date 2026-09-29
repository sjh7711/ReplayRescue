const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const manifestPath = path.join(root, 'extension', 'manifest.json');
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
if (!manifest.key) {
  const { publicKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
  manifest.key = publicKey.export({ type: 'spki', format: 'der' }).toString('base64');
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
}
const hash = crypto.createHash('sha256').update(Buffer.from(manifest.key, 'base64')).digest('hex').slice(0, 32);
const id = [...hash].map(char => String.fromCharCode(97 + parseInt(char, 16))).join('');
fs.writeFileSync(path.join(root, 'extension-id.txt'), id + '\n');
fs.writeFileSync(path.join(root, 'native-host.json'), JSON.stringify({
  name: 'com.local.replayrescue', description: 'Local Replay Rescue domain bridge',
  path: path.join(root, 'ReplayRescue.exe'), type: 'stdio', allowed_origins: ['chrome-extension://' + id + '/']
}, null, 2) + '\n');
console.log('Extension ID: ' + id);
