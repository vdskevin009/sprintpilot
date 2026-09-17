(() => {
  const root = document.getElementById('sprintpilot-planning-preview');
  const data = JSON.parse(document.getElementById('planning-sample-data').textContent);
  const details = document.getElementById('preview-details');
  root.addEventListener('click', event => {
    const period = event.target.closest('[data-horizon]');
    if (period) {
      root.querySelectorAll('.preview-pane').forEach(pane => { pane.hidden = pane.dataset.period !== period.dataset.horizon; });
      details.hidden = true;
      return;
    }
    const tag = event.target.closest('.planning-tag-filters button');
    if (tag) {
      const section = tag.closest('.planning-chart');
      const labels = [...section.querySelectorAll('.planning-tag-filters button:not(.clear-tags)')];
      if (tag.classList.contains('clear-tags')) labels.forEach(b => b.setAttribute('aria-pressed', 'true'));
      else {
        const all = labels.every(b => b.getAttribute('aria-pressed') === 'true');
        if (all) labels.forEach(b => b.setAttribute('aria-pressed', String(b === tag)));
        else tag.setAttribute('aria-pressed', String(tag.getAttribute('aria-pressed') !== 'true'));
        if (labels.every(b => b.getAttribute('aria-pressed') === 'false')) labels.forEach(b => b.setAttribute('aria-pressed', 'true'));
      }
      const shown = new Set(labels.filter(b => b.getAttribute('aria-pressed') === 'true').map(b => b.textContent.trim()));
      section.querySelectorAll('.planning-bar-row').forEach(row => { row.style.display = shown.has(row.dataset.label) ? '' : 'none'; });
      return;
    }
    const row = event.target.closest('[data-ids]');
    if (!row) return;
    const ids = new Set(row.dataset.ids.split(',').filter(Boolean).map(Number));
    details.replaceChildren();
    const title = document.createElement('h2'); title.textContent = row.dataset.label + ' · ' + ids.size + ' items'; details.append(title);
    const note = document.createElement('p'); note.textContent = 'Full item estimates below; split chart contributions may be smaller.'; details.append(note);
    const close = document.createElement('button'); close.textContent = 'Close items'; close.addEventListener('click', () => { details.hidden = true; }); details.append(close);
    const wrapper = document.createElement('div'); wrapper.className = 'planning-table-wrap';
    const table = document.createElement('table');
    const header = document.createElement('tr');
    ['ID', 'Work item', 'Owner', 'Hours', 'Tags'].forEach(label => { const th = document.createElement('th'); th.textContent = label; header.append(th); });
    table.append(header);
    data.filter(item => ids.has(item.Id)).forEach(item => {
      const tr = document.createElement('tr');
      [item.Id, item.Title, item.Owner || 'Unassigned', item.Estimate ?? 'Unestimated', item.Tags].forEach(value => { const td = document.createElement('td'); td.textContent = value; tr.append(td); });
      table.append(tr);
    });
    wrapper.append(table); details.append(wrapper); details.hidden = false;
    details.scrollIntoView({ behavior: 'auto', block: 'nearest' });
  });
})();
