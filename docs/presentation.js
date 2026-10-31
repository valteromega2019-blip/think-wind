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
    document.querySelectorAll('[data-reveal], .app-symbol, .dock-preview img, .button').forEach(element => {
      element.style.removeProperty('transform');
      element.style.removeProperty('opacity');
    });
  }
  function enable() {
    disable();
    if (preference.matches) return;
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
  }
  document.querySelectorAll('.hero-copy, .hero-visual, .player-news-copy, .player-news-visual, .monitor-copy, .monitor-visual, .section-heading, .feature-card, .wind-detail, .steps article, .setup-notice, .requirements, .release-section').forEach(element => element.setAttribute('data-reveal', ''));
  preference.addEventListener('change', enable);
  window.addEventListener('pagehide', disable);
  window.addEventListener('pageshow', event => { if (event.persisted) enable(); });
  enable();
})();
