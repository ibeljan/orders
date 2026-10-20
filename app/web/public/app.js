// Browser code: loads orders every 2 seconds so you can watch the worker complete them.

const $ = (id) => document.getElementById(id);

function cell(text, className) {
  const td = document.createElement('td');
  td.textContent = text;
  if (className) td.className = className;
  return td;
}

function renderOrders(orders) {
  const rows = orders.map((order) => {
    const tr = document.createElement('tr');
    tr.append(
      cell(order.id),
      cell(order.product),
      cell(order.quantity),
      cell(order.status, `status ${order.status.toLowerCase()}`),
      cell(order.processedBy ?? '—', 'muted'),
    );
    const del = document.createElement('button');
    del.textContent = 'Delete';
    del.className = 'link';
    del.onclick = async () => {
      await fetch(`/api/orders/${order.id}`, { method: 'DELETE' });
      loadOrders();
    };
    const actions = document.createElement('td');
    actions.append(del);
    tr.append(actions);
    return tr;
  });
  $('orders').replaceChildren(...rows);
  $('empty').hidden = orders.length > 0;

  const count = (status) => orders.filter((o) => o.status === status).length;
  $('counts').textContent =
    `${count('Pending')} pending · ${count('Processing')} processing · ${count('Completed')} completed`;
}

async function loadOrders() {
  try {
    const response = await fetch('/api/orders');
    if (!response.ok) throw new Error(response.status);
    renderOrders(await response.json());
    $('api-error').hidden = true;
  } catch {
    $('api-error').hidden = false;
  }
}

async function loadInfo() {
  try {
    const info = await (await fetch('/api/info')).json();
    $('web-version').textContent = info.web.version;
    $('web-host').textContent = info.web.hostname;
    $('api-version').textContent = info.api?.version ?? 'unavailable';
    $('api-host').textContent = info.api?.hostname ?? '—';
  } catch {
    // Web server itself is down; nothing to show.
  }
}

$('order-form').addEventListener('submit', async (event) => {
  event.preventDefault();
  const body = { product: $('product').value, quantity: Number($('quantity').value) };
  const response = await fetch('/api/orders', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (response.ok) {
    $('form-message').textContent = 'Order placed.';
    $('product').value = '';
  } else {
    const problem = await response.json().catch(() => ({}));
    const errors = problem.errors ? Object.values(problem.errors).flat().join(' ') : problem.error;
    $('form-message').textContent = errors || 'Could not place order.';
  }
  loadOrders();
});

loadOrders();
loadInfo();
setInterval(loadOrders, 2000);
setInterval(loadInfo, 5000);
