(() => {
  const motion = window.Motion;
  if (!motion) return;
  const preference = window.matchMedia('(prefers-reduced-motion: reduce)');
  let cleanup = [];
  const controls = new Set();
  function animate(element, values, options) {
    const control = motion.animate(element, values, options);
    controls.add(control);
    control.finished.then(() => controls.delete(control)).catch(() => controls.delete(control));
    return control;
  }
  function disable() {
    for (const dispose of cleanup) dispose();
    cleanup = [];
    for (const control of controls) control.complete();
    controls.clear();
    document.querySelectorAll('[data-reveal], .app-symbol, .dock-preview img, .button, .magnet-sprite, .magnet-slot').forEach(element => {
      element.style.removeProperty('transform');
      element.style.removeProperty('opacity');
    });
  }
  function listen(element, name, callback, options) {
    element.addEventListener(name, callback, options);
    cleanup.push(() => element.removeEventListener(name, callback, options));
  }
  function assemble(selector, distance = 30) {
    if (preference.matches) return;
    document.querySelectorAll(selector).forEach((element, index) => {
      animate(element, { opacity: [0, 1], y: [distance, 0], scale: [.88, 1] }, {
        type: 'spring', stiffness: 190, damping: 22, delay: index * .09
      });
    });
  }
  function setupIsland() {
    const island = document.querySelector('.island-preview');
    const region = document.querySelector('.hero-visual');
    if (!island || !region) return;
    const buttons = [...document.querySelectorAll('[data-island-stage]')];
    const blocks = island.querySelector('.preview-blocks');
    const extra = island.querySelector('.island-extra');
    const statusBar = region.querySelector('.status-preview');
    let stage = 'bar';
    let baseMode = 'bar';
    let closeTimer;
    let touched = false;
    let cycleTimer;
    let visible = false;
    let cycleIndex = 0;
    const modes = ['bar', 'island', 'expanded'];
    function stopCycle() { clearInterval(cycleTimer); cycleTimer = undefined; }
    function startCycle() {
      stopCycle();
      if (preference.matches || !visible || document.hidden || touched) return;
      cycleIndex = Math.max(0, modes.indexOf(stage));
      cycleTimer = setInterval(() => {
        cycleIndex = (cycleIndex + 1) % modes.length;
        setStage(modes[cycleIndex]);
      }, 1400);
    }
    function setStage(next, instant = false) {
      stage = next;
      if (next !== 'expanded') baseMode = next;
      for (const button of buttons) button.setAttribute('aria-pressed', String(button.dataset.islandStage === next));
      const expanded = next === 'expanded';
      const centered = baseMode === 'island';
      const clip = expanded ? 'inset(0% 0% 0% 0% round 18px)' : 'inset(0% 0% 100% 0% round 18px)';
      const barWidth = centered ? 190 : region.querySelector('.desktop-preview').clientWidth;
      const barRadius = centered ? '0px 0px 18px 18px' : '0px 0px 0px 0px';
      island.style.left = centered ? '12px' : 'auto';
      island.style.right = '12px';
      island.style.marginInline = centered ? 'auto' : '0';
      if (instant || preference.matches) {
        island.style.clipPath = clip;
        if (statusBar) { statusBar.style.width = barWidth + 'px'; statusBar.style.borderRadius = barRadius; }
        blocks.style.opacity = expanded ? '1' : '0';
        extra.style.opacity = expanded ? '1' : '0';
        blocks.style.removeProperty('transform');
        extra.style.removeProperty('transform');
      } else {
        animate(island, { clipPath: clip }, { duration: .55, ease: [.22, 1, .36, 1] });
        if (statusBar) animate(statusBar, { width: barWidth, borderRadius: barRadius }, { duration: .5, ease: [.22, 1, .36, 1] });
        animate(blocks, { opacity: expanded ? 1 : 0, y: expanded ? 0 : -8 }, { duration: .3 });
        animate(extra, { opacity: expanded ? 1 : 0, y: expanded ? 0 : -6 }, { duration: .3 });
      }
    }
    function interact() { touched = true; clearTimeout(closeTimer); stopCycle(); }
    setStage('bar', true);
    for (const button of buttons) listen(button, 'click', () => { interact(); setStage(button.dataset.islandStage); });
    const expandOnHover = event => {
      if (event.pointerType === 'touch') return;
      interact(); if (stage !== 'expanded') setStage('expanded');
    };
    listen(island, 'pointerenter', expandOnHover);
    if (statusBar) listen(statusBar, 'pointerenter', expandOnHover);
    listen(window, 'resize', () => setStage(stage, true), { passive: true });
    listen(region, 'pointerenter', () => clearTimeout(closeTimer));
    listen(region, 'pointerleave', () => {
      clearTimeout(closeTimer);
      closeTimer = setTimeout(() => {
        if (region.contains(document.activeElement)) return;
        setStage(baseMode); touched = false; startCycle();
      }, 900);
    });
    listen(region, 'focusout', event => {
      if (!region.contains(event.relatedTarget) && !region.matches(':hover')) { touched = false; startCycle(); }
    });
    listen(document, 'visibilitychange', () => { if (document.hidden) stopCycle(); else startCycle(); });
    if (!preference.matches) cleanup.push(motion.inView(region.querySelector('.desktop-preview'), () => {
      visible = true; startCycle();
      return () => { visible = false; stopCycle(); };
    }));
    cleanup.push(() => { clearTimeout(closeTimer); stopCycle(); });
  }
  function setupMagnets() {
    const row = document.querySelector('.magnet-dock');
    if (!row) return;
    const slots = [...row.querySelectorAll('.magnet-slot')];
    const springs = slots.map(element => {
      const scale = motion.springValue(1, { stiffness: 320, damping: 25 });
      const x = motion.springValue(0, { stiffness: 320, damping: 25 });
      const y = motion.springValue(0, { stiffness: 320, damping: 25 });
      const detach = motion.styleEffect(element.querySelector('.magnet-sprite'), { scale, x, y });
      cleanup.push(() => { detach(); scale.destroy(); x.destroy(); y.destroy(); });
      return { scale, x, y, center: 0, width: 0, growth: 0 };
    });
    let left = 0;
    let radius = 120;
    let pointer = null;
    let pendingFrame = 0;
    function measure() {
      left = row.getBoundingClientRect().left;
      for (let index = 0; index < slots.length; index++) {
        springs[index].center = slots[index].offsetLeft + slots[index].offsetWidth / 2;
        springs[index].width = slots[index].offsetWidth;
      }
      radius = springs.length > 1 ? (springs[1].center - springs[0].center) * 2.25 : 100;
    }
    function update() {
      pendingFrame = 0;
      let total = 0;
      for (const item of springs) {
        const distance = pointer === null ? 1 : Math.min(1, Math.abs(pointer - item.center) / radius);
        const wave = (1 + Math.cos(distance * Math.PI)) / 2;
        item.growth = .55 * wave;
        total += item.width * item.growth;
      }
      let offset = -total / 2;
      for (const item of springs) {
        const extra = item.width * item.growth;
        item.scale.set(1 + item.growth);
        item.x.set(offset + extra / 2);
        item.y.set(-item.growth * 7);
        offset += extra;
      }
    }
    function schedule(value) {
      pointer = value;
      if (!pendingFrame) pendingFrame = requestAnimationFrame(update);
    }
    listen(row, 'pointerenter', measure);
    listen(row, 'pointermove', event => { if (event.pointerType !== 'touch') schedule(event.clientX - left); });
    listen(row, 'pointerleave', () => schedule(null));
    listen(row, 'focusout', event => { if (!row.contains(event.relatedTarget)) schedule(null); });
    for (let index = 0; index < slots.length; index++) {
      listen(slots[index], 'focus', () => { measure(); schedule(springs[index].center); });
      listen(slots[index], 'click', () => { measure(); schedule(springs[index].center); });
    }
    listen(window, 'resize', measure, { passive: true });
    cleanup.push(() => { if (pendingFrame) cancelAnimationFrame(pendingFrame); });
    measure();
  }
  function setupSearchDemo() {
    const scene = document.querySelector('.search-demo');
    if (!scene) return;
    const trigger = scene.querySelector('.search-demo-trigger');
    const panel = scene.querySelector('.search-demo-panel');
    const input = scene.querySelector('input');
    const results = scene.querySelector('.search-demo-results');
    const opened = scene.querySelector('.search-demo-opened');
    const status = scene.querySelector('.search-demo-status');
    const enter = scene.querySelector('.search-demo-enter');
    const examples = [
      { name: 'Visual Studio Code', icon: '⌘', category: 'Aplicativo · Desenvolvimento' },
      { name: 'Explorador de Arquivos', icon: '▰', category: 'Aplicativo · Arquivos' },
      { name: 'Spotify', icon: '♫', category: 'Aplicativo · Música' }
    ];
    let matches = [];
    let timers = [];
    let userEdited = false;
    let visible = false;
    function clearAuto() { for (const timer of timers) clearTimeout(timer); timers = []; }
    function renderResults() {
      const term = input.value.trim().toLocaleLowerCase('pt-BR');
      matches = examples.filter(app => app.name.toLocaleLowerCase('pt-BR').includes(term));
      results.replaceChildren(); results.hidden = false; opened.hidden = true;
      enter.classList.remove('is-pressed');
      for (let index = 0; index < matches.length; index++) {
        const app = matches[index];
        const button = document.createElement('button');
        button.type = 'button'; button.className = 'search-demo-result' + (index === 0 ? ' is-selected' : '');
        button.dataset.result = String(index);
        button.setAttribute('aria-label', app.name + (index === 0 ? ', pré-selecionado, Enter para abrir na demonstração' : ', abrir na demonstração'));
        const icon = document.createElement('span'); icon.className = 'search-demo-result-icon'; icon.textContent = app.icon; icon.setAttribute('aria-hidden', 'true');
        const details = document.createElement('span');
        const name = document.createElement('strong'); name.textContent = app.name;
        const category = document.createElement('small'); category.textContent = app.category;
        details.append(name, category); button.append(icon, details);
        if (index === 0) { const key = document.createElement('kbd'); key.textContent = '↵'; button.append(key); }
        results.append(button);
      }
      const message = matches.length ? 'Primeiro resultado selecionado. Pressione Enter.' : 'Nenhum exemplo encontrado. Experimente Code, Spotify ou Arquivos.';
      if (status.textContent !== message) status.textContent = message;
    }
    function openPanel(focus = false) {
      trigger.hidden = true; trigger.setAttribute('aria-expanded', 'true'); panel.hidden = false;
      renderResults();
      if (!preference.matches) animate(panel, { opacity: [0, 1], y: [15, 0], scale: [.96, 1] }, { type: 'spring', stiffness: 240, damping: 25 });
      if (focus) input.focus({ preventScroll: true });
    }
    function launch(app) {
      if (!app) return;
      enter.classList.add('is-pressed');
      results.hidden = true; opened.hidden = false;
      opened.querySelector('strong').textContent = app.name;
      opened.querySelector('.search-demo-opened-icon').textContent = app.icon;
      status.textContent = 'Enter → ' + app.name + '. Abertura ilustrativa, sem executar o programa.';
      if (!preference.matches) animate(opened, { opacity: [0, 1], y: [12, 0], scale: [.95, 1] }, { type: 'spring', stiffness: 260, damping: 25 });
    }
    function play() {
      clearAuto(); userEdited = false; input.value = '';
      status.setAttribute('aria-live', 'off');
      panel.hidden = true; trigger.hidden = false; trigger.setAttribute('aria-expanded', 'false');
      if (preference.matches) { input.value = 'Code'; openPanel(); launch(matches[0]); return; }
      const queue = (delay, action) => timers.push(setTimeout(action, delay));
      queue(350, () => openPanel());
      [...'Code'].forEach((character, index) => queue(750 + index * 190, () => { input.value += character; renderResults(); }));
      queue(1900, () => { enter.classList.add('is-pressed'); animate(enter, { scale: [1, .9, 1] }, { duration: .22 }); });
      queue(2080, () => launch(matches[0]));
      queue(4200, () => { if (visible && !document.hidden && !userEdited) play(); });
    }
    function interact() { userEdited = true; clearAuto(); status.setAttribute('aria-live', 'polite'); }
    listen(trigger, 'click', () => { interact(); openPanel(true); });
    listen(input, 'focus', interact);
    listen(input, 'input', () => { interact(); renderResults(); });
    listen(input, 'keydown', event => { if (event.key === 'Enter') { event.preventDefault(); interact(); launch(matches[0]); } });
    listen(results, 'click', event => {
      const result = event.target.closest('[data-result]');
      if (result && results.contains(result)) { interact(); launch(matches[Number(result.dataset.result)]); }
    });
    const replay = document.querySelector('#replay-search');
    if (replay) listen(replay, 'click', play);
    for (const button of document.querySelectorAll('[data-show-search]')) listen(button, 'click', () => {
      interact(); scene.scrollIntoView({ behavior: preference.matches ? 'instant' : 'smooth', block: 'center' }); openPanel(true);
    });
    listen(document, 'visibilitychange', () => {
      if (document.hidden) clearAuto();
      else if (visible && !userEdited && !preference.matches) play();
    });
    if (!preference.matches) cleanup.push(motion.inView(scene, () => {
      visible = true;
      if (!userEdited && !document.hidden) play();
      return () => { visible = false; clearAuto(); };
    }, { amount: .5 }));
    cleanup.push(clearAuto);
  }
  function enable() {
    disable();
    setupIsland();
    setupSearchDemo();
    const replay = document.querySelector('#replay-illustrations');
    if (replay) { replay.disabled = preference.matches; replay.title = preference.matches ? 'Movimento reduzido ativado no sistema' : 'Repetir a montagem dos ícones'; }
    if (preference.matches) return;
    setupMagnets();
    cleanup.push(motion.inView('[data-reveal]', element => {
      animate(element, { opacity: [0, 1], y: [18, 0] }, { duration: .48, ease: [.22, 1, .36, 1] });
    }, { amount: .12 }));
    cleanup.push(motion.hover('.button', element => {
      animate(element, { y: -2 }, { type: 'spring', stiffness: 420, damping: 30 });
      return () => animate(element, { y: 0 }, { type: 'spring', stiffness: 420, damping: 30 });
    }));
    cleanup.push(motion.hover('.app-symbol, .dock-preview img', element => {
      animate(element, { y: -5, scale: 1.18 }, { type: 'spring', stiffness: 360, damping: 23 });
      return () => animate(element, { y: 0, scale: 1 }, { type: 'spring', stiffness: 360, damping: 23 });
    }));
    cleanup.push(motion.inView('.hero-visual', () => {
      assemble('.island-preview', -30);
      assemble('.dock-preview > *', 36);
    }));
    cleanup.push(motion.inView('.magnet-scene', () => assemble('.magnet-slot', 50)));
    cleanup.push(motion.inView('.monitor-visual', () => {
      assemble('.monitor-components > div', 28);
      assemble('.monitor-history, .monitor-overlay, .overlay-demo', 22);
    }));
    cleanup.push(motion.inView('.player-news-visual', () => {
      assemble('.player-news-card', 34);
      assemble('.player-news-dock > span', 24);
    }));
    if (replay) listen(replay, 'click', () => assemble('.magnet-slot', 50));
  }
  document.querySelectorAll('.hero-copy, .player-news-copy, .monitor-copy, .section-heading, .feature-card, .wind-detail, .steps article, .setup-notice, .requirements, .release-section').forEach(element => element.setAttribute('data-reveal', ''));
  preference.addEventListener('change', enable);
  window.addEventListener('pagehide', disable);
  window.addEventListener('pageshow', event => { if (event.persisted) enable(); });
  enable();
})();
