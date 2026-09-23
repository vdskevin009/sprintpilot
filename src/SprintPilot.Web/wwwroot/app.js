const sprintPilotThemeKey='sprintpilot-theme';
try{const savedTheme=localStorage.getItem(sprintPilotThemeKey);if(savedTheme)document.documentElement.dataset.theme=savedTheme;}catch{}
window.sprintPilot={
 pwaPrompt:null,
 dotnet:null,
 theme(value,persist=true){
  let effective=value||'system';
  try{const saved=localStorage.getItem(sprintPilotThemeKey);if(!persist&&effective==='system'&&saved)effective=saved;if(persist)localStorage.setItem(sprintPilotThemeKey,effective);}catch{}
  document.documentElement.dataset.theme=effective;
  return effective;
 },
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
  this.dragScrollHandler=e=>this.updateDragAutoScroll(e);
  this.dragScrollStop=()=>this.stopDragAutoScroll();
  document.addEventListener('dragover',this.dragScrollHandler);
  document.addEventListener('drop',this.dragScrollStop);
  document.addEventListener('dragend',this.dragScrollStop);
  this.notifyPwa();
 },
 updateDragAutoScroll(e){
  const target=e.target instanceof Element?e.target:null;
  const container=target?.closest('.grid-wrap,.daily-focus-list,.person-column');
  if(!container||container.scrollHeight<=container.clientHeight){this.stopDragAutoScroll();return;}
  const rect=container.getBoundingClientRect(),edge=Math.min(78,Math.max(42,rect.height*.16));
  let velocity=0;
  if(e.clientY<rect.top+edge)velocity=-Math.max(4,Math.round(22*(rect.top+edge-e.clientY)/edge));
  else if(e.clientY>rect.bottom-edge)velocity=Math.max(4,Math.round(22*(e.clientY-(rect.bottom-edge))/edge));
  this.dragScrollContainer=container;
  this.dragScrollVelocity=velocity;
  if(!velocity){if(this.dragScrollFrame)cancelAnimationFrame(this.dragScrollFrame);this.dragScrollFrame=null;return;}
  if(this.dragScrollFrame)return;
  const tick=()=>{
   const c=this.dragScrollContainer,v=this.dragScrollVelocity;
   if(!c||!v){this.dragScrollFrame=null;return;}
   c.scrollTop+=v;
   this.dragScrollFrame=requestAnimationFrame(tick);
  };
  this.dragScrollFrame=requestAnimationFrame(tick);
 },
 stopDragAutoScroll(){
  this.dragScrollVelocity=0;this.dragScrollContainer=null;
  if(this.dragScrollFrame)cancelAnimationFrame(this.dragScrollFrame);
  this.dragScrollFrame=null;
 },
 dialog(){this.beforeDialog=document.activeElement;setTimeout(()=>document.querySelector('[role=dialog] button,[role=dialog] input')?.focus(),0);},
 restoreFocus(){this.beforeDialog?.focus();},
 dispose(){
  if(this.handler)document.removeEventListener('keydown',this.handler);
  if(this.dragScrollHandler)document.removeEventListener('dragover',this.dragScrollHandler);
  if(this.dragScrollStop){document.removeEventListener('drop',this.dragScrollStop);document.removeEventListener('dragend',this.dragScrollStop);}
  this.stopDragAutoScroll?.();
  this.handler=null;this.dragScrollHandler=null;this.dragScrollStop=null;
  this.dotnet=null;
 }
};
window.addEventListener('beforeinstallprompt',e=>{e.preventDefault();window.sprintPilot.pwaPrompt=e;window.sprintPilot.notifyPwa();});
window.addEventListener('appinstalled',()=>{window.sprintPilot.pwaPrompt=null;window.sprintPilot.notifyPwa();});
if('serviceWorker'in navigator)navigator.serviceWorker.register('/sw.js').catch(()=>{});
document.addEventListener('keydown',e=>{if(e.key!=='Tab')return;const dialog=document.querySelector('[aria-modal=true]');if(!dialog)return;const focusable=[...dialog.querySelectorAll('button,input,select,textarea,a[href],[tabindex="0"]')].filter(x=>!x.disabled&&x.offsetParent!==null);if(!focusable.length)return;const first=focusable[0],last=focusable.at(-1);if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus();}else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus();}});
