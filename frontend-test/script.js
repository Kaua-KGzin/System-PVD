const API_URL = window.PDV_API_URL || 'http://localhost:5235';

const state = {
    token: null,
    refreshToken: null,
    cashSessionId: null,
    operatorName: '',
    terminalId: 'PDV-01',
    cart: [],
    paymentMethod: 'Cash',
    catalogPage: 1,
    catalogSearch: '',
    catalogItems: [],
    historyPage: 1,
    suppliersPage: 1,
    supplierSearch: '',
    suppliers: [],
    purchaseItems: [],
    lastSalesSummary: null,
    dashboardTimer: null,
    expectedClosingAmount: 0
};

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => Array.from(document.querySelectorAll(selector));

const STORAGE_KEY = 'pdv.frontend.v4';

const saveSession = () => {
    const snapshot = {
        token: state.token,
        refreshToken: state.refreshToken,
        cashSessionId: state.cashSessionId,
        operatorName: state.operatorName,
        terminalId: state.terminalId,
        cart: state.cart
    };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot));
};

const restoreSession = () => {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        if (!raw) return;
        const snapshot = JSON.parse(raw);
        state.token = snapshot.token || null;
        state.refreshToken = snapshot.refreshToken || null;
        state.cashSessionId = snapshot.cashSessionId || null;
        state.operatorName = snapshot.operatorName || '';
        state.terminalId = snapshot.terminalId || 'PDV-01';
        state.cart = Array.isArray(snapshot.cart) ? snapshot.cart : [];
        $('#operatorName').value = state.operatorName;
        $('#terminalId').value = state.terminalId;
    } catch {
        localStorage.removeItem(STORAGE_KEY);
    }
};

const clearSession = () => {
    localStorage.removeItem(STORAGE_KEY);
    state.token = null;
    state.refreshToken = null;
    state.cashSessionId = null;
    state.cart = [];
};

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

const getCartTotals = () => {
    const gross = state.cart.reduce((sum, item) => sum + item.price * item.quantity, 0);
    const itemDiscounts = state.cart.reduce((sum, item) => sum + (Number(item.unitDiscount) || 0) * item.quantity, 0);
    const saleDiscount = Number($('#saleDiscount')?.value) || 0;
    const net = Math.max(0, gross - itemDiscounts - saleDiscount);
    return { gross, itemDiscounts, saleDiscount, net };
};

const getPayments = () =>
    $$('[data-payment-method]')
        .map((input) => ({
            method: input.dataset.paymentMethod,
            amount: Number(input.value) || 0
        }))
        .filter((payment) => payment.amount > 0);

const getPaidAmount = () => getPayments().reduce((sum, payment) => sum + payment.amount, 0);

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
        clearSession();
        refreshSessionHeader();
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
    td.className = className ? `p-3 border-b border-slate-100 ${className}` : 'p-3 border-b border-slate-100';
    if (content instanceof Node) {
        td.appendChild(content);
    } else {
        td.textContent = content ?? '';
    }
    return td;
};

const appendRow = (tbody, cells, className) => {
    const tr = document.createElement('tr');
    tr.className = className ? `hover:bg-slate-50 transition-colors ${className}` : 'hover:bg-slate-50 transition-colors group';
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
    window.clearInterval(state.dashboardTimer);
    state.dashboardTimer = tabId === 'dashboard-tab'
        ? window.setInterval(loadDashboard, 60000)
        : null;

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
    if (state.token) saveSession();
};

