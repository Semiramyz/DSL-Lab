const form = document.querySelector('#applicationForm');
const decisionEmpty = document.querySelector('#decisionEmpty');
const decisionContent = document.querySelector('#decisionContent');
const resultGrid = document.querySelector('#resultGrid');
const approvalValue = document.querySelector('#approvalValue');
const approvalIcon = document.querySelector('#approvalIcon');
const ruleList = document.querySelector('#ruleList');
const astTabs = document.querySelector('#astTabs');
const astGraph = document.querySelector('#astGraph');
const graphTitle = document.querySelector('#graphTitle');
const graphDsl = document.querySelector('#graphDsl');
const caseTabs = document.querySelector('#caseTabs');
const ruleCount = document.querySelector('#ruleCount');
const downloadDot = document.querySelector('#downloadDot');
const ruleForm = document.querySelector('#ruleForm');
const editorMessage = document.querySelector('#editorMessage');
let rules = [];
let selectedRule = '';
let graphRequest = 0;
const appliedRules = new Set();
let mandatoryTests = [];
let currentInputs = {};

const labels = { clienteHabilitado: 'Cliente habilitado', nivelIngresos: 'Nivel de ingresos', riesgo: 'Nivel de riesgo', creditoAprobado: 'Crédito aprobado', estabilidad: 'Estabilidad laboral', capacidadPago: 'Capacidad de pago', historial: 'Historial crediticio' };
const inputLabels = { edad: 'Edad', ingresos: 'Ingresos', puntaje: 'Puntaje', antiguedadLaboral: 'Antigüedad', cuotaInicial: 'Cuota inicial', montoSolicitado: 'Monto solicitado', morasHistoricas: 'Moras históricas' };
const formatValue = value => typeof value === 'boolean' ? (value ? 'Sí' : 'No') : typeof value === 'number' ? new Intl.NumberFormat('es-CO').format(value) : value;
const displayLabel = id => labels[id] ?? id.replaceAll(/([A-Z])/g, ' $1').replace(/^./, value => value.toUpperCase());

async function loadRules() {
  rules = await fetch('/api/rules').then(response => response.json());
  ruleList.innerHTML = rules.map(rule => `<button class="rule-row" data-rule="${rule.id}" type="button"><b>${displayLabel(rule.id)}</b><code>${rule.syntax}</code></button>`).join('');
  astTabs.innerHTML = rules.map((rule, index) => `<button class="ast-tab ${index === 0 ? 'active' : ''}" data-rule="${rule.id}" type="button">${displayLabel(rule.id)}</button>`).join('');
  document.querySelectorAll('.rule-row').forEach(button => button.addEventListener('click', () => toggleRule(button.dataset.rule)));
  document.querySelectorAll('.ast-tab').forEach(button => button.addEventListener('click', () => selectRule(button.dataset.rule)));
  updateAppliedRules();
  selectRule(rules[0].id);
  loadMandatoryTests();
}

function toggleRule(id) {
  if (appliedRules.has(id)) appliedRules.delete(id);
  else if (appliedRules.size < 3) appliedRules.add(id);
  else { editorMessage.textContent = 'Máximo 3 reglas aplicables.'; editorMessage.className = 'error-message'; return; }
  updateAppliedRules();
  selectRule(id);
}

function updateAppliedRules() {
  document.querySelectorAll('.rule-row').forEach(item => item.classList.toggle('selected', appliedRules.has(item.dataset.rule)));
  ruleCount.textContent = `${appliedRules.size} / 3 aplicadas`;
}

function selectRule(id) {
  selectedRule = id;
  const rule = rules.find(item => item.id === id);
  graphTitle.textContent = `Árbol de decisión · ${displayLabel(id)}`;
  graphDsl.textContent = rules.find(item => item.id === id)?.syntax ?? '';
  document.querySelectorAll('.ast-tab').forEach(item => item.classList.toggle('active', item.dataset.rule === id));
  document.querySelectorAll('.rule-row').forEach(item => item.classList.toggle('active', item.dataset.rule === id));
  downloadDot.href = `/api/ast/${id}`;
  downloadDot.download = `ast_${id}.dot`;
  populateEditor(id);
  renderGraph(id, currentInputs);
}

function populateEditor(id) {
  const rule = rules.find(item => item.id === id);
  const match = rule?.syntax.match(/^SI (\S+) (>=|<=|==) (\S+) ENTONCES (\S+) = (.+)$/);
  if (!match) return;
  const [, conditionVariable, operator, conditionValue, actionVariable, actionValue] = match;
  ruleForm.elements.name.value = id;
  ruleForm.elements.conditionVariable.value = conditionVariable;
  ruleForm.elements.operator.value = operator;
  ruleForm.elements.conditionValue.value = conditionValue;
  ruleForm.elements.conditionValueType.value = /^-?\d+(\.\d+)?$/.test(conditionValue) ? 'number' : 'text';
  ruleForm.elements.actionVariable.value = actionVariable;
  ruleForm.elements.actionValue.value = actionValue.replaceAll('"', '');
  ruleForm.elements.actionValueType.value = ['true', 'false'].includes(actionValue) ? 'boolean' : /^-?\d+(\.\d+)?$/.test(actionValue) ? 'number' : 'text';
}

