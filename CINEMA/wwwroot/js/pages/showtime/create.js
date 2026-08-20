const W = { step:1, movieId:'', movieName:'', movieDur:0, date:'', rooms:[], price:0, lang:'Vietsub', cleanMin:15, slots:[], items:[] };
let sortInst = null;

function goToStep(n) {
  if (n === 2 && !v1()) return;
  if (n === 3 && !v2()) return;
  if (n === 3) buildPreview();
  document.querySelectorAll('.wizard-panel').forEach(p => p.classList.remove('active'));
  const panel = document.getElementById('panel-' + n);
  if (panel) panel.classList.add('active');
  [1,2,3].forEach(i => {
    const dot = document.getElementById('dot-' + i);
    if (dot) dot.className = 'wstep-dot ' + (i < n ? 'done' : i === n ? 'active' : 'pending');
    const lbl = document.getElementById('lbl-' + i);
    if (lbl) lbl.className = 'wstep-label ' + (i < n ? 'done' : i === n ? 'active' : '');
    const line = document.getElementById('line-' + i);
    if (line && i < 3) line.className = 'wstep-line ' + (i < n ? 'done' : '');
  });
  W.step = n;
  window.scrollTo({top:0, behavior:'smooth'});
}

function onMovieChange(sel) {
  const o = sel.options[sel.selectedIndex];
  W.movieId = sel.value; W.movieName = o.getAttribute('data-name') || ''; W.movieDur = parseInt(o.getAttribute('data-dur')) || 0;
  updateInfo();
}

function updateInfo() {
  const cleanInput = document.getElementById('s1Clean');
  W.cleanMin = cleanInput ? (parseInt(cleanInput.value) || 15) : 15;
  const box = document.getElementById('s1Info');
  if (!box) return;
  if (W.movieDur > 0) {
    const tot = W.movieDur + W.cleanMin;
    box.innerHTML = '<i class="bi bi-info-circle me-1"></i>Phim <strong>' + W.movieName + '</strong>: ' + W.movieDur + ' phút + ' + W.cleanMin + 'p dọn phòng = <strong>' + tot + ' phút/suất</strong> &mdash; Tối đa <strong>' + Math.floor(840/tot) + ' suất/ngày</strong>';
    box.classList.remove('d-none');
  } else box.classList.add('d-none');
  updS2sum();
}

function toggleRoom(lbl) {
  const cb = lbl.querySelector('input');
  if (cb) {
    cb.checked = !cb.checked;
    lbl.classList.toggle('checked', cb.checked);
  }
  W.rooms = [];
  document.querySelectorAll('.room-chip input:checked').forEach(c => W.rooms.push({id:parseInt(c.value), name:c.getAttribute('data-name')}));
  updS2sum();
}

function v1() {
  if (!W.movieId) { alert('Vui lòng chọn phim!'); return false; }
  const s1Date = document.getElementById('s1Date');
  if (!s1Date || !s1Date.value) { alert('Vui lòng chọn ngày chiếu!'); return false; }
  if (W.rooms.length === 0) { alert('Vui lòng chọn ít nhất 1 phòng!'); return false; }
  const s1Price = document.getElementById('s1Price');
  if (!s1Price || !s1Price.value) { alert('Vui lòng nhập giá vé!'); return false; }
  W.date = s1Date.value;
  W.price = parseFloat(s1Price.value);
  const s1Lang = document.getElementById('s1Lang');
  W.lang = s1Lang ? s1Lang.value : 'Vietsub';
  const s1Clean = document.getElementById('s1Clean');
  W.cleanMin = s1Clean ? (parseInt(s1Clean.value) || 15) : 15;
  const dStr = new Date(W.date + 'T00:00').toLocaleDateString('vi-VN', {weekday:'long',day:'2-digit',month:'2-digit',year:'numeric'});
  const s2Sub = document.getElementById('s2Sub');
  if (s2Sub) s2Sub.textContent = W.movieName + ' — ' + dStr + ' — ' + W.rooms.map(r=>r.name).join(', ');
  return true;
}

