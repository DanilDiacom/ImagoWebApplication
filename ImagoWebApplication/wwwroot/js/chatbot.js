// Чат-бот IMAGO (Views/Shared/_ChatWidget.cshtml, Controllers/ChatbotController.cs)
// Переписка хранится на сервере (7 дней); здесь — только копия для текущей вкладки.
(function () {
    var root = document.getElementById('ic-chat');
    if (!root) return;

    var T = window.icChatTexts || {};
    var toggle = root.querySelector('.ic-chat-toggle');
    var panel = root.querySelector('.ic-chat-panel');
    var list = root.querySelector('.ic-chat-messages');
    var inputForm = root.querySelector('.ic-chat-input');
    var textarea = inputForm.querySelector('textarea');
    var contactForm = root.querySelector('.ic-chat-contact');
    var confirmBox = root.querySelector('.ic-chat-confirm');
    var urls = {
        message: root.dataset.urlMessage,
        contact: root.dataset.urlContact,
        history: root.dataset.urlHistory,
        reset: root.dataset.urlNew,
        send: root.dataset.urlSend
    };

    var STORE = 'imagoChat';
    var POLL_MS = 20000;          // пока ждём ответ менеджера из Telegram
    var state = load();
    var busy = false;
    var pollTimer = null;
    var badge = root.querySelector('.ic-chat-badge');

    function emptyState() {
        return { loaded: false, messages: [], open: false, askContact: false, askConfirm: false, hasContact: false, waiting: false, unread: false };
    }

    function load() {
        try {
            var s = JSON.parse(sessionStorage.getItem(STORE) || 'null');
            if (s && Array.isArray(s.messages)) return s;
        } catch (e) { }
        return emptyState();
    }

    function save() {
        try { sessionStorage.setItem(STORE, JSON.stringify(state)); } catch (e) { }
    }

    // Адрес страницы для бота: «/Home/Školení», а не «/Home/%C5%A0kolen%C3%AD»
    function currentPage() {
        var path = location.pathname;
        try { path = decodeURIComponent(path); } catch (e) { }
        return path + location.search;
    }

    function esc(s) {
        var d = document.createElement('div');
        d.textContent = s == null ? '' : String(s);
        return d.innerHTML;
    }

    // Безопасный вывод: экранируем HTML, разрешаем только **жирный**, переносы и ссылки на наш сайт
    function format(text) {
        var html = esc(text);
        html = html.replace(/\[([^\]]+)\]\((\/(?!\/)[^)\s]*)\)/g, function (m, label, url) {
            return '<a href="' + url + '">' + label + '</a>';
        });
        html = html.replace(/\*\*([^*]+)\*\*/g, '<b>$1</b>');
        return html.replace(/\n/g, '<br>');
    }

    function addBubble(role, text, extraClass) {
        var div = document.createElement('div');
        div.className = 'ic-msg ic-msg-' + role + (extraClass ? ' ' + extraClass : '');
        div.innerHTML = role === 'user' ? esc(text).replace(/\n/g, '<br>') : format(text);
        if (role === 'staff') {
            // ответ менеджера из Telegram — с подписью
            div.innerHTML = '<div class="ic-msg-staff-label">' + esc(T.Chatbot_StaffLabel) + '</div>' + div.innerHTML;
        }
        list.appendChild(div);
        list.scrollTop = list.scrollHeight;
        return div;
    }

    function updateContactForm() {
        contactForm.hidden = !(state.askContact && !state.hasContact);
        // кнопка «Předat dotaz»: вопросы ждут отправки; если контактов нет — после нажатия появится форма контактов
        if (confirmBox) confirmBox.hidden = !(state.askConfirm && contactForm.hidden);
    }

    function render() {
        list.innerHTML = '';
        addBubble('bot', T.Chatbot_Greeting);
        state.messages.forEach(function (m) { addBubble(m.role, m.text, m.error ? 'ic-msg-error' : ''); });
        updateContactForm();
        panel.hidden = !state.open;
        root.classList.toggle('ic-chat-open', state.open);
        updateBadge();
    }

    function updateBadge() {
        if (badge) badge.hidden = !state.unread;
    }

    function applyHistory(h) {
        var before = state.messages.length;
        var hadStaff = state.messages.filter(function (m) { return m.role === 'staff'; }).length;
        state.loaded = true;
        state.messages = (h.messages || []).map(function (m) { return { role: m.role, text: m.text }; });
        state.askContact = !!h.askContact;
        state.hasContact = !!h.hasContact;
        state.askConfirm = !!h.askConfirm;
        state.waiting = !!h.waitingReply;
        var staff = state.messages.filter(function (m) { return m.role === 'staff'; }).length;
        if (staff > hadStaff && !state.open) state.unread = true;   // пришёл ответ менеджера, а чат закрыт
        save();
        if (state.messages.length !== before || staff !== hadStaff) render();
        else { updateContactForm(); updateBadge(); }
        schedulePolling();
    }

    // Пока ждём ответ из Telegram — тихо проверяем историю
    function schedulePolling() {
        if (pollTimer) { clearTimeout(pollTimer); pollTimer = null; }
        if (!state.waiting) return;
        pollTimer = setTimeout(function () {
            if (busy) { schedulePolling(); return; }
            request('GET', urls.history).then(applyHistory).catch(schedulePolling);
        }, POLL_MS);
    }

    function request(method, url, body) {
        return fetch(url, {
            method: method,
            headers: body ? { 'Content-Type': 'application/json' } : {},
            body: body ? JSON.stringify(body) : undefined,
            credentials: 'same-origin'
        }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        });
    }

    // Один раз за сессию вкладки подтягиваем переписку с сервера (посетитель мог вернуться через несколько дней)
    function loadHistory() {
        if (state.loaded) return Promise.resolve();
        return request('GET', urls.history).then(function (h) {
            applyHistory(h);
            render();
        }).catch(function () { });
    }

    function setOpen(open) {
        state.open = open;
        save();
        panel.hidden = !open;
        root.classList.toggle('ic-chat-open', open);
        if (open) {
            state.unread = false;
            save();
            updateBadge();
            loadHistory();
            list.scrollTop = list.scrollHeight;
            textarea.focus();
        }
    }

    function applyReply(res) {
        state.messages.push({ role: 'bot', text: res.reply, error: !!res.error });
        state.hasContact = !!res.hasContact;
        state.askContact = !!res.askContact && !state.hasContact;
        state.askConfirm = !!res.askConfirm;
        state.waiting = !!res.waitingReply;
        save();
        addBubble('bot', res.reply, res.error ? 'ic-msg-error' : '');
        updateContactForm();
        schedulePolling();
        if (!contactForm.hidden) contactForm.querySelector('[name=name]').focus();
    }

    function fail() {
        applyReply({ reply: T.Chatbot_ServiceError, error: true, askContact: true, hasContact: state.hasContact });
    }

    function sendMessage(text) {
        if (busy) return;
        busy = true;
        state.messages.push({ role: 'user', text: text });
        save();
        addBubble('user', text);
        var typing = addBubble('bot', T.Chatbot_Typing, 'ic-msg-typing');

        request('POST', urls.message, { message: text, page: currentPage() })
            .then(function (res) { typing.remove(); applyReply(res); })
            .catch(function () { typing.remove(); fail(); })
            .finally(function () { busy = false; });
    }

    toggle.addEventListener('click', function () { setOpen(!state.open); });
    var label = root.querySelector('.ic-chat-label');
    if (label) label.addEventListener('click', function () { setOpen(true); });
    root.querySelector('.ic-chat-close').addEventListener('click', function () { setOpen(false); });
    root.querySelector('.ic-chat-new').addEventListener('click', function () {
        if (busy) return;
        request('POST', urls.reset).catch(function () { });
        state = emptyState();
        state.loaded = true;
        state.open = true;
        save();
        render();
        textarea.focus();
    });

    inputForm.addEventListener('submit', function (e) {
        e.preventDefault();
        var text = textarea.value.trim();
        if (!text || busy) return;
        textarea.value = '';
        textarea.style.height = '';
        sendMessage(text);
    });

    // Enter — отправить, Shift+Enter — новая строка
    textarea.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            inputForm.requestSubmit ? inputForm.requestSubmit() : inputForm.dispatchEvent(new Event('submit'));
        }
    });
    textarea.addEventListener('input', function () {
        textarea.style.height = '';
        textarea.style.height = Math.min(textarea.scrollHeight, 140) + 'px';
    });

    if (confirmBox) {
        confirmBox.querySelector('.ic-chat-confirm-send').addEventListener('click', function () {
            if (busy) return;
            busy = true;
            request('POST', urls.send, {})
                .then(applyReply)
                .catch(fail)
                .finally(function () { busy = false; });
        });
    }

    contactForm.addEventListener('submit', function (e) {
        e.preventDefault();
        if (busy) return;
        var name = contactForm.querySelector('[name=name]').value.trim();
        var contact = contactForm.querySelector('[name=contact]').value.trim();
        if (!name || !contact) {
            addBubble('bot', T.Chatbot_ContactInvalid, 'ic-msg-error');
            return;
        }
        busy = true;
        request('POST', urls.contact, { name: name, contact: contact, page: currentPage() })
            .then(function (res) {
                if (res.escalated) contactForm.reset();
                applyReply(res);
            })
            .catch(fail)
            .finally(function () { busy = false; });
    });

    render();
    if (state.open) loadHistory();
    else if (state.waiting) {
        // ждём ответ — сразу проверяем (он мог прийти, пока посетитель был на другой странице)
        request('GET', urls.history).then(applyHistory).catch(schedulePolling);
    }
})();