async function renderGraph(id, inputs = {}) {
  const requestId = ++graphRequest;
  const dot = await fetch(`/api/ast/${id}`).then(response => response.text());
  if (requestId !== graphRequest || id !== selectedRule) return;
  const nodes = [...dot.matchAll(/n(\d+) \[label="((?:\\.|[^\"])*)"\]/g)].map(match => ({ id: match[1], label: match[2].replaceAll('\\n', '\n').replaceAll('\\"', '"') }));
  const edges = [...dot.matchAll(/n(\d+) -> n(\d+)/g)].map(match => ({ from: match[1], to: match[2] }));
  if (!nodes.length) return;
  const children = Object.fromEntries(nodes.map(node => [node.id, []]));
  const hasParent = new Set();
  edges.forEach(edge => { children[edge.from].push(edge.to); hasParent.add(edge.to); });
  nodes.forEach(node => {
    const nodeChildren = children[node.id] ?? [];
    if (!node.label.startsWith('GreaterThanOrEqual') && !node.label.startsWith('LessThanOrEqual') && !node.label.startsWith('Equal')) return;
    const variableId = nodeChildren.find(child => nodeByIdFrom(nodes, child)?.label.startsWith('Variable'));
    const literalId = nodeChildren.find(child => nodeByIdFrom(nodes, child)?.label.startsWith('Literal'));
    const variableName = variableId ? nodeByIdFrom(nodes, variableId).label.split('\n')[1] : '';
    if (literalId && Object.prototype.hasOwnProperty.call(inputs, variableName)) {
      const literal = nodeByIdFrom(nodes, literalId);
      literal.label = `Literal\n${formatGraphValue(inputs[variableName])}`;
    }
  });
  const root = nodes.find(node => !hasParent.has(node.id));
  const levels = [];
  const visit = (nodeId, depth = 0) => { if (!children[nodeId]) return; (levels[depth] ??= []).push(nodeId); children[nodeId].forEach(child => visit(child, depth + 1)); };
  visit(root.id);
  const nodeWidth = 148, nodeHeight = 54, gapX = 25, gapY = 68;
  const positions = {};
  levels.forEach(level => level.forEach((id, index) => { positions[id] = { x: 25 + index * (nodeWidth + gapX), y: 22 + levels.indexOf(level) * (nodeHeight + gapY) }; }));
  const width = Math.max(420, Math.max(...levels.map(level => level.length)) * (nodeWidth + gapX) + 25);
  const height = levels.length * (nodeHeight + gapY) + 8;
  const nodeById = Object.fromEntries(nodes.map(node => [node.id, node]));
  const edgeMarkup = edges.filter(edge => positions[edge.from] && positions[edge.to]).map(edge => { const from = positions[edge.from], to = positions[edge.to]; return `<path class="graph-edge" d="M ${from.x + nodeWidth / 2} ${from.y + nodeHeight} C ${from.x + nodeWidth / 2} ${from.y + nodeHeight + 30}, ${to.x + nodeWidth / 2} ${to.y - 30}, ${to.x + nodeWidth / 2} ${to.y}" marker-end="url(#arrow)"/>`; }).join('');
  const nodeMarkup = nodes.map(node => { const position = positions[node.id]; const lines = node.label.split('\n'); const type = lines[0].toLowerCase(); const className = type.includes('variable') ? 'graph-variable' : type.includes('literal') ? 'graph-value' : type.includes('assignment') ? 'graph-action' : 'graph-condition'; const text = lines.map((line, index) => `<text x="${position.x + nodeWidth / 2}" y="${position.y + 23 + index * 16}" text-anchor="middle" class="${index === 0 ? 'graph-label' : 'graph-detail'}">${escapeHtml(line)}</text>`).join(''); return `<g class="graph-node ${className}"><rect x="${position.x}" y="${position.y}" width="${nodeWidth}" height="${nodeHeight}" rx="12"/><g>${text}</g></g>`; }).join('');
  astGraph.setAttribute('viewBox', `0 0 ${width} ${height}`);
  astGraph.setAttribute('width', width);
  astGraph.setAttribute('height', height);
  astGraph.innerHTML = `<defs><marker id="arrow" markerWidth="8" markerHeight="8" refX="7" refY="3" orient="auto"><path d="M0,0 L0,6 L7,3 z" fill="#9fb3c8"/></marker></defs>${edgeMarkup}${nodeMarkup}`;
}

function escapeHtml(value) {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
}

function nodeByIdFrom(nodes, id) {
  return nodes.find(node => node.id === id);
}

function formatGraphValue(value) {
  return typeof value === 'number' ? new Intl.NumberFormat('es-CO').format(value) : String(value);
}

