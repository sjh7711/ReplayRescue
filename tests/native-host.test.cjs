const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const child = spawn(path.join(root, 'ReplayRescue.exe'), ['--native-host'], {windowsHide:true,stdio:['pipe','pipe','pipe']});
let buffer=Buffer.alloc(0), handled=false;
const timeout=setTimeout(()=>{child.kill();throw new Error('Native host timed out');},10000);
child.stdout.on('data', data=>{
  buffer=Buffer.concat([buffer,data]);
  if(buffer.length<4 || buffer.length<4+buffer.readUInt32LE(0) || handled)return;
  handled=true;
  const policy=JSON.parse(buffer.subarray(4,4+buffer.readUInt32LE(0)));
  assert.equal(policy.type,'policy');assert.ok(policy.domains.includes('netflix.com'));
  assert.ok(['en','ko'].includes(policy.language), 'Native bridge provides the saved application language');
  const body=Buffer.from(JSON.stringify({type:'report',version:policy.version,blockedDomains:['netflix.com','unconfigured.example'],windowCount:1,incognitoAccess:false}));
  const prefix=Buffer.alloc(4);prefix.writeUInt32LE(body.length);
  child.stdin.write(prefix.subarray(0,2));
  setTimeout(()=>{child.stdin.write(Buffer.concat([prefix.subarray(2),body]));},50);
  setTimeout(()=>{
    const file=path.join(root,'data',`browser-${child.pid}.json`);
    const report=JSON.parse(fs.readFileSync(file,'utf8'));
    assert.equal(report.Ready,true);
    assert.deepEqual(report.BlockedDomains,['netflix.com']);
    assert.equal(report.WindowCount,1);
    child.stdin.end();
  },350);
});
child.on('close', code=>{
  clearTimeout(timeout);assert.equal(code,0);assert.ok(handled);
  assert.equal(fs.existsSync(path.join(root,'data',`browser-${child.pid}.json`)),false);
  console.log('Native EXE framing, fragmented input, report validation, and disconnect cleanup passed.');
});