function updS2sum() {
  const n = W.slots.length, r = W.rooms.length;
  const s2Cnt = document.getElementById('s2Cnt');
  if (s2Cnt) s2Cnt.textContent = n + ' khung giờ';
  const s2Calc = document.getElementById('s2Calc');
  if (s2Calc) s2Calc.textContent = n > 0 && r > 0 ? '× ' + r + ' phòng' : '';
  const s2Total = document.getElementById('s2Total');
  if (s2Total) s2Total.textContent = n * r > 0 ? '→ ' + (n * r) + ' suất sẽ tạo' : '';
}

function switchTab(t) {
  const tabM = document.getElementById('tabManual');
  if (tabM) tabM.classList.toggle('d-none', t !== 'manual');
  const tabA = document.getElementById('tabAuto');
  if (tabA) tabA.classList.toggle('d-none', t !== 'auto');
  const btnM = document.getElementById('tabManualBtn');
  if (btnM) btnM.classList.toggle('active', t === 'manual');
  const btnA = document.getElementById('tabAutoBtn');
  if (btnA) btnA.classList.toggle('active', t === 'auto');
}

function refreshSlots() {
  const c = document.getElementById('sortableSlots');
  if (!c) return;
  c.innerHTML = '';
  if (W.slots.length === 0) {
    c.innerHTML = '<span class="text-muted small">Chưa có khung giờ nào...</span>';
  } else {
    W.slots.forEach(t => {
      const el = document.createElement('div');
      el.className = 's-item'; el.dataset.time = t;
      el.innerHTML = '<i class="bi bi-grip-vertical" style="opacity:.5;font-size:.85rem;"></i>' + t + '<span class="rm" onclick="removeSlot(\'' + t + '\')" title="Xóa">&#x2715;</span>';
      c.appendChild(el);
    });
    if (sortInst) sortInst.destroy();
    if (window.Sortable) {
        sortInst = new Sortable(c, {animation:150, onEnd:e=>{const x=W.slots.splice(e.oldIndex,1)[0];W.slots.splice(e.newIndex,0,x);}});
    }
  }
  document.querySelectorAll('#presetChips .ts-chip').forEach(c => c.classList.toggle('selected', W.slots.includes(c.dataset.time)));
  updS2sum();
}

function togglePreset(btn) {
  const t = btn.dataset.time;
  W.slots.includes(t) ? W.slots = W.slots.filter(s=>s!==t) : (W.slots.push(t), W.slots.sort());
  refreshSlots();
}

function removeSlot(t) { W.slots = W.slots.filter(s=>s!==t); refreshSlots(); }
function clearSlots() { W.slots = []; refreshSlots(); }

function addManual() {
  const input = document.getElementById('manualTime');
  if (!input) return;
  const t = input.value;
  if (!t || W.slots.includes(t)) return;
  W.slots.push(t); W.slots.sort(); refreshSlots();
  input.value = '';
}

async function autoGen() {
  const startEl = document.getElementById('autoStart');
  const endEl = document.getElementById('autoEnd');
  if (!startEl || !endEl) return;
  const start = startEl.value, end = endEl.value;
  if (!start || !end) { alert('Vui lòng nhập giờ bắt đầu và kết thúc!'); return; }
  if (!W.movieDur) { alert('Vui lòng chọn phim trước!'); return; }
  const cleanInput = document.getElementById('s1Clean');
  const tot = W.movieDur + (cleanInput ? (parseInt(cleanInput.value) || 15) : 15);
  const sMin = toMin(start), eMin = toMin(end);
  let busy = [];
  const autoAvoid = document.getElementById('autoAvoid');
  if (autoAvoid && autoAvoid.checked && W.date) {
    try {
      const r = await fetch('/Showtime/GetScheduleByDate?date=' + W.date);
      const ex = await r.json();
      busy = ex.map(e => ({auditoriumId:e.auditoriumId, s:toMin(e.startTime), e:toMin(e.endTime)}));
    } catch(e){}
  }
  const gen = []; let cur = sMin;
  while (cur + W.movieDur <= eMin + 30) {
    const endSlot = cur + tot;
    const isFreeInAnySelectedRoom = W.rooms.some(room => {
      const hasOverlap = busy.some(b => b.auditoriumId === room.id && cur < b.e && endSlot > b.s);
      return !hasOverlap;
    });

    if (isFreeInAnySelectedRoom) {
      gen.push(minToT(cur));
    }
    cur += tot;
  }
  const chipsEl = document.getElementById('autoChips');
  if (chipsEl) chipsEl.innerHTML = gen.map(t=>'<span class="auto-chip">' + t + '</span>').join('');
  const infoEl = document.getElementById('autoInfo');
  if (infoEl) infoEl.textContent = gen.length + ' suất từ ' + start + ' đến ' + end + ' (' + tot + 'p/suất' + ((autoAvoid && autoAvoid.checked)?' — đã tránh trùng lịch':'') + ')';
  W.slots = gen; refreshSlots();
}

