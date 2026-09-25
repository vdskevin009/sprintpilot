const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const code=fs.readFileSync('src/SprintPilot.Web/wwwroot/app.js','utf8');
function browser(saved,blocked=false){
 const storage=new Map(saved?[['sprintpilot-theme',saved]]:[]);
 const ctx={window:{addEventListener(){}},document:{documentElement:{dataset:{}},addEventListener(){}},navigator:{},
 localStorage:{getItem(k){if(blocked)throw Error('blocked');return storage.get(k)},setItem(k,v){if(blocked)throw Error('blocked');storage.set(k,v)}}};
 vm.runInNewContext(code,ctx);
 return {api:ctx.window.sprintPilot,storage,theme:()=>ctx.document.documentElement.dataset.theme};
}
let b=browser('avd');
assert.equal(b.theme(),'avd');
assert.equal(b.api.restoreTheme('dark',false),'avd','Migrate the last browser choice over legacy disk settings');
b=browser();
assert.equal(b.api.restoreTheme('avd',true),'avd','Disk theme survives empty browser storage/new port');
assert.equal(b.storage.get('sprintpilot-theme'),'avd');
b=browser('dark');
assert.equal(b.api.restoreTheme('avd',true),'avd','Durable choice wins over stale browser cache');
b=browser('avd');
assert.equal(b.api.restoreTheme('system',true),'system','Explicit System must remain System');
b=browser('avd',true);
assert.equal(b.api.restoreTheme('avd',true),'avd','Disk choice works when browser storage is blocked');
b=browser('invalid');
assert.equal(b.api.restoreTheme('light',false),'light','Ignore invalid browser themes');
console.log('PASS theme migration, restart/cache loss, stale cache, explicit System and blocked storage');