const renderCart = () => {
    const cartItems = $('#cartItems');
    cartItems.replaceChildren();

    let count = 0;

    state.cart.forEach((item, index) => {
        const unitDiscount = Number(item.unitDiscount) || 0;
        const itemTotal = Math.max(0, (item.price - unitDiscount) * item.quantity);
        count += item.quantity;

        const row = document.createElement('li');
        row.className = 'px-md py-3 grid grid-cols-12 gap-2 items-center text-sm group hover:bg-slate-50 cursor-pointer rounded-lg mx-2 my-1 border border-transparent hover:border-slate-200 transition-colors';

        const productCell = document.createElement('span');
        productCell.className = 'col-span-4 flex flex-col justify-center';
        
        const name = document.createElement('strong');
        name.className = 'font-bold text-slate-900 truncate pr-2 tracking-tight block';
        name.textContent = item.name;
        
        const code = document.createElement('small');
        code.className = 'bg-indigo-50 text-indigo-700 text-[10px] px-1 rounded font-bold w-max mt-1 block';
        code.textContent = item.barcode;
        productCell.append(name, code);

        const quantityCell = document.createElement('span');
        quantityCell.className = 'col-span-2 px-2 flex justify-center';

        const qty = document.createElement('input');
        qty.className = 'cart-input text-center';
        qty.type = 'number';
        qty.min = '0.001';
        qty.step = '0.001';
        qty.value = String(item.quantity);
        qty.addEventListener('input', () => {
            const next = Number(qty.value);
            if (Number.isNaN(next) || next <= 0) return;
            item.quantity = next;
            renderCart();
        });
        quantityCell.appendChild(qty);

        const priceCell = document.createElement('span');
        priceCell.className = 'col-span-2 text-right text-slate-500 flex items-center justify-end font-medium';
        priceCell.textContent = formatCurrency(item.price);

        const discountCell = document.createElement('span');
        discountCell.className = 'col-span-2 px-2 flex justify-center';
        const discount = document.createElement('input');
        discount.className = 'cart-input text-center';
        discount.type = 'number';
        discount.min = '0';
        discount.step = '0.01';
        discount.value = String(unitDiscount);
        discount.addEventListener('input', () => {
            item.unitDiscount = Math.max(0, Number(discount.value) || 0);
            renderCart();
        });
        discountCell.appendChild(discount);
        
        const totalCell = document.createElement('span');
        totalCell.className = 'col-span-2 text-right font-bold text-slate-900 pr-2 flex items-center justify-end';
        totalCell.textContent = formatCurrency(itemTotal);

        const removeCell = document.createElement('button');
        removeCell.type = 'button';
        removeCell.className = 'text-slate-400 hover:text-red-600 absolute right-2 top-2 opacity-0 group-hover:opacity-100 transition-opacity';
        removeCell.innerHTML = '<span class="material-symbols-outlined text-[18px]">close</span>';
        removeCell.addEventListener('click', () => {
            state.cart.splice(index, 1);
            renderCart();
        });
        row.style.position = 'relative';
        row.append(productCell, quantityCell, priceCell, discountCell, totalCell, removeCell);
        cartItems.appendChild(row);
    });

    if (state.cart.length === 0) {
        const empty = document.createElement('li');
        empty.className = 'empty-note p-8';
        empty.textContent = 'Abertura rápida com enter ou bipagem (Nenhum produto)';
        cartItems.appendChild(empty);
    }

    $('#lblItemCount').textContent = String(count);
    updatePaymentSummary();
    saveSession();
};

const updatePaymentSummary = () => {
    const totals = getCartTotals();
    const paid = getPaidAmount();
    const remaining = Math.max(0, totals.net - paid);
    const change = Math.max(0, paid - totals.net);
    $('#lblTotal').textContent = formatCurrency(totals.net);
    $('#lblPaid').textContent = formatCurrency(paid);
    $('#lblRemaining').textContent = formatCurrency(remaining);
    $('#lblChange').textContent = formatCurrency(change);
    $('#btnFinalize').disabled = totals.net <= 0 || paid < totals.net;
};

