const API_URL = window.PDV_API_URL || 'http://localhost:5235';

const state = {
    token: null,
    cashSessionId: null,
    operatorName: '',
    terminalId: 'PDV-01',
    cart: [],
    paymentMethod: 'Cash',
    catalogPage: 1,
    catalogSearch: '',
    historyPage: 1,
    suppliersPage: 1,
    supplierSearch: '',
    suppliers: [],
    purchaseItems: []
};

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => Array.from(document.querySelectorAll(selector));

const formatCurrency = (value) =>
    (Number(value) || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

const formatDateTime = (value) =>
    value ? new Date(value).toLocaleString('pt-BR') : '-';

const todayInputValue = () => {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
};

const makeDateTimeOffset = (dateValue, endOfDay = false) => {
    const time = endOfDay ? 'T23:59:59.999' : 'T00:00:00.000';
    return new Date(`${dateValue}${time}`).toISOString();
};

const showToast = (message, type = 'success') => {
    const toast = $('#toast');
    toast.textContent = message;
    toast.className = `toast show ${type}`;
    window.clearTimeout(showToast.timer);
    showToast.timer = window.setTimeout(() => toast.className = 'toast', 3200);
};

const setBusy = (button, busy, label) => {
    if (!button) return;
    if (busy) {
        button.dataset.label = button.textContent;
        button.textContent = label;
        button.disabled = true;
    } else {
        button.textContent = button.dataset.label || button.textContent;
        button.disabled = false;
    }
};

const readApiError = async (response) => {
    const text = await response.text();
    if (!text) return `Erro HTTP ${response.status}`;

    try {
        const data = JSON.parse(text);
        return data.title || data.detail || data.message || text;
    } catch {
        return text;
    }
};

const authFetch = async (endpoint, options = {}) => {
    const headers = { ...(options.headers || {}) };
    if (state.token) headers.Authorization = `Bearer ${state.token}`;
    if (options.body && !headers['Content-Type']) headers['Content-Type'] = 'application/json';

    const response = await fetch(`${API_URL}${endpoint}`, { ...options, headers });
    if (response.status === 401) {
        showScreen('login-screen');
        throw new Error('Sessao expirada. Faca login novamente.');
    }

    return response;
};

const fetchJson = async (endpoint, options = {}) => {
    const response = await authFetch(endpoint, options);
    if (!response.ok) throw new Error(await readApiError(response));
    if (response.status === 204) return null;
    return response.json();
};

const showScreen = (screenId) => {
    $$('.screen').forEach((screen) => screen.classList.remove('active'));
    $(`#${screenId}`).classList.add('active');
};

const createCell = (content, className) => {
    const td = document.createElement('td');
    if (className) td.className = className;
    if (content instanceof Node) {
        td.appendChild(content);
    } else {
        td.textContent = content ?? '';
    }
    return td;
};

const appendRow = (tbody, cells, className) => {
    const tr = document.createElement('tr');
    if (className) tr.className = className;
    cells.forEach((cell) => {
        const descriptor = cell && typeof cell === 'object' && !(cell instanceof Node) && 'value' in cell
            ? cell
            : { value: cell };
        tr.appendChild(createCell(descriptor.value, descriptor.className));
    });
    tbody.appendChild(tr);
    return tr;
};

const setTableMessage = (tbody, colSpan, message, className = 'muted') => {
    const tr = document.createElement('tr');
    const td = document.createElement('td');
    td.colSpan = colSpan;
    td.className = className;
    td.textContent = message;
    tr.appendChild(td);
    tbody.replaceChildren(tr);
};

const setPagination = (pageInfo, prevButton, nextButton, data) => {
    const totalPages = data.totalPages || 1;
    pageInfo.textContent = `Pagina ${data.page} de ${totalPages}`;
    prevButton.disabled = data.page <= 1;
    nextButton.disabled = data.page >= totalPages;
};

const setActiveTab = (tabId) => {
    const titles = {
        'dashboard-tab': 'Dashboard',
        'pdv-tab': 'Frente de caixa',
        'catalog-tab': 'Catalogo',
        'history-tab': 'Historico de vendas',
        'cash-tab': 'Fechamento',
        'suppliers-tab': 'Fornecedores',
        'purchase-tab': 'Entrada de estoque',
        'reports-tab': 'Relatorios'
    };

    $$('.tab-btn').forEach((button) => button.classList.toggle('active', button.dataset.tab === tabId));
    $$('.tab-content').forEach((content) => content.classList.toggle('active', content.id === tabId));
    $('#viewTitle').textContent = titles[tabId] || 'PDV Pro';

    const loaders = {
        'dashboard-tab': loadDashboard,
        'catalog-tab': () => loadCatalog(1),
        'history-tab': () => loadHistory(1),
        'cash-tab': updateCashClosingUI,
        'suppliers-tab': () => loadSuppliers(1),
        'purchase-tab': loadPurchaseWorkspace,
        'reports-tab': loadReports
    };

    loaders[tabId]?.();
    if (tabId === 'pdv-tab') $('#barcodeInput').focus();
};

const refreshSessionHeader = () => {
    $('#lblOperator').textContent = state.operatorName || 'Operador';
    $('#lblTerminal').textContent = state.terminalId || 'Terminal';
    $('#badgeSessionState').textContent = state.cashSessionId ? 'Caixa aberto' : 'Sem caixa';
    $('#badgeSessionState').className = state.cashSessionId ? 'badge success' : 'badge warning';
};

const renderCart = () => {
    const cartItems = $('#cartItems');
    cartItems.replaceChildren();

    let total = 0;
    let count = 0;

    state.cart.forEach((item, index) => {
        const itemTotal = item.price * item.quantity;
        total += itemTotal;
        count += item.quantity;

        const row = document.createElement('li');
        row.className = 'cart-grid cart-line';

        const productCell = document.createElement('span');
        const name = document.createElement('strong');
        name.textContent = item.name;
        const code = document.createElement('small');
        code.textContent = item.barcode;
        productCell.append(name, code);

        const quantityCell = document.createElement('span');
        const qty = document.createElement('button');
        qty.className = 'mini-control';
        qty.type = 'button';
        qty.textContent = String(item.quantity);
        qty.title = 'Remover uma unidade';
        qty.addEventListener('click', () => {
            item.quantity -= 1;
            if (item.quantity <= 0) state.cart.splice(index, 1);
            renderCart();
        });
        quantityCell.appendChild(qty);

        const priceCell = document.createElement('span');
        priceCell.textContent = formatCurrency(item.price);
        const totalCell = document.createElement('span');
        totalCell.textContent = formatCurrency(itemTotal);

        row.append(productCell, quantityCell, priceCell, totalCell);
        cartItems.appendChild(row);
    });

    if (state.cart.length === 0) {
        const empty = document.createElement('li');
        empty.className = 'empty-cart';
        empty.textContent = 'Nenhum item no carrinho.';
        cartItems.appendChild(empty);
    }

    $('#lblItemCount').textContent = String(count);
    $('#lblTotal').textContent = formatCurrency(total);
};

const addProductToCart = async () => {
    const barcodeInput = $('#barcodeInput');
    const barcode = barcodeInput.value.trim();
    if (!barcode) return;

    barcodeInput.disabled = true;
    try {
        const existing = state.cart.find((item) => item.barcode === barcode);
        if (existing) {
            existing.quantity += 1;
        } else {
            const product = await fetchJson(`/api/products/barcode/${encodeURIComponent(barcode)}`);
            if (!product.isActive) throw new Error('Produto inativo.');
            state.cart.push({
                productId: product.id,
                barcode: product.barcode,
                name: product.name,
                price: product.unitPrice,
                quantity: 1
            });
        }

        barcodeInput.value = '';
        renderCart();
        $('#paymentSection').classList.add('hidden');
        $('#btnCheckout').classList.remove('hidden');
    } catch (error) {
        showToast(error.message, 'error');
    } finally {
        barcodeInput.disabled = false;
        barcodeInput.focus();
    }
};

const finalizeSale = async () => {
    const totalAmount = state.cart.reduce((sum, item) => sum + item.price * item.quantity, 0);
    if (totalAmount <= 0) {
        showToast('Carrinho vazio.', 'error');
        return;
    }

    const payload = {
        cashSessionId: state.cashSessionId,
        operatorName: state.operatorName,
        customerDocument: null,
        saleDiscountTotal: 0,
        issueFiscalDocument: true,
        items: state.cart.map((item) => ({
            barcode: item.barcode,
            quantity: item.quantity,
            unitDiscount: 0
        })),
        payments: [{ method: state.paymentMethod, amount: totalAmount }]
    };

    const button = $('#btnFinalize');
    setBusy(button, true, 'Processando...');
    try {
        const sale = await fetchJson('/api/sales', {
            method: 'POST',
            body: JSON.stringify(payload)
        });
        showToast(`Venda #${sale.number} concluida.`);
        state.cart = [];
        renderCart();
        $('#paymentSection').classList.add('hidden');
        $('#btnCheckout').classList.remove('hidden');
        loadDashboard();
    } catch (error) {
        showToast(error.message, 'error');
    } finally {
        setBusy(button, false);
        $('#barcodeInput').focus();
    }
};

const loadDashboard = async () => {
    try {
        const [dashboard, stockAlerts] = await Promise.all([
            fetchJson('/api/dashboard'),
            fetchJson('/api/reports/stock-alerts')
        ]);

        $('#kpiSalesCount').textContent = dashboard.todaySales.count;
        $('#kpiSalesTotal').textContent = formatCurrency(dashboard.todaySales.total);
        $('#kpiAvgTicket').textContent = formatCurrency(dashboard.todaySales.averageTicket);
        $('#kpiLowStock').textContent = dashboard.lowStockProducts;

        const recentBody = $('#recentSalesBody');
        recentBody.replaceChildren();
        if (!dashboard.recentSales.length) {
            setTableMessage(recentBody, 4, 'Sem vendas registradas.');
        } else {
            dashboard.recentSales.forEach((sale) => appendRow(recentBody, [
                `#${sale.number}`,
                sale.operatorName,
                sale.status,
                { value: formatCurrency(sale.netTotal), className: 'money' }
            ]));
        }

        const alertsBody = $('#dashboardStockAlertsBody');
        alertsBody.replaceChildren();
        const alerts = stockAlerts.slice(0, 6);
        if (!alerts.length) {
            setTableMessage(alertsBody, 3, 'Sem produtos abaixo do minimo.');
        } else {
            alerts.forEach((item) => appendRow(alertsBody, [
                item.name,
                item.stockQuantity,
                item.minStockQuantity
            ]));
        }
    } catch (error) {
        showToast(error.message, 'error');
    }
};

const loadCatalog = async (page) => {
    const tbody = $('#catalogTableBody');
    setTableMessage(tbody, 6, 'Carregando...');

    try {
        const query = new URLSearchParams({ page, pageSize: 15 });
        if (state.catalogSearch) query.set('search', state.catalogSearch);
        const data = await fetchJson(`/api/products?${query}`);

        state.catalogPage = data.page;
        tbody.replaceChildren();
        if (!data.items.length) {
            setTableMessage(tbody, 6, 'Nenhum produto encontrado.');
        } else {
            data.items.forEach((product) => appendRow(tbody, [
                product.name,
                product.barcode,
                product.unitOfMeasure,
                { value: formatCurrency(product.unitPrice), className: 'money' },
                product.stockQuantity,
                product.minStockQuantity
            ]));
        }
        setPagination($('#catalogPageInfo'), $('#btnCatalogPrev'), $('#btnCatalogNext'), data);
    } catch (error) {
        setTableMessage(tbody, 6, error.message, 'error-cell');
    }
};

const loadHistory = async (page) => {
    const tbody = $('#historyTableBody');
    setTableMessage(tbody, 6, 'Carregando...');

    try {
        const query = new URLSearchParams({ page, pageSize: 10, cashSessionId: state.cashSessionId });
        const data = await fetchJson(`/api/sales?${query}`);

        state.historyPage = data.page;
        tbody.replaceChildren();
        if (!data.items.length) {
            setTableMessage(tbody, 6, 'Nenhuma venda no turno atual.');
        } else {
            data.items.forEach((sale) => {
                const cancelButton = document.createElement('button');
                cancelButton.className = 'btn tiny danger-ghost';
                cancelButton.type = 'button';
                cancelButton.textContent = sale.status === 'Cancelled' ? 'Cancelada' : 'Cancelar';
                cancelButton.disabled = sale.status === 'Cancelled';
                cancelButton.addEventListener('click', () => cancelSale(sale.id));

                appendRow(tbody, [
                    `#${sale.number}`,
                    formatDateTime(sale.createdAt),
                    sale.operatorName,
                    sale.status,
                    { value: formatCurrency(sale.paidAmount), className: 'money' },
                    cancelButton
                ], sale.status === 'Cancelled' ? 'muted-row' : '');
            });
        }
        setPagination($('#historyPageInfo'), $('#btnHistoryPrev'), $('#btnHistoryNext'), data);
    } catch (error) {
        setTableMessage(tbody, 6, error.message, 'error-cell');
    }
};

const cancelSale = async (saleId) => {
    const reason = window.prompt('Motivo do cancelamento:', 'Estorno PDV');
    if (!reason) return;

    try {
        await fetchJson(`/api/sales/${saleId}/cancel`, {
            method: 'POST',
            body: JSON.stringify({ reason })
        });
        showToast('Venda cancelada.');
        loadHistory(state.historyPage);
        loadDashboard();
    } catch (error) {
        showToast(error.message, 'error');
    }
};

const updateCashClosingUI = async () => {
    try {
        const session = await fetchJson(`/api/cash-sessions/${state.cashSessionId}`);
        $('#summaryOpening').textContent = formatCurrency(session.openingAmount);
        $('#summaryExpected').textContent = formatCurrency(session.expectedClosingAmount);
    } catch (error) {
        showToast(error.message, 'error');
    }
};

const closeCashSession = async () => {
    const closingAmount = Number($('#closingAmount').value);
    const notes = $('#closingNotes').value.trim();

    if (Number.isNaN(closingAmount) || closingAmount < 0) {
        showToast('Informe o valor apurado.', 'error');
        return;
    }

    if (!window.confirm(`Confirmar fechamento com ${formatCurrency(closingAmount)}?`)) return;

    const button = $('#btnCloseCash');
    setBusy(button, true, 'Fechando...');
    try {
        const closed = await fetchJson(`/api/cash-sessions/${state.cashSessionId}/close`, {
            method: 'POST',
            body: JSON.stringify({ closingAmount, notes: notes || null })
        });

        showToast(`Caixa fechado. Diferenca: ${formatCurrency(closed.closingDifference)}.`);
        state.cashSessionId = null;
        state.cart = [];
        refreshSessionHeader();
        showScreen('setup-screen');
    } catch (error) {
        showToast(error.message, 'error');
    } finally {
        setBusy(button, false);
    }
};

const loadSuppliers = async (page) => {
    const tbody = $('#suppliersTableBody');
    setTableMessage(tbody, 5, 'Carregando...');

    try {
        const query = new URLSearchParams({ page, pageSize: 10 });
        if (state.supplierSearch) query.set('search', state.supplierSearch);
        const data = await fetchJson(`/api/suppliers?${query}`);

        state.suppliersPage = data.page;
        state.suppliers = data.items;
        tbody.replaceChildren();
        if (!data.items.length) {
            setTableMessage(tbody, 5, 'Nenhum fornecedor cadastrado.');
        } else {
            data.items.forEach((supplier) => {
                const editButton = document.createElement('button');
                editButton.className = 'btn tiny ghost';
                editButton.type = 'button';
                editButton.textContent = 'Editar';
                editButton.addEventListener('click', () => fillSupplierForm(supplier));

                appendRow(tbody, [
                    supplier.name,
                    supplier.cnpj || '-',
                    supplier.contactName || supplier.phone || supplier.email || '-',
                    supplier.isActive ? 'Ativo' : 'Inativo',
                    editButton
                ]);
            });
        }
        setPagination($('#suppliersPageInfo'), $('#btnSuppliersPrev'), $('#btnSuppliersNext'), data);
        refreshPurchaseSuppliers();
    } catch (error) {
        setTableMessage(tbody, 5, error.message, 'error-cell');
    }
};

const fillSupplierForm = (supplier) => {
    $('#supplierFormTitle').textContent = 'Editar fornecedor';
    $('#supplierId').value = supplier.id;
    $('#supplierName').value = supplier.name || '';
    $('#supplierCnpj').value = supplier.cnpj || '';
    $('#supplierContact').value = supplier.contactName || '';
    $('#supplierPhone').value = supplier.phone || '';
    $('#supplierEmail').value = supplier.email || '';
    $('#supplierActive').checked = Boolean(supplier.isActive);
};

const clearSupplierForm = () => {
    $('#supplierFormTitle').textContent = 'Novo fornecedor';
    $('#supplierForm').reset();
    $('#supplierId').value = '';
    $('#supplierActive').checked = true;
};

const saveSupplier = async (event) => {
    event.preventDefault();
    const id = $('#supplierId').value;
    const payload = {
        name: $('#supplierName').value.trim(),
        cnpj: $('#supplierCnpj').value.trim() || null,
        contactName: $('#supplierContact').value.trim() || null,
        phone: $('#supplierPhone').value.trim() || null,
        email: $('#supplierEmail').value.trim() || null
    };

    if (!payload.name) {
        showToast('Nome do fornecedor e obrigatorio.', 'error');
        return;
    }

    const button = $('#btnSaveSupplier');
    setBusy(button, true, 'Salvando...');
    try {
        if (id) {
            await fetchJson(`/api/suppliers/${id}`, {
                method: 'PUT',
                body: JSON.stringify({ ...payload, isActive: $('#supplierActive').checked })
            });
        } else {
            await fetchJson('/api/suppliers', {
                method: 'POST',
                body: JSON.stringify(payload)
            });
        }

        showToast('Fornecedor salvo.');
        clearSupplierForm();
        loadSuppliers(state.suppliersPage);
    } catch (error) {
        showToast(error.message, 'error');
    } finally {
        setBusy(button, false);
    }
};

const refreshPurchaseSuppliers = async () => {
    const data = await fetchJson('/api/suppliers?page=1&pageSize=100&isActive=true');
    const activeSuppliers = data.items;

    const select = $('#purchaseSupplier');
    const selected = select.value;
    select.replaceChildren();

    if (!activeSuppliers.length) {
        const option = document.createElement('option');
        option.textContent = 'Cadastre um fornecedor';
        option.value = '';
        select.appendChild(option);
        return;
    }

    activeSuppliers.forEach((supplier) => {
        const option = document.createElement('option');
        option.value = supplier.id;
        option.textContent = supplier.name;
        select.appendChild(option);
    });

    if (selected) select.value = selected;
};

const loadPurchaseWorkspace = async () => {
    try {
        await refreshPurchaseSuppliers();
        await loadPurchaseEntries();
        renderPurchaseItems();
    } catch (error) {
        showToast(error.message, 'error');
    }
};

const addPurchaseItem = async () => {
    const barcode = $('#purchaseBarcode').value.trim();
    const quantity = Number($('#purchaseQuantity').value);
    const unitCost = Number($('#purchaseUnitCost').value);

    if (!barcode || quantity <= 0 || $('#purchaseUnitCost').value === '' || unitCost < 0 || Number.isNaN(quantity) || Number.isNaN(unitCost)) {
        showToast('Informe produto, quantidade e custo.', 'error');
        return;
    }

    try {
        const product = await fetchJson(`/api/products/barcode/${encodeURIComponent(barcode)}`);
        const existing = state.purchaseItems.find((item) => item.productId === product.id);
        if (existing) {
            existing.quantity += quantity;
            existing.unitCost = unitCost;
        } else {
            state.purchaseItems.push({
                productId: product.id,
                barcode: product.barcode,
                name: product.name,
                quantity,
                unitCost
            });
        }

        $('#purchaseBarcode').value = '';
        $('#purchaseQuantity').value = '1';
        $('#purchaseUnitCost').value = '';
        renderPurchaseItems();
    } catch (error) {
        showToast(error.message, 'error');
    }
};

const renderPurchaseItems = () => {
    const list = $('#purchaseItemsList');
    list.replaceChildren();

    let total = 0;
    state.purchaseItems.forEach((item, index) => {
        const lineTotal = item.quantity * item.unitCost;
        total += lineTotal;

        const row = document.createElement('div');
        row.className = 'entry-line';

        const info = document.createElement('span');
        info.textContent = `${item.name} (${item.quantity} x ${formatCurrency(item.unitCost)})`;

        const amount = document.createElement('strong');
        amount.textContent = formatCurrency(lineTotal);

        const remove = document.createElement('button');
        remove.className = 'btn tiny ghost';
        remove.type = 'button';
        remove.textContent = 'Remover';
        remove.addEventListener('click', () => {
            state.purchaseItems.splice(index, 1);
            renderPurchaseItems();
        });

        row.append(info, amount, remove);
        list.appendChild(row);
    });

    if (!state.purchaseItems.length) {
        const empty = document.createElement('div');
        empty.className = 'empty-note';
        empty.textContent = 'Sem itens adicionados.';
        list.appendChild(empty);
    }

    $('#purchaseTotal').textContent = formatCurrency(total);
};

const confirmPurchaseEntry = async () => {
    const supplierId = $('#purchaseSupplier').value;
    const invoiceNumber = $('#purchaseInvoice').value.trim();

    if (!supplierId || !invoiceNumber || state.purchaseItems.length === 0) {
        showToast('Informe fornecedor, nota e ao menos um item.', 'error');
        return;
    }

    const button = $('#btnConfirmPurchase');
    setBusy(button, true, 'Confirmando...');
    try {
        await fetchJson('/api/purchase-entries', {
            method: 'POST',
            body: JSON.stringify({
                supplierId,
                invoiceNumber,
                notes: null,
                receivedAt: null,
                items: state.purchaseItems.map((item) => ({
                    productId: item.productId,
                    quantity: item.quantity,
                    unitCost: item.unitCost
                }))
            })
        });

        showToast('Entrada registrada e estoque atualizado.');
        state.purchaseItems = [];
        $('#purchaseInvoice').value = '';
        renderPurchaseItems();
        loadPurchaseEntries();
        loadCatalog(state.catalogPage);
        loadDashboard();
    } catch (error) {
        showToast(error.message, 'error');
    } finally {
        setBusy(button, false);
    }
};

const loadPurchaseEntries = async () => {
    const tbody = $('#purchaseEntriesBody');
    setTableMessage(tbody, 4, 'Carregando...');

    try {
        const data = await fetchJson('/api/purchase-entries?page=1&pageSize=10');
        tbody.replaceChildren();
        if (!data.items.length) {
            setTableMessage(tbody, 4, 'Nenhuma entrada registrada.');
        } else {
            data.items.forEach((entry) => appendRow(tbody, [
                entry.invoiceNumber,
                entry.supplierName,
                formatDateTime(entry.receivedAt),
                { value: formatCurrency(entry.totalCost), className: 'money' }
            ]));
        }
    } catch (error) {
        setTableMessage(tbody, 4, error.message, 'error-cell');
    }
};

const loadReports = async () => {
    try {
        const from = $('#reportFrom').value || todayInputValue();
        const to = $('#reportTo').value || from;
        $('#reportFrom').value = from;
        $('#reportTo').value = to;

        const query = new URLSearchParams({
            from: makeDateTimeOffset(from),
            to: makeDateTimeOffset(to, true)
        });

        const [summary, movements] = await Promise.all([
            fetchJson(`/api/reports/sales-summary?${query}`),
            fetchJson('/api/reports/inventory-movements?page=1&pageSize=20')
        ]);

        $('#reportSalesCount').textContent = summary.totalSales;
        $('#reportNetRevenue').textContent = formatCurrency(summary.netRevenue);
        $('#reportDiscounts').textContent = formatCurrency(summary.totalDiscounts);
        renderSalesByHour(summary.byHour || []);
        renderInventoryMovements(movements.items || []);
    } catch (error) {
        showToast(error.message, 'error');
    }
};

const renderSalesByHour = (entries) => {
    const container = $('#salesByHour');
    container.replaceChildren();

    if (!entries.length) {
        const empty = document.createElement('div');
        empty.className = 'empty-note';
        empty.textContent = 'Sem vendas no periodo.';
        container.appendChild(empty);
        return;
    }

    const max = Math.max(...entries.map((entry) => entry.total), 1);
    entries.forEach((entry) => {
        const bar = document.createElement('div');
        bar.className = 'bar-row';
        const label = document.createElement('span');
        label.textContent = `${String(entry.hour).padStart(2, '0')}h`;
        const track = document.createElement('div');
        track.className = 'bar-track';
        const fill = document.createElement('div');
        fill.className = 'bar-fill';
        fill.style.width = `${Math.max(6, (entry.total / max) * 100)}%`;
        const value = document.createElement('strong');
        value.textContent = formatCurrency(entry.total);
        track.appendChild(fill);
        bar.append(label, track, value);
        container.appendChild(bar);
    });
};

const renderInventoryMovements = (items) => {
    const tbody = $('#inventoryMovementsBody');
    tbody.replaceChildren();
    if (!items.length) {
        setTableMessage(tbody, 4, 'Sem movimentacoes.');
        return;
    }

    items.forEach((item) => appendRow(tbody, [
        item.productName,
        item.type,
        item.quantityDelta,
        formatDateTime(item.createdAt)
    ]));
};

const login = async (event) => {
    event.preventDefault();
    const username = $('#username').value.trim();
    const password = $('#password').value;

    if (!username || !password) {
        showToast('Informe usuario e senha.', 'error');
        return;
    }

    const button = $('#btnLogin');
    setBusy(button, true, 'Entrando...');
    try {
        const response = await fetch(`${API_URL}/api/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password })
        });

        if (!response.ok) throw new Error(await readApiError(response));
        const data = await response.json();
        state.token = data.token;
        state.operatorName = data.username;
        $('#operatorName').value = data.username;
        showScreen('setup-screen');
        showToast('Login realizado.');
    } catch (error) {
        showToast(error.message, 'error');
    } finally {
        setBusy(button, false);
    }
};

const openRegister = async (event) => {
    event.preventDefault();
    const terminalId = $('#terminalId').value.trim() || 'PDV-01';
    const operatorName = $('#operatorName').value.trim();
    const openingAmount = Number($('#openingAmount').value);

    if (!operatorName || Number.isNaN(openingAmount) || openingAmount < 0) {
        showToast('Preencha operador e fundo de troco.', 'error');
        return;
    }

    const button = $('#btnOpenRegister');
    setBusy(button, true, 'Abrindo...');
    try {
        let session;
        const openResponse = await authFetch(`/api/cash-sessions/open/${encodeURIComponent(terminalId)}`);
        if (openResponse.ok) {
            session = await openResponse.json();
        } else if (openResponse.status === 404) {
            session = await fetchJson('/api/cash-sessions', {
                method: 'POST',
                body: JSON.stringify({ terminalId, operatorName, openingAmount })
            });
        } else {
            throw new Error(await readApiError(openResponse));
        }

        state.cashSessionId = session.id;
        state.operatorName = session.operatorName;
        state.terminalId = session.terminalId;
        refreshSessionHeader();
        showScreen('app-container');
        setActiveTab('dashboard-tab');
    } catch (error) {
        showToast(error.message, 'error');
    } finally {
        setBusy(button, false);
    }
};

const bindEvents = () => {
    $('#loginForm').addEventListener('submit', login);
    $('#openRegisterForm').addEventListener('submit', openRegister);
    $$('.tab-btn').forEach((button) => button.addEventListener('click', () => setActiveTab(button.dataset.tab)));

    $('#btnRefreshDashboard').addEventListener('click', loadDashboard);
    $('#btnScan').addEventListener('click', addProductToCart);
    $('#barcodeInput').addEventListener('keydown', (event) => {
        if (event.key === 'Enter') addProductToCart();
    });
    $$('.chip[data-barcode]').forEach((button) => {
        button.addEventListener('click', () => {
            $('#barcodeInput').value = button.dataset.barcode;
            addProductToCart();
        });
    });

    $('#btnCheckout').addEventListener('click', () => {
        if (!state.cart.length) {
            showToast('Carrinho vazio.', 'error');
            return;
        }
        $('#btnCheckout').classList.add('hidden');
        $('#paymentSection').classList.remove('hidden');
    });
    $('#btnCancelCheckout').addEventListener('click', () => {
        $('#paymentSection').classList.add('hidden');
        $('#btnCheckout').classList.remove('hidden');
    });
    $$('.pay-method').forEach((button) => {
        button.addEventListener('click', () => {
            $$('.pay-method').forEach((item) => item.classList.remove('active'));
            button.classList.add('active');
            state.paymentMethod = button.dataset.method;
        });
    });
    $('#btnFinalize').addEventListener('click', finalizeSale);

    $('#btnCatalogSearch').addEventListener('click', () => {
        state.catalogSearch = $('#catalogSearch').value.trim();
        loadCatalog(1);
    });
    $('#catalogSearch').addEventListener('keydown', (event) => {
        if (event.key === 'Enter') $('#btnCatalogSearch').click();
    });
    $('#btnCatalogPrev').addEventListener('click', () => loadCatalog(state.catalogPage - 1));
    $('#btnCatalogNext').addEventListener('click', () => loadCatalog(state.catalogPage + 1));

    $('#btnRefreshHistory').addEventListener('click', () => loadHistory(state.historyPage));
    $('#btnHistoryPrev').addEventListener('click', () => loadHistory(state.historyPage - 1));
    $('#btnHistoryNext').addEventListener('click', () => loadHistory(state.historyPage + 1));
    $('#btnCloseCash').addEventListener('click', closeCashSession);

    $('#btnSupplierSearch').addEventListener('click', () => {
        state.supplierSearch = $('#supplierSearch').value.trim();
        loadSuppliers(1);
    });
    $('#supplierSearch').addEventListener('keydown', (event) => {
        if (event.key === 'Enter') $('#btnSupplierSearch').click();
    });
    $('#btnSuppliersPrev').addEventListener('click', () => loadSuppliers(state.suppliersPage - 1));
    $('#btnSuppliersNext').addEventListener('click', () => loadSuppliers(state.suppliersPage + 1));
    $('#supplierForm').addEventListener('submit', saveSupplier);
    $('#btnClearSupplier').addEventListener('click', clearSupplierForm);

    $('#btnAddPurchaseItem').addEventListener('click', addPurchaseItem);
    $('#purchaseBarcode').addEventListener('keydown', (event) => {
        if (event.key === 'Enter') addPurchaseItem();
    });
    $('#btnConfirmPurchase').addEventListener('click', confirmPurchaseEntry);
    $('#btnRefreshPurchases').addEventListener('click', loadPurchaseEntries);
    $('#btnLoadReports').addEventListener('click', loadReports);
};

document.addEventListener('DOMContentLoaded', () => {
    $('#reportFrom').value = todayInputValue();
    $('#reportTo').value = todayInputValue();
    refreshSessionHeader();
    renderCart();
    renderPurchaseItems();
    bindEvents();
});