function v2() { if (W.slots.length===0){alert('Vui lòng chọn ít nhất 1 khung giờ!');return false;} return true; }

function toMin(t) { if(!t)return 0; const[h,m]=t.split(':').map(Number); return h*60+m; }
function minToT(m) { const h=Math.floor(m/60)%24,mm=m%60; return String(h).padStart(2,'0')+':'+String(mm).padStart(2,'0'); }
function addMin(iso, mins) {
  const d = new Date(iso);
  d.setMinutes(d.getMinutes() + mins);
  const pad = n => String(n).padStart(2,'0');
  return d.getFullYear()+'-'+pad(d.getMonth()+1)+'-'+pad(d.getDate())+'T'+pad(d.getHours())+':'+pad(d.getMinutes());
}
function fmtT(localISO) { return localISO.slice(11,16); }

function buildPreview() {
  W.items = [];
  const tot = W.movieDur + W.cleanMin;
  W.rooms.forEach(room => {
    W.slots.forEach(slot => {
      const startISO = W.date + 'T' + slot + ':00';
      const endISO = addMin(startISO, tot);
      W.items.push({roomId:room.id, roomName:room.name, startISO, endISO, startStr:slot, endStr:fmtT(endISO), price:W.price, lang:W.lang, hasConflict:null, conflictWith:null, isBatchConflict:false, selected:true});
    });
  });
  renderGrid(true); updStats();
  checkConflicts();
}

function renderGrid(chk) {
  const g = document.getElementById('previewGrid');
  if (!g) return;
  g.innerHTML = '';
  W.items.forEach((item, i) => {
    const div = document.createElement('div'); div.className = 'col-md-4';
    let badge = chk || item.hasConflict===null
      ? '<span class="badge bg-secondary"><i class="bi bi-hourglass me-1"></i>Đang kiểm tra...</span>'
      : item.hasConflict
        ? '<span class="badge bg-danger"><i class="bi bi-x-circle me-1"></i>Trùng lịch</span>'
        : '<span class="badge bg-success"><i class="bi bi-check-circle me-1"></i>Hợp lệ</span>';
    const cls = chk || item.hasConflict===null ? 'preview-card checking' : item.hasConflict ? 'preview-card conflict' : 'preview-card valid';
    const conf = item.hasConflict && item.conflictWith ? '<div class="conf-detail"><i class="bi bi-exclamation-triangle me-1"></i>' + item.conflictWith + '</div>' : '';
    const disabled = item.hasConflict && !item.isBatchConflict ? 'disabled' : '';
    div.innerHTML = '<div class="' + cls + '" id="pc-'+i+'">' +
      '<div class="card-cb"><input type="checkbox" class="form-check-input" ' + (item.selected?'checked':'') + ' ' + disabled + ' onchange="onCB(' + i + ',this.checked)" /></div>' +
      '<div class="fw-semibold text-truncate pe-4">' + W.movieName + '</div>' +
      '<div class="small text-muted mt-1"><i class="bi bi-building me-1"></i>' + item.roomName + '</div>' +
      '<div class="mt-2 fs-5 fw-bold text-primary">' + item.startStr + ' <i class="bi bi-arrow-right small"></i> ' + item.endStr + '</div>' +
      '<div class="small text-muted mt-1">' + item.price.toLocaleString('vi-VN') + ' đ &bull; ' + item.lang + '</div>' +
      '<div class="mt-2">' + badge + '</div>' + conf + '</div>';
    g.appendChild(div);
  });
}

