const API_URL = 'http://localhost:5235';

let state = {
    token: null,
    cashSessionId: null,
    operatorName: '',
    cart: [], // { productId, barcode, name, price, quantity }
    paymentMethod: 'Cash'
};

document.addEventListener('DOMContentLoaded', () => {
    // Referências do DOM
    const loginScreen = document.getElementById('login-screen');
    const setupScreen = document.getElementById('setup-screen');
    const pdvScreen = document.getElementById('pdv-screen');
    const operatorBadge = document.getElementById('lblOperator');
    const lblSubtotal = document.getElementById('lblSubtotal');
    const lblTotal = document.getElementById('lblTotal');
    const cartItemsList = document.getElementById('cartItems');
    const barcodeInput = document.getElementById('barcodeInput');
    const paymentSection = document.getElementById('paymentSection');
    const checkoutAction = document.getElementById('checkoutAction');
    const btnCheckout = document.getElementById('btnCheckout');
    
    // Utilitário de formatação de moeda
    const formatCurrency = (val) => val.toLocaleString('pt-BR', {style: 'currency', currency: 'BRL'});

    // 0. Login
    document.getElementById('btnLogin').addEventListener('click', async () => {
        const usernameInput = document.getElementById('username').value.trim();
        const passwordInput = document.getElementById('password').value.trim();

        if (!usernameInput || !passwordInput) {
            return alert('Preencha usuário e senha!');
        }

        const btnLogin = document.getElementById('btnLogin');
        btnLogin.disabled = true;
        btnLogin.textContent = 'Autenticando...';

        try {
            const res = await fetch(`${API_URL}/api/auth/login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username: usernameInput, password: passwordInput })
            });

            if (!res.ok) {
                throw new Error('Credenciais inválidas!');
            }

            const data = await res.json();
            state.token = data.token; // Armazena token JWT
            
            // Vai para a tela de setup de caixa
            loginScreen.classList.remove('active');
            setupScreen.classList.add('active');
        } catch (e) {
            alert(e.message);
        } finally {
            btnLogin.disabled = false;
            btnLogin.textContent = 'Entrar no Sistema';
        }
    });

    // 1. Abrir Sessão do Caixa
    document.getElementById('btnOpenRegister').addEventListener('click', async () => {
        const operatorName = document.getElementById('operatorName').value.trim();
        const openingAmount = parseFloat(document.getElementById('openingAmount').value);
        
        if(!operatorName || isNaN(openingAmount)) {
            return alert('Por favor, preencha o operador e o valor inicial do caixa.');
        }

        const btnOpen = document.getElementById('btnOpenRegister');
        btnOpen.disabled = true;
        btnOpen.textContent = "Abrindo...";
        
        try {
            let sessionId = null;
            let finalOperatorName = operatorName;
            
            // 1. Tenta recuperar se já existe um caixa aberto para o PDV-01
            const checkRes = await fetch(`${API_URL}/api/cash-sessions/open/PDV-01`, {
                headers: { 'Authorization': `Bearer ${state.token}` }
            });
            
            // Se retornou OK (200), já existe caixa
            if (checkRes.ok) {
                const sessionData = await checkRes.json();
                sessionId = sessionData.id;
                finalOperatorName = sessionData.operatorName || operatorName;
                console.log("Caixa preexistente recuperado.");
            } else {
                // 2. Tenta criação normal de um novo caixa
                const res = await fetch(`${API_URL}/api/cash-sessions`, {
                    method: 'POST',
                    headers: { 
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${state.token}`
                    },
                    body: JSON.stringify({ 
                        terminalId: 'PDV-01', 
                        operatorName, 
                        openingAmount 
                    })
                });

                if(!res.ok) {
                    const err = await res.text();
                    throw new Error(err || 'Erro desconhecido ao abrir caixa');
                }
                
                sessionId = await res.json(); 
            }
            
            state.cashSessionId = sessionId; 
            state.operatorName = finalOperatorName;
            
            // Troca de Telas
            setupScreen.classList.remove('active');
            pdvScreen.classList.add('active');
            operatorBadge.textContent = state.operatorName;
            
            // Foca no input principal
            barcodeInput.focus();
        } catch (e) {
            console.error('Falha de conexão com a API:', e);
            alert(`Erro ao tentar abrir caixa: ${e.message}\nVerifique se o backend está rodando em http://localhost:5235.`);
        } finally {
            btnOpen.disabled = false;
            btnOpen.textContent = "Abrir Sessão de Caixa";
        }
    });

    // 2. Escanear e Adicionar Produto
    const addProduct = async () => {
        const barcode = barcodeInput.value.trim();
        if(!barcode) return;
        
        const btnScan = document.getElementById('btnScan');
        btnScan.disabled = true;
        barcodeInput.disabled = true;

        try {
            // Tenta achar Produto no Carrinho (Incrementar direto)
            const existing = state.cart.find(i => i.barcode === barcode);
            if(existing) {
                existing.quantity += 1;
            } else {
                // Requisição Backend
                const res = await fetch(`${API_URL}/api/products/barcode/${barcode}`, {
                    headers: { 'Authorization': `Bearer ${state.token}` }
                });
                if(!res.ok) {
                    if (res.status === 404) throw new Error('Produto não encontrado! (Verifique o código)');
                    throw new Error('Falha na consulta do produto.');
                }
                
                const product = await res.json();
                state.cart.push({
                    productId: product.id,
                    barcode: product.barcode,
                    name: product.name,
                    price: product.unitPrice, // Corrigido de price para unitPrice
                    quantity: 1
                });
            }
            
            // Limpa após Sucesso
            barcodeInput.value = '';
            renderCart();
            
            // Volta para a tela de Produtos caso estivesse em Pagamento
            resetToScanMode();

        } catch (e) {
            alert(e.message);
        } finally {
            btnScan.disabled = false;
            barcodeInput.disabled = false;
            barcodeInput.focus();
        }
    };

    document.getElementById('btnScan').addEventListener('click', addProduct);
    
    barcodeInput.addEventListener('keypress', (e) => {
        if(e.key === 'Enter') addProduct();
    });

    document.querySelectorAll('.btn-quick-add').forEach(btn => {
        btn.addEventListener('click', () => {
            barcodeInput.value = btn.dataset.barcode;
            addProduct();
        });
    });

    // 3. Renderizar Carrinho
    const renderCart = () => {
        cartItemsList.innerHTML = '';
        let total = 0;
        
        state.cart.forEach(item => {
            const itemTotal = item.price * item.quantity;
            total += itemTotal;
            
            const li = document.createElement('li');
            li.className = 'cart-item';
            li.innerHTML = `
                <span>${item.name}<br><small style="color:#94a3b8;font-size:0.8rem">${item.barcode}</small></span>
                <span>${item.quantity}</span>
                <span class="price">${formatCurrency(item.price)}</span>
                <span class="total">${formatCurrency(itemTotal)}</span>
            `;
            cartItemsList.appendChild(li);
        });
        
        lblSubtotal.textContent = formatCurrency(total);
        lblTotal.textContent = formatCurrency(total);
        
        // Auto scroll
        cartItemsList.scrollTop = cartItemsList.scrollHeight;
    };

    // 4. Fluxo de Pagamento
    const resetToScanMode = () => {
        paymentSection.classList.add('hidden');
        checkoutAction.classList.remove('hidden');
    }

    btnCheckout.addEventListener('click', () => {
        if(state.cart.length === 0) return alert('Carrinho vazio! Escaneie itens antes de cobrar.');
        checkoutAction.classList.add('hidden');
        paymentSection.classList.remove('hidden');
    });

    // Selecionar Forma de Pagamento
    document.querySelectorAll('.pay-method').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.pay-method').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            state.paymentMethod = btn.dataset.method;
        });
    });

    // 5. Finalizar Venda
    document.getElementById('btnFinalize').addEventListener('click', async () => {
        const totalAmount = state.cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);
        
        const payload = {
            cashSessionId: state.cashSessionId,
            operatorName: state.operatorName,
            customerDocument: null,     // Pode criar um campo de input se desejar CPF na nota
            saleDiscountTotal: 0,
            issueFiscalDocument: true,  // Vamos simular a emissão
            items: state.cart.map(i => ({
                barcode: i.barcode,
                quantity: i.quantity,
                unitDiscount: 0
            })),
            payments: [{
                method: state.paymentMethod,
                amount: totalAmount
            }]
        };

        const btnFinalize = document.getElementById('btnFinalize');
        btnFinalize.disabled = true;
        btnFinalize.textContent = "Processando...";

        try {
            const res = await fetch(`${API_URL}/api/sales`, {
                method: 'POST',
                headers: { 
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${state.token}`
                },
                body: JSON.stringify(payload)
            });

            if(!res.ok) {
                const errResult = await res.text();
                throw new Error(errResult || 'Falha catastrófica na API');
            }

            const saleResult = await res.json();
            console.log("Comprovante Venda: ", saleResult);
            
            alert(`✅ Venda #${saleResult.number} finalizada com sucesso!\nTotal Arrecadado: ${formatCurrency(saleResult.paidAmount)}`);
            
            // Reseta para o proximo cliente
            state.cart = [];
            renderCart();
            resetToScanMode();
            barcodeInput.focus();

        } catch (e) {
            console.error(e);
            alert('Falha na venda: ' + e.message);
        } finally {
            btnFinalize.disabled = false;
            btnFinalize.textContent = "Finalizar Venda";
        }
    });
});
