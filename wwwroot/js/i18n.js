(() => {
    const LOCALE_STORAGE_KEY = 'sasilva_locale';
    const FALLBACK = 'pt';
    const LOCALES = ['en', 'pt'];

    let currentLocale = FALLBACK;
    let translations = {};

    function getPreferredLocale() {
        const stored = localStorage.getItem(LOCALE_STORAGE_KEY);
        if (stored && LOCALES.includes(stored)) return stored;

        const browser = navigator.language.slice(0, 2).toLowerCase();
        if (LOCALES.includes(browser)) return browser;

        return FALLBACK;
    }

    function deepGet(obj, path) {
        return path.split('.').reduce((acc, key) => acc && acc[key], obj);
    }

    async function loadLocale(locale) {
        try {
            const resp = await fetch(`/i18n/${locale}.json`);
            if (!resp.ok) throw new Error(`Failed to load ${locale}`);
            return await resp.json();
        } catch (e) {
            console.warn(`Failed to load locale ${locale}, falling back to ${FALLBACK}`);
            currentLocale = FALLBACK;
            return loadLocale(FALLBACK);
        }
    }

    function hydrate(langData) {
        const trans = langData;
        const t = (key) => deepGet(trans, key) || key;

        document.querySelectorAll('[data-i18n]').forEach(el => {
            const key = el.getAttribute('data-i18n');
            const value = t(key);
            const tag = el.tagName.toLowerCase();
            if (tag === 'input' || tag === 'textarea') {
                el.value = value;
            } else {
                el.innerHTML = value;
            }
        });

        document.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
            const key = el.getAttribute('data-i18n-placeholder');
            el.placeholder = t(key);
        });

        document.documentElement.lang = langData.meta.locale;
        document.documentElement.dir = langData.meta.direction;
    }

    function setLangSwitchUI(locale) {
        const ptBtn = document.getElementById('lang-pt');
        const enBtn = document.getElementById('lang-en');
        const switcher = document.getElementById('lang-switcher');
        if (!ptBtn || !enBtn) return;

        ptBtn.classList.toggle('active', locale === 'pt');
        enBtn.classList.toggle('active', locale === 'en');
        switcher?.classList.add('ready');
    }

    async function switchLocale(target) {
        if (target === currentLocale) return;
        currentLocale = target;
        localStorage.setItem(LOCALE_STORAGE_KEY, target);

        if (!translations[target]) {
            translations[target] = await loadLocale(target);
        }

        hydrate(translations[target]);
        setLangSwitchUI(target);
        window.__t = (key) => deepGet(translations[target], key) || key;
        window.dispatchEvent(new CustomEvent('localeChanged', { detail: { locale: target } }));
    }

    async function init() {
        currentLocale = getPreferredLocale();
        document.documentElement.lang = currentLocale;

        const trans = await loadLocale(currentLocale);
        translations[currentLocale] = trans;
        hydrate(trans);
        setLangSwitchUI(currentLocale);

        document.getElementById('lang-pt')?.addEventListener('click', () => switchLocale('pt'));
        document.getElementById('lang-en')?.addEventListener('click', () => switchLocale('en'));

        window.__t = (key) => deepGet(translations[currentLocale], key) || key;
        window.__getLocale = () => currentLocale;
    }

    document.readyState === 'loading'
        ? document.addEventListener('DOMContentLoaded', init)
        : init();
})();
