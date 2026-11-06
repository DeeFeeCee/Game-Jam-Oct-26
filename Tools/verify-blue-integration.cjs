// Run from the repository root: node Tools/verify-blue-integration.cjs
const fs = require('fs');
const cp = require('child_process');
const crypto = require('crypto');
const baseline = 'd5694f569de90113f0a7bcd71d96e68bcf55c98e';
const artCommit = '1ab8f22556336cd34361920bc5e49403ad966dd7';
const git = (...args) => cp.execFileSync('git', ['-c', 'safe.directory=' + process.cwd().replaceAll('\\', '/'), ...args], {maxBuffer: 20 * 1024 * 1024});
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
const failures = [];
function sameAs(ref, path) {
  const expected = git('show', ref + ':' + path);
  const actual = fs.readFileSync(path);
  const pointer = expected.toString().match(/^version https:\/\/git-lfs.github.com\/spec\/v1\noid sha256:([a-f0-9]+)/);
  const same = pointer ? sha(actual) === pointer[1] : expected.toString().replaceAll('\r\n', '\n') === actual.toString().replaceAll('\r\n', '\n');
  if (!same) failures.push('Content changed: ' + path);
}
const tracked = git('ls-tree', '-r', '--name-only', baseline).toString().trim().split('\n');
const intentionalEdits = new Set(['Assets/Game/Editor/Levels/LevelAuthoring32x18.cs', 'Assets/Game/Runtime/UI/PauseController.cs', 'Assets/Game/Runtime/UI/GameOverController.cs']);
const preserved = tracked.filter(p => !intentionalEdits.has(p));
preserved.forEach(p => sameAs(baseline, p));
const imported = ['BlueEnter','BlueExit','BluePlatforms','BlueTerminal','BlueTerminalTextbox'].flatMap(n => ['Assets/Art/Levels/' + n + '.png','Assets/Art/Levels/' + n + '.png.meta']);
imported.push(...['TerminalBlink.controller','Terminal_Blinking.anim'].flatMap(n => ['Assets/Art/Levels/BlueAnims/' + n,'Assets/Art/Levels/BlueAnims/' + n + '.meta']));
imported.push('Assets/Art/Levels.meta', 'Assets/Art/Levels/BlueAnims.meta');
imported.forEach(p => sameAs(artCommit, p));
const guids = new Map(); let metaCount = 0;
function walk(dir) {
  for (const item of fs.readdirSync(dir, {withFileTypes:true})) {
    const p = dir + '/' + item.name;
    if (item.isDirectory()) walk(p);
    else if (p.endsWith('.meta')) {
      metaCount++; const match = fs.readFileSync(p, 'utf8').match(/^guid: ([a-f0-9]{32})$/m);
      if (!match) { failures.push('Invalid metadata: ' + p); continue; }
      if (guids.has(match[1])) failures.push('Duplicate GUID: ' + p + ' and ' + guids.get(match[1]));
      guids.set(match[1], p);
    } else if (!fs.existsSync(p + '.meta')) failures.push('Missing metadata: ' + p);
  }
}
walk('Assets');
for (const name of ['BlueBG','BlueLvlGround-newt']) if(fs.existsSync('Assets/Art/Levels/' + name + '.png')) failures.push('Excluded duplicate was imported: ' + name);
const report = {baseline, artCommit, preservedFiles:preserved.length, importedFiles:imported.length, metadataFiles:metaCount, failures};
fs.mkdirSync('Logs/BlueIntegration', {recursive:true}); fs.writeFileSync('Logs/BlueIntegration/preservation.json', JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2)); process.exitCode = failures.length ? 1 : 0;
