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
    const clips = {
      compact: 'inset(0% 0% 100% 0% round 18px)',
      partial: 'inset(0% 0% 25% 0% round 18px)',
      full: 'inset(0% 0% 0% 0% round 18px)'
    };
    let stage = 'compact';
    let closeTimer;
    let touched = false;
    const timers = [];
    function setStage(next, instant = false) {
      stage = next;
      for (const button of buttons) button.setAttribute('aria-pressed', String(button.dataset.islandStage === next));
      const expanded = next !== 'compact';
      if (instant || preference.matches) {
        island.style.clipPath = clips[next];
        blocks.style.opacity = expanded ? '1' : '0';
        extra.style.opacity = next === 'full' ? '1' : '0';
        blocks.style.removeProperty('transform');
        extra.style.removeProperty('transform');
      } else {
        animate(island, { clipPath: clips[next] }, { duration: .55, ease: [.22, 1, .36, 1] });
        animate(blocks, { opacity: expanded ? 1 : 0, y: expanded ? 0 : -8 }, { duration: .3 });
        animate(extra, { opacity: next === 'full' ? 1 : 0, y: next === 'full' ? 0 : -6 }, { duration: .3 });
      }
    }
    function interact() { touched = true; clearTimeout(closeTimer); }
    setStage('compact', true);
    for (const button of buttons) listen(button, 'click', () => { interact(); setStage(button.dataset.islandStage); });
    const expandOnHover = event => {
      if (event.pointerType === 'touch') return;
      interact(); if (stage === 'compact') setStage('partial');
    };
    listen(island, 'pointerenter', expandOnHover);
    const statusBar = region.querySelector('.status-preview');
    if (statusBar) listen(statusBar, 'pointerenter', expandOnHover);
    listen(region, 'pointerenter', () => clearTimeout(closeTimer));
    listen(region, 'pointerleave', () => {
      clearTimeout(closeTimer);
      closeTimer = setTimeout(() => { if (!region.contains(document.activeElement)) setStage('compact'); }, 900);
    });
    if (!preference.matches) cleanup.push(motion.inView(island, () => {
      for (const [delay, next] of [[650, 'partial'], [1600, 'full'], [3300, 'compact']]) {
        timers.push(setTimeout(() => { if (!touched) setStage(next); }, delay));
      }
    }));
    cleanup.push(() => { clearTimeout(closeTimer); for (const timer of timers) clearTimeout(timer); });
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
  function enable() {
    disable();
    setupIsland();
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
