(() => {
  const section = document.querySelector('#historico');
  if (!section) return;
  const picker = section.querySelector('#history-picker');
  const title = section.querySelector('#history-title');
  const status = section.querySelector('#history-status');
  const description = section.querySelector('#history-description');
  const link = section.querySelector('#history-link');
  const filters = [...section.querySelectorAll('[data-history-filter]')];
  let entries = [];
  function select() {
    const entry = entries.find(item => item.id === picker.value);
    if (!entry) return;
    title.textContent = entry.title;
    status.textContent = entry.state === 'planned' ? 'Em desenvolvimento · ainda não disponível' :
      'Publicada em ' + new Date(entry.date).toLocaleDateString('pt-BR');
    description.textContent = entry.description || 'Consulte as notas oficiais no GitHub.';
    link.hidden = !entry.url;
    if (entry.url) link.href = entry.url;
  }
  function filter(state) {
    picker.replaceChildren();
    for (const entry of entries.filter(item => item.state === state)) {
      const option = document.createElement('option');
      option.value = entry.id; option.textContent = entry.title; picker.append(option);
    }
    for (const button of filters) button.setAttribute('aria-pressed', String(button.dataset.historyFilter === state));
    select();
  }
  picker.addEventListener('change', select);
  for (const button of filters) button.addEventListener('click', () => filter(button.dataset.historyFilter));
  fetch('updates.json').then(response => {
    if (!response.ok) throw new Error('Histórico indisponível');
    return response.json();
  }).then(data => {
    entries = data; picker.disabled = false;
    filters.forEach(button => { button.disabled = false; }); filter('published');
  }).catch(() => {
    status.textContent = 'Não foi possível carregar o histórico. Tente novamente mais tarde.';
    description.textContent = 'As notas publicadas continuam disponíveis no GitHub.';
    link.hidden = false; link.href = 'https://github.com/valteromega2019-blip/think-wind/releases';
  });
})();