form.addEventListener('submit', async event => {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(form).entries());
  Object.keys(data).forEach(key => data[key] = Number(data[key]));
  data.selectedRules = [...appliedRules];
  currentInputs = { ...data };
  const response = await fetch('/api/evaluate', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  if (!response.ok) return;
  const payload = await response.json();
  renderResults(payload.results, payload.appliedRules);
  renderGraph(selectedRule, currentInputs);
});

function renderResults(results, applied = []) {
  const approvalWasSelected = applied.includes('creditoAprobado');
  const approved = approvalWasSelected && results.creditoAprobado === true;
  decisionEmpty.classList.add('hidden');
  decisionContent.classList.remove('hidden');
  approvalValue.textContent = !approvalWasSelected ? 'REGLAS EVALUADAS' : approved ? 'APROBADO' : 'NO APROBADO';
  approvalValue.style.color = !approvalWasSelected ? 'var(--ink)' : approved ? 'var(--teal)' : 'var(--red)';
  approvalIcon.textContent = !approvalWasSelected ? '•' : approved ? '✓' : '×';
  approvalIcon.classList.toggle('rejected', approvalWasSelected && !approved);
  resultGrid.innerHTML = Object.entries(results).map(([key, value]) => `<div class="result-item"><small>${displayLabel(key)}</small><strong class="${value === true || value === 'ALTO' || value === 'BAJO' || value === 'ALTA' || value === 'SUFICIENTE' || value === 'LIMPIO' ? 'good' : 'warn'}">${formatValue(value)}</strong></div>`).join('');
}

ruleForm.addEventListener('submit', async event => {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(ruleForm).entries());
  const existing = rules.some(rule => rule.id.toLowerCase() === data.name.toLowerCase());
  const response = await fetch(existing ? `/api/rules/${encodeURIComponent(data.name)}` : '/api/rules', { method: existing ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  const payload = await response.json();
  editorMessage.textContent = response.ok ? `Regla “${data.name}” guardada.` : payload.error ?? 'No se pudo guardar la regla.';
  editorMessage.className = response.ok ? 'success-message' : 'error-message';
  if (response.ok) { await loadRules(); selectRule(data.name); }
});

document.querySelector('#newRule').addEventListener('click', () => {
  ruleForm.reset();
  ruleForm.elements.name.focus();
  editorMessage.textContent = 'Modo: nueva regla';
  editorMessage.className = '';
});

document.querySelector('#runTests').addEventListener('click', async () => {
  const tests = mandatoryTests.length ? mandatoryTests : await fetch('/api/tests').then(response => response.json());
  const passed = tests.filter(test => test.passed).length;
  const summary = document.querySelector('#testSummary');
  summary.className = `test-summary pass ${passed === tests.length ? '' : 'fail'}`;
  summary.innerHTML = `<span class="empty-icon">${passed === tests.length ? '✓' : '!'}</span><strong>${passed}/${tests.length} casos correctos</strong><p>Resultados de validación</p>`;
  document.querySelector('#testList').classList.remove('hidden');
  document.querySelector('#testList').innerHTML = tests.map(test => `<button class="test-row ${test.passed ? '' : 'fail'}" data-test="${test.id}" type="button"><span>Caso ${test.id} · ${test.edad} años · ${formatValue(test.ingresos)} COP · puntaje ${test.puntaje}</span><span>${test.passed ? 'OK · ver DSL' : 'FALLO'}</span></button>`).join('');
  document.querySelectorAll('.test-row').forEach(row => row.addEventListener('click', () => selectTest(tests.find(test => test.id === Number(row.dataset.test)))));
});

async function loadMandatoryTests() {
  mandatoryTests = await fetch('/api/tests').then(response => response.json());
  caseTabs.innerHTML = mandatoryTests.map(test => `<button class="case-tab" data-test="${test.id}" type="button">Caso ${test.id}</button>`).join('');
  caseTabs.querySelectorAll('.case-tab').forEach(tab => tab.addEventListener('click', () => selectTest(mandatoryTests.find(test => test.id === Number(tab.dataset.test)))));
}

function selectTest(test) {
  const values = { edad: test.edad, ingresos: test.ingresos, puntaje: test.puntaje, antiguedadLaboral: 24, cuotaInicial: 1000000, montoSolicitado: 5000000, morasHistoricas: 0 };
  Object.entries(values).forEach(([key, value]) => form.elements[key].value = value);
  currentInputs = values;
  form.requestSubmit();
  selectRule('creditoAprobado');
  caseTabs.querySelectorAll('.case-tab').forEach(tab => tab.classList.toggle('selected', Number(tab.dataset.test) === test.id));
  document.querySelectorAll('.test-row').forEach(row => row.classList.toggle('selected', Number(row.dataset.test) === test.id));
}

document.querySelector('#fillExample').addEventListener('click', () => {
  const values = { edad: 25, ingresos: 5000000, puntaje: 750, antiguedadLaboral: 24, cuotaInicial: 1000000, montoSolicitado: 5000000, morasHistoricas: 0 };
  Object.entries(values).forEach(([key, value]) => form.elements[key].value = value);
  form.requestSubmit();
});

loadRules().catch(() => { graphTitle.textContent = 'No se pudo conectar con el motor de reglas.'; });