async function checkConflicts() {
  const reqs = W.items.map((item,i) => ({index:i, auditoriumId:item.roomId, startTime:item.startISO, endTime:item.endISO, isSelected:item.selected}));
  try {
    const r = await fetch('/Showtime/CheckConflicts', {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(reqs)});
    const res = await r.json();
    res.forEach(r => { 
      W.items[r.index].hasConflict = r.hasConflict; 
      W.items[r.index].conflictWith = r.conflictWith; 
      W.items[r.index].isBatchConflict = r.isBatchConflict; 
      if(r.hasConflict && !r.isBatchConflict) W.items[r.index].selected = false; 
    });
  } catch(e) { W.items.forEach(i=>i.hasConflict=false); }
  renderGrid(false); updStats();
}

function recheck() { renderGrid(true); checkConflicts(); }

function updStats() {
  const tot=W.items.length, val=W.items.filter(i=>i.hasConflict===false).length, con=W.items.filter(i=>i.hasConflict===true).length, sel=W.items.filter(i=>i.selected&&!i.hasConflict).length;
  const sTot = document.getElementById('sTot');
  if (sTot) sTot.textContent=tot;
  const sVal = document.getElementById('sVal');
  if (sVal) sVal.textContent=tot>0?val:'-';
  const sCon = document.getElementById('sCon');
  if (sCon) sCon.textContent=tot>0?con:'-';
  const sSave = document.getElementById('sSave');
  if (sSave) sSave.textContent=tot>0?sel:'-';
  const btnLbl = document.getElementById('btnLbl');
  if (btnLbl) btnLbl.textContent='Lưu ' + sel + ' suất hợp lệ';
}

function onCB(i, checked) { W.items[i].selected = checked; updStats(); updBulk(); checkConflicts(); }
function toggleAll() { const a=W.items.filter(i=>!i.hasConflict).every(i=>i.selected); W.items.forEach(i=>{if(!i.hasConflict)i.selected=!a;}); renderGrid(false); updStats(); }
function autoRemoveConflicts() { W.items.forEach(i=>{if(i.hasConflict && !i.isBatchConflict)i.selected=false;}); renderGrid(false); updStats(); }
function clearSel() { W.items.forEach(i=>i.selected=false); renderGrid(false); updStats(); updBulk(); checkConflicts(); }

function updBulk() {
  const s=W.items.filter(i=>i.selected&&!i.hasConflict);
  const bulkBar = document.getElementById('bulkBar');
  if (bulkBar) bulkBar.classList.toggle('on',s.length>0);
  const bulkCnt = document.getElementById('bulkCnt');
  if (bulkCnt) bulkCnt.textContent=s.length;
}

function applyBulk() {
  const p=document.getElementById('bulkPrice').value, l=document.getElementById('bulkLang').value;
  W.items.forEach(i=>{ if(i.selected&&!i.hasConflict){if(p)i.price=parseFloat(p);if(l)i.lang=l;} });
  renderGrid(false); updStats();
}

async function doSubmit() {
  const valid = W.items.filter(i=>i.selected&&!i.hasConflict);
  if (valid.length===0){alert('Không có suất nào hợp lệ!');return;}
  
  const btn = document.getElementById('btnSave');
  if (btn) {
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Đang lưu...';
  }

  const payload = valid.map(item => ({
    movieId: parseInt(W.movieId),
    auditoriumId: item.roomId,
    startTime: item.startISO,
    endTime: item.endISO,
    basePrice: item.price,
    language: item.lang
  }));

  try {
    const r = await fetch('/Showtime/CreateMultipleJson', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    const res = await r.json();
    if (res.success) {
      alert('Đã lưu thành công ' + res.count + ' suất chiếu!');
      window.location.href = '/Showtime/Index';
    } else {
      alert('Lỗi:\n' + (res.errors ? res.errors.join('\n') : 'Không thể lưu.'));
      if (btn) {
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-floppy me-1"></i><span id="btnLbl">Lưu tất cả hợp lệ</span>';
      }
      updStats();
    }
  } catch(e) {
    alert('Lỗi kết nối server.');
    if (btn) {
      btn.disabled = false;
      btn.innerHTML = '<i class="bi bi-floppy me-1"></i><span id="btnLbl">Lưu tất cả hợp lệ</span>';
    }
    updStats();
  }
}

function openModal(id){
    const el = document.getElementById(id);
    if (el) el.classList.add('open');
}
function closeModal(id){
    const el = document.getElementById(id);
    if (el) el.classList.remove('open');
}

document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll('.v3-overlay').forEach(m=>m.addEventListener('click',e=>{if(e.target===m)m.classList.remove('open');}));

    const s1Date = document.getElementById('s1Date');
    if (s1Date && window.ShowtimeCreateConfig && window.ShowtimeCreateConfig.todayDate) {
        s1Date.value = window.ShowtimeCreateConfig.todayDate;
    }
});

