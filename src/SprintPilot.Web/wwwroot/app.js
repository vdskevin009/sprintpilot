window.sprintPilot={
 pwaPrompt:null,
 dotnet:null,
 theme(value){document.documentElement.dataset.theme=value;},
 clearToken(){const e=document.getElementById('pat');if(e)e.value='';},
 focusSearch(){document.getElementById('search')?.focus();},
 orderDropSuccess(id){const row=document.querySelector(`[data-work-item-id="${id}"]`);if(!row)return;row.scrollIntoView({block:'nearest',inline:'nearest'});row.classList.remove('order-drop-success');void row.offsetWidth;row.classList.add('order-drop-success');setTimeout(()=>row.classList.remove('order-drop-success'),1200);},
 copy:async text=>{await navigator.clipboard.writeText(text);},
 isStandalone(){return window.matchMedia?.('(display-mode: standalone)').matches===true||window.navigator.standalone===true;},
 notifyPwa(){
  if(!this.dotnet)return;
  this.dotnet.invokeMethodAsync('PwaInstallStateChanged',!!this.pwaPrompt,this.isStandalone()).catch(()=>{});
 },
 installPwa:async function(){
  if(this.isStandalone())return 'installed';
  const prompt=this.pwaPrompt;
  if(!prompt)return 'unavailable';
  this.pwaPrompt=null;
  try{
   await prompt.prompt();
   const choice=await prompt.userChoice;
   this.notifyPwa();
   return choice?.outcome==='accepted'?'accepted':'dismissed';
  }catch{
   this.notifyPwa();
   return 'unavailable';
  }
 },
 init(dotnet){
  this.dispose();
  this.dotnet=dotnet;
  this.handler=e=>{const editable=e.target instanceof Element&&e.target.closest('input,textarea,select,[contenteditable=true]');let cmd='';if(e.ctrlKey&&!e.shiftKey&&e.key.toLowerCase()==='k')cmd='palette';else if(e.key==='Escape')cmd='escape';else if(!editable){if(e.key==='/')cmd='search';else if(e.altKey&&e.key==='ArrowLeft')cmd='previous';else if(e.altKey&&e.key==='ArrowRight')cmd='next';else if(e.key.toLowerCase()==='r'&&!e.ctrlKey&&!e.altKey&&!e.metaKey)cmd='refresh';else if(e.ctrlKey&&e.key.toLowerCase()==='a'&&document.getElementById('work-grid')?.contains(e.target))cmd='select';}if(cmd){e.preventDefault();dotnet.invokeMethodAsync('Shortcut',cmd);}};
  document.addEventListener('keydown',this.handler);
  this.notifyPwa();
 },
 dialog(){this.beforeDialog=document.activeElement;setTimeout(()=>document.querySelector('[role=dialog] button,[role=dialog] input')?.focus(),0);},
 restoreFocus(){this.beforeDialog?.focus();},
 dispose(){
  if(this.handler)document.removeEventListener('keydown',this.handler);
  this.handler=null;
  this.dotnet=null;
 }
};
window.addEventListener('beforeinstallprompt',e=>{e.preventDefault();window.sprintPilot.pwaPrompt=e;window.sprintPilot.notifyPwa();});
window.addEventListener('appinstalled',()=>{window.sprintPilot.pwaPrompt=null;window.sprintPilot.notifyPwa();});
if('serviceWorker'in navigator)navigator.serviceWorker.register('/sw.js').catch(()=>{});
document.addEventListener('keydown',e=>{if(e.key!=='Tab')return;const dialog=document.querySelector('[aria-modal=true]');if(!dialog)return;const focusable=[...dialog.querySelectorAll('button,input,select,textarea,a[href],[tabindex="0"]')].filter(x=>!x.disabled&&x.offsetParent!==null);if(!focusable.length)return;const first=focusable[0],last=focusable.at(-1);if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus();}else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus();}});
