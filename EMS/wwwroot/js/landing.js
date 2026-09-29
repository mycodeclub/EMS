// EMS landing page
(() => {
    'use strict';

    // Mobile menu
    const nav = document.querySelector('.nav');
    const toggle = document.querySelector('.nav-toggle');
    const setMenu = (open) => {
        nav.classList.toggle('nav-open', open);
        toggle.setAttribute('aria-expanded', String(open));
        toggle.setAttribute('aria-label', open ? 'Close menu' : 'Open menu');
    };
    toggle?.addEventListener('click', () => setMenu(!nav.classList.contains('nav-open')));
    document.querySelectorAll('.nav-links a').forEach((a) => a.addEventListener('click', () => setMenu(false)));
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape') setMenu(false); });

    // "Start free trial" / "Book a demo" / "Talk to sales" pre-select the enquiry type.
    document.querySelectorAll('[data-interest]').forEach((link) => {
        link.addEventListener('click', () => {
            const radio = document.querySelector(`input[name="Interest"][value="${link.dataset.interest}"]`);
            if (radio) radio.checked = true;
        });
    });

    // Reveal on scroll
    const items = document.querySelectorAll('.reveal');
    if ('IntersectionObserver' in window) {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('is-visible');
                    observer.unobserve(entry.target);
                }
            });
        }, { rootMargin: '0px 0px -8% 0px', threshold: 0.08 });
        items.forEach((el) => observer.observe(el));
    } else {
        items.forEach((el) => el.classList.add('is-visible'));
    }

    // After a submit: bring the form (errors) or the thank-you message into view.
    const form = document.getElementById('enquiry-form');
    const success = document.getElementById('enquiry-success');
    if (form?.dataset.hasErrors === 'true') {
        form.closest('.reveal')?.classList.add('is-visible');
        const firstInvalid = form.querySelector('.input-validation-error') || form;
        firstInvalid.scrollIntoView({ block: 'center' });
        if (firstInvalid !== form) firstInvalid.focus({ preventScroll: true });
    } else if (success) {
        success.closest('.reveal')?.classList.add('is-visible');
        success.focus({ preventScroll: true });
    }

    // Prevent double submits.
    form?.addEventListener('submit', () => {
        const button = form.querySelector('button[type="submit"]');
        button.disabled = true;
        button.textContent = 'Sending…';
    });
})();