const fillSinglePayment = (method) => {
    $$('[data-payment-method]').forEach((input) => {
        input.value = input.dataset.paymentMethod === method ? getCartTotals().net.toFixed(2) : '0';
    });
    state.paymentMethod = method;
    updatePaymentSummary();
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
                quantity: 1,
                unitDiscount: 0
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
    const totals = getCartTotals();
    const payments = getPayments();
    const paidAmount = payments.reduce((sum, payment) => sum + payment.amount, 0);

    if (!state.cashSessionId) {
        showToast('Abra um caixa antes de vender.', 'error');
        return;
    }

    if (totals.net <= 0) {
        showToast('Carrinho vazio.', 'error');
        return;
    }

    if (paidAmount < totals.net) {
        showToast('Pagamento insuficiente.', 'error');
        return;
    }

    const payload = {
        cashSessionId: state.cashSessionId,
        operatorName: state.operatorName,
        customerDocument: $('#customerDocument').value.trim() || null,
        saleDiscountTotal: totals.saleDiscount,
        issueFiscalDocument: true,
        items: state.cart.map((item) => ({
            barcode: item.barcode,
            quantity: item.quantity,
            unitDiscount: Number(item.unitDiscount) || 0
        })),
        payments
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
        $('#saleDiscount').value = '0';
        $('#customerDocument').value = '';
        $$('[data-payment-method]').forEach((input) => input.value = '0');
        renderCart();
        $('#paymentSection').classList.add('hidden');
        $('#btnCheckout').classList.remove('hidden');
        loadHistory(1);
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
        const from = todayInputValue();
        const reportQuery = new URLSearchParams({
            from: makeDateTimeOffset(from),
            to: makeDateTimeOffset(from, true)
        });
        const [dashboard, stockAlerts, summary] = await Promise.all([
            fetchJson('/api/dashboard'),
            fetchJson('/api/reports/stock-alerts'),
            fetchJson(`/api/reports/sales-summary?${reportQuery}`)
        ]);

        $('#kpiSalesCount').textContent = dashboard.todaySales.count;
        $('#kpiSalesTotal').textContent = formatCurrency(dashboard.todaySales.total);
        $('#kpiAvgTicket').textContent = formatCurrency(dashboard.todaySales.averageTicket);
        $('#kpiOpenCashSessions').textContent = dashboard.openCashSessions;
        $('#kpiLowStock').textContent = dashboard.lowStockProducts;
        renderSalesByHour(summary.byHour || [], '#dashboardSalesByHour');

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
    setTableMessage(tbody, 7, 'Carregando...');

    try {
        const query = new URLSearchParams({ page, pageSize: 15 });
        if (state.catalogSearch) query.set('search', state.catalogSearch);
        const data = await fetchJson(`/api/products?${query}`);

        state.catalogPage = data.page;
        state.catalogItems = data.items;
        tbody.replaceChildren();
        if (!data.items.length) {
            setTableMessage(tbody, 7, 'Nenhum produto encontrado.');
        } else {
            data.items.forEach((product) => {
                const actions = document.createElement('div');
                actions.className = 'flex justify-end gap-2';
                const edit = document.createElement('button');
                edit.type = 'button';
                edit.className = 'px-3 py-1 border border-slate-300 rounded text-slate-600 text-xs font-bold hover:bg-slate-50';
                edit.textContent = 'Editar';
                edit.addEventListener('click', () => openProductModal(product));
                const adjust = document.createElement('button');
                adjust.type = 'button';
                adjust.className = 'px-3 py-1 border border-amber-300 rounded text-amber-700 text-xs font-bold hover:bg-amber-50';
                adjust.textContent = 'Ajustar';
                adjust.addEventListener('click', () => openProductModal(product, true));
                actions.append(edit, adjust);

                appendRow(tbody, [
                    product.name,
                    product.barcode,
                    product.unitOfMeasure,
                    { value: formatCurrency(product.unitPrice), className: 'money' },
                    product.stockQuantity,
                    product.minStockQuantity,
                    actions
                ]);
            });
        }
        setPagination($('#catalogPageInfo'), $('#btnCatalogPrev'), $('#btnCatalogNext'), data);
    } catch (error) {
        setTableMessage(tbody, 7, error.message, 'error-cell');
    }
};

const openProductModal = (product = null, focusAdjustment = false) => {
    $('#productForm').reset();
    $('#productId').value = product?.id || '';
    $('#productFormTitle').textContent = product ? 'Editar produto' : 'Novo produto';
    $('#productName').value = product?.name || '';
    $('#productBarcode').value = product?.barcode || '';
    $('#productSku').value = product?.sku || '';
    $('#productUnit').value = product?.unitOfMeasure || 'UN';
    $('#productPrice').value = product?.unitPrice ?? '';
    $('#productStock').value = product?.stockQuantity ?? 0;
    $('#productStock').disabled = Boolean(product);
    $('#productMinStock').value = product?.minStockQuantity ?? 0;
    $('#productActive').checked = product ? Boolean(product.isActive) : true;
    $('#stockDelta').value = '';
    $('#stockReason').value = '';
    $('#btnAdjustStock').disabled = !product;
    $('#stockAdjustmentBox').classList.toggle('hidden', !product);
    $('#productModal').classList.remove('hidden');
    (focusAdjustment ? $('#stockDelta') : $('#productName')).focus();
};

const closeProductModal = () => {
    $('#productModal').classList.add('hidden');
};

const saveProduct = async (event) => {
    event.preventDefault();
    const id = $('#productId').value;
    const payload = {
        barcode: $('#productBarcode').value.trim(),
        sku: $('#productSku').value.trim() || null,
        name: $('#productName').value.trim(),
        unitOfMeasure: $('#productUnit').value.trim() || 'UN',
        unitPrice: Number($('#productPrice').value),
        minStockQuantity: Number($('#productMinStock').value),
        isActive: $('#productActive').checked
    };

    if (!payload.barcode || !payload.name || Number.isNaN(payload.unitPrice) || Number.isNaN(payload.minStockQuantity)) {
        showToast('Preencha produto, codigo, preco e estoque minimo.', 'error');
        return;
    }

    const button = $('#btnSaveProduct');
    setBusy(button, true, 'Salvando...');
    try {
        if (id) {
            await fetchJson(`/api/products/${id}`, {
                method: 'PUT',
                body: JSON.stringify(payload)
            });
        } else {
            await fetchJson('/api/products', {
                method: 'POST',
                body: JSON.stringify({
                    ...payload,
                    stockQuantity: Number($('#productStock').value) || 0
                })
            });
        }

        showToast('Produto salvo.');
        closeProductModal();
        loadCatalog(state.catalogPage);
        loadDashboard();
    } catch (error) {
        showToast(error.message, 'error');
    } finally {
        setBusy(button, false);
    }
};

const adjustProductStock = async () => {
    const id = $('#productId').value;
    const quantityDelta = Number($('#stockDelta').value);
    const reason = $('#stockReason').value.trim();
    if (!id || Number.isNaN(quantityDelta) || quantityDelta === 0) {
        showToast('Informe um delta de estoque diferente de zero.', 'error');
        return;
    }

    const button = $('#btnAdjustStock');
    setBusy(button, true, 'Ajustando...');
    try {
        await fetchJson(`/api/products/${id}/stock-adjustments`, {
            method: 'POST',
            body: JSON.stringify({ quantityDelta, reason: reason || null })
        });
        showToast('Estoque ajustado.');
        closeProductModal();
        loadCatalog(state.catalogPage);
        loadDashboard();
    } catch (error) {
        showToast(error.message, 'error');
    } finally {
        setBusy(button, false);
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
                cancelButton.className = 'px-3 py-1 bg-red-50 text-red-600 rounded text-xs font-bold hover:bg-red-100 transition-colors disabled:opacity-50 disabled:grayscale';
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
    if (!state.cashSessionId) {
        $('#summaryOpening').textContent = formatCurrency(0);
        $('#summaryExpected').textContent = formatCurrency(0);
        $('#summaryDifference').textContent = formatCurrency(0);
        return;
    }

    try {
        const session = await fetchJson(`/api/cash-sessions/${state.cashSessionId}`);
        state.expectedClosingAmount = Number(session.expectedClosingAmount) || 0;
        $('#summaryOpening').textContent = formatCurrency(session.openingAmount);
        $('#summaryExpected').textContent = formatCurrency(session.expectedClosingAmount);
        updateClosingDifference();
    } catch (error) {
        showToast(error.message, 'error');
    }
};

const updateClosingDifference = () => {
    const counted = Number($('#closingAmount').value) || 0;
    const difference = counted - (Number(state.expectedClosingAmount) || 0);
    const target = $('#summaryDifference');
    target.textContent = formatCurrency(difference);
    target.className = difference === 0 ? 'text-lg text-green-700' : 'text-lg text-red-600';
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
        state.expectedClosingAmount = 0;
        $('#closingAmount').value = '';
        $('#closingNotes').value = '';
        refreshSessionHeader();
        renderCart();
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
                editButton.className = 'px-3 py-1 border border-slate-300 rounded text-slate-600 text-xs font-bold hover:bg-slate-50 transition-colors';
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
        remove.className = 'px-3 py-1 border border-slate-300 rounded text-error text-[10px] font-bold hover:bg-red-50 hover:border-error transition-colors uppercase';
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
        const terminalId = $('#reportTerminal').value.trim();
        if (terminalId) query.set('terminalId', terminalId);

        const [summary, movements] = await Promise.all([
            fetchJson(`/api/reports/sales-summary?${query}`),
            fetchJson('/api/reports/inventory-movements?page=1&pageSize=20')
        ]);

        state.lastSalesSummary = summary;
        $('#reportSalesCount').textContent = summary.totalSales;
        $('#reportNetRevenue').textContent = formatCurrency(summary.netRevenue);
        $('#reportDiscounts').textContent = formatCurrency(summary.totalDiscounts);
        renderSalesByHour(summary.byHour || [], '#salesByHour');
        renderPaymentBreakdown(summary.byPaymentMethod || []);
        renderInventoryMovements(movements.items || []);
    } catch (error) {
        showToast(error.message, 'error');
    }
};

const renderSalesByHour = (entries, selector = '#salesByHour') => {
    const container = $(selector);
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

const renderPaymentBreakdown = (items) => {
    const tbody = $('#paymentBreakdownBody');
    tbody.replaceChildren();
    if (!items.length) {
        setTableMessage(tbody, 3, 'Sem pagamentos no periodo.');
        return;
    }

    items.forEach((item) => appendRow(tbody, [
        item.method,
        { value: item.count, className: 'text-center' },
        { value: formatCurrency(item.total), className: 'money text-right' }
    ]));
};

const exportReportsCsv = () => {
    if (!state.lastSalesSummary) {
        showToast('Carregue o relatorio antes de exportar.', 'warning');
        return;
    }

    const rows = [
        ['Metrica', 'Valor'],
        ['Total de vendas', state.lastSalesSummary.totalSales],
        ['Receita total', state.lastSalesSummary.totalRevenue],
        ['Descontos', state.lastSalesSummary.totalDiscounts],
        ['Receita liquida', state.lastSalesSummary.netRevenue],
        [],
        ['Metodo', 'Quantidade', 'Total'],
        ...(state.lastSalesSummary.byPaymentMethod || []).map((item) => [item.method, item.count, item.total])
    ];

    const csv = rows.map((row) => row.map((cell) => `"${String(cell ?? '').replace(/"/g, '""')}"`).join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `relatorio-pdv-${todayInputValue()}.csv`;
    link.click();
    URL.revokeObjectURL(url);
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
        // Backend .NET pode serializar em PascalCase ou camelCase dependendo da configuração
        state.token = data.token ?? data.Token;
        state.refreshToken = data.refreshToken ?? data.RefreshToken ?? null;
        const resolvedUsername = data.username ?? data.Username ?? '';
        state.operatorName = resolvedUsername;
        $('#operatorName').value = resolvedUsername;
        saveSession();
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
        saveSession();
        showScreen('app-container');
        setActiveTab('dashboard-tab');
    } catch (error) {
        showToast(error.message, 'error');
    } finally {
        setBusy(button, false);
    }
};

const bindShortcuts = () => {
    document.addEventListener('keydown', (event) => {
        if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement) {
            if (event.key !== 'Escape' && !event.key.startsWith('F')) return;
        }

        const map = {
            F1: () => setActiveTab('dashboard-tab'),
            F2: () => setActiveTab('pdv-tab'),
            F9: () => $('#btnCheckout').click(),
            F10: () => $('#btnFinalize').click(),
            Escape: () => {
                closeProductModal();
                $('#paymentSection').classList.add('hidden');
                $('#btnCheckout').classList.remove('hidden');
            }
        };

        if (map[event.key]) {
            event.preventDefault();
            map[event.key]();
        }
    });
};

const bootstrapProtectedState = () => {
    restoreSession();
    refreshSessionHeader();
    renderCart();
    renderPurchaseItems();

    if (!state.token) {
        showScreen('login-screen');
        return;
    }

    if (state.cashSessionId) {
        showScreen('app-container');
        setActiveTab('dashboard-tab');
    } else {
        showScreen('setup-screen');
    }
};

const bindEvents = () => {
    $('#loginForm').addEventListener('submit', login);
    $('#openRegisterForm').addEventListener('submit', openRegister);
    $$('.tab-btn').forEach((button) => button.addEventListener('click', () => setActiveTab(button.dataset.tab)));

    $('#btnRefreshDashboard').addEventListener('click', loadDashboard);
    $('#btnDashboardPdv').addEventListener('click', () => setActiveTab('pdv-tab'));
    $('#btnDashboardCash').addEventListener('click', () => setActiveTab('cash-tab'));
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
            fillSinglePayment(button.dataset.method);
        });
    });
    $$('[data-payment-method]').forEach((input) => input.addEventListener('input', updatePaymentSummary));
    $('#saleDiscount').addEventListener('input', renderCart);
    $('#btnFinalize').addEventListener('click', finalizeSale);

    $('#btnNewProduct').addEventListener('click', () => openProductModal());
    $('#btnCloseProductModal').addEventListener('click', closeProductModal);
    $('#btnCancelProduct').addEventListener('click', closeProductModal);
    $('#productForm').addEventListener('submit', saveProduct);
    $('#btnAdjustStock').addEventListener('click', adjustProductStock);
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
    $('#closingAmount').addEventListener('input', updateClosingDifference);
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
    $('#btnExportReports').addEventListener('click', exportReportsCsv);
};

document.addEventListener('DOMContentLoaded', () => {
    $('#reportFrom').value = todayInputValue();
    $('#reportTo').value = todayInputValue();
    bindEvents();
    bindShortcuts();
    bootstrapProtectedState();
});
