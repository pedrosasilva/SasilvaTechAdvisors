(() => {
    const header = document.getElementById('site-header');
    const menuToggle = document.getElementById('menu-toggle');
    const navLinks = document.getElementById('nav-links');
    const navLinksItems = document.querySelectorAll('.nav-link');
    const contactForm = document.getElementById('contact-form');
    const formStatus = document.getElementById('form-status');
    const heroParallax = document.getElementById('hero-bg-parallax');
    const parallaxElements = document.querySelectorAll('[data-parallax-speed]');

    menuToggle?.addEventListener('click', () => {
        menuToggle.classList.toggle('active');
        navLinks.classList.toggle('open');
    });

    navLinksItems.forEach(link => {
        link.addEventListener('click', () => {
            menuToggle.classList.remove('active');
            navLinks.classList.remove('open');
        });
    });

    let ticking = false;
    let currentScroll = 0;
    let targetScroll = 0;

    window.addEventListener('scroll', () => {
        targetScroll = window.scrollY;
        if (!ticking) {
            window.requestAnimationFrame(() => {
                handleScroll();
                ticking = false;
            });
            ticking = true;
        }
    });

    function lerp(a, b, t) {
        return a + (b - a) * t;
    }

    function handleScroll() {
        currentScroll = lerp(currentScroll, targetScroll, 0.12);

        if (header) {
            header.classList.toggle('scrolled', currentScroll > 50);
        }

        const sections = document.querySelectorAll('section[id]');
        const scrollPos = currentScroll + 120;

        sections.forEach(section => {
            const top = section.offsetTop;
            const height = section.offsetHeight;
            const id = section.getAttribute('id');

            if (scrollPos >= top && scrollPos < top + height) {
                navLinksItems.forEach(link => {
                    link.classList.remove('active');
                    if (link.getAttribute('href') === `#${id}`) {
                        link.classList.add('active');
                    }
                });
            }
        });

        if (heroParallax) {
            const heroOffset = currentScroll * 0.28;
            heroParallax.style.transform = `translate3d(0, ${heroOffset}px, 0) scale(1.03)`;
        }

        parallaxElements.forEach(el => {
            const speed = parseFloat(el.getAttribute('data-parallax-speed'));
            const section = el.closest('section');
            if (section) {
                const rect = section.getBoundingClientRect();
                const sectionVisible = rect.top < window.innerHeight && rect.bottom > 0;
                if (sectionVisible) {
                    const offset = rect.top * speed;
                    el.style.transform = `translate3d(0, ${offset}px, 0)`;
                }
            }
        });

        if (Math.abs(currentScroll - targetScroll) > 0.5) {
            window.requestAnimationFrame(() => {
                handleScroll();
            });
        }
    }

    const revealObserver = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('visible');
            }
        });
    }, {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    });

    document.querySelectorAll('.reveal').forEach(el => {
        revealObserver.observe(el);
    });

    contactForm?.addEventListener('submit', async (e) => {
        e.preventDefault();

        const submitBtn = contactForm.querySelector('.btn-submit');
        const name = document.getElementById('name')?.value?.trim();
        const email = document.getElementById('email')?.value?.trim();
        const message = document.getElementById('message')?.value?.trim();

        if (!name || !email || !message) {
            formStatus.textContent = 'Please fill in all fields.';
            formStatus.className = 'error';
            return;
        }

        submitBtn.classList.add('loading');
        formStatus.textContent = '';

        try {
            const response = await fetch('/contact/submit', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name, email, message })
            });

            const data = await response.json();

            if (response.ok) {
                formStatus.textContent = data.message || 'Message sent successfully!';
                formStatus.className = 'success';
                contactForm.reset();
            } else {
                formStatus.textContent = data.message || 'Failed to send message. Please try again.';
                formStatus.className = 'error';
            }
        } catch {
            formStatus.textContent = 'Connection error. Please try again later.';
            formStatus.className = 'error';
        } finally {
            submitBtn.classList.remove('loading');
        }
    });
})();