async function copySchedule() {
  const dateInput = document.getElementById('copyDate');
  if (!dateInput) return;
  const date=dateInput.value;
  if(!date){alert('Vui lòng chọn ngày!');return;}
  const msg=document.getElementById('copyMsg');
  if (msg) {
    msg.textContent='Đang tải...';
    msg.classList.remove('d-none');
  }
  try {
    const r=await fetch('/Showtime/GetScheduleByDate?date='+date), data=await r.json();
    const times=[...new Set(data.map(d=>d.startTime))].sort();
    if(times.length===0){
        if (msg) msg.textContent='Không tìm thấy lịch chiếu nào trong ngày '+date;
        return;
    }
    times.forEach(t=>{if(!W.slots.includes(t))W.slots.push(t);}); W.slots.sort(); refreshSlots();
    if (msg) msg.textContent='Đã thêm '+times.length+' khung giờ từ ngày '+date;
    setTimeout(()=>closeModal('copyModal'),1200);
  } catch(e){
    if (msg) msg.textContent='Lỗi khi tải lịch. Vui lòng thử lại.';
  }
}

function saveTpl() {
  if(W.slots.length===0){alert('Chưa có khung giờ nào để lưu!');return;}
  const name=prompt('Đặt tên mẫu (vd: Cuối tuần, Ngày lễ):','');
  if(!name)return;
  const tpls=JSON.parse(localStorage.getItem('cz_tpls')||'{}');
  tpls[name]=W.slots; localStorage.setItem('cz_tpls',JSON.stringify(tpls));
  alert('Đã lưu mẫu "'+name+'" ('+W.slots.length+' khung giờ)!');
}

function openLoadTpl() {
  const tpls=JSON.parse(localStorage.getItem('cz_tpls')||'{}'), keys=Object.keys(tpls);
  const list=document.getElementById('tplList');
  if (!list) return;
  if(keys.length===0){list.innerHTML='<p class="text-muted small">Chưa có mẫu nào. Lưu mẫu tại bước 2.</p>';}
  else list.innerHTML=keys.map(k=>'<div class="d-flex justify-content-between align-items-center mb-2 p-2 border rounded-3"><div><div class="fw-semibold">'+k+'</div><div class="text-muted small">'+tpls[k].join(', ')+'</div></div><div class="d-flex gap-2"><button class="btn btn-sm btn-primary" onclick="loadTpl(\''+k+'\')">Tải</button><button class="btn btn-sm btn-outline-danger" onclick="delTpl(\''+k+'\')">Xóa</button></div></div>').join('');
  openModal('tplModal');
}

function loadTpl(name) {
  const tpls=JSON.parse(localStorage.getItem('cz_tpls')||'{}');
  if(tpls[name]){W.slots=[...tpls[name]];refreshSlots();closeModal('tplModal');alert('Đã tải mẫu "'+name+'"!');}
}

function delTpl(name) {
  if(!confirm('Xóa mẫu "'+name+'"?'))return;
  const tpls=JSON.parse(localStorage.getItem('cz_tpls')||'{}'); delete tpls[name]; localStorage.setItem('cz_tpls',JSON.stringify(tpls)); openLoadTpl();
}
