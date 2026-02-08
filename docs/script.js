/**
 * UEExtractor Landing Page - Interactive JavaScript
 */

document.addEventListener('DOMContentLoaded', function() {
    // Initialize all components
    initLanguageSwitcher();
    initMobileMenu();
    initSmoothScroll();
    initTabs();
    initPrepTabs();
    initCLISearch();
    initCopyButtons();
    initTerminalAnimation();
    initScrollAnimations();
    initHeaderScroll();
});

/**
 * Language Switcher
 */
function initLanguageSwitcher() {
    const langBtn = document.getElementById('langToggle');
    const langMenu = document.getElementById('langMenu');
    const langOptions = document.querySelectorAll('.lang-option');
    let currentLang = localStorage.getItem('ueex-lang') || 'en';

    function setLanguage(lang) {
        currentLang = lang;
        localStorage.setItem('ueex-lang', lang);
        document.documentElement.lang = lang;
        
        // Update active class on options
        langOptions.forEach(opt => {
            if (opt.getAttribute('data-value') === lang) {
                opt.classList.add('active');
            } else {
                opt.classList.remove('active');
            }
        });

        // Store original translations if not already stored
        document.querySelectorAll('[data-en][data-ru]').forEach(el => {
            if (!el.hasAttribute('data-original-en')) {
                el.setAttribute('data-original-en', el.getAttribute('data-en'));
                el.setAttribute('data-original-ru', el.getAttribute('data-ru'));
            }
        });

        // Update all elements with data-en and data-ru attributes
        document.querySelectorAll('[data-en][data-ru]').forEach(el => {
            const originalEn = el.getAttribute('data-original-en');
            const originalRu = el.getAttribute('data-original-ru');
            const text = lang === 'en' ? originalEn : originalRu;
            
            if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
                el.value = text;
            } else if (el.hasAttribute('data-en-placeholder') || el.hasAttribute('data-ru-placeholder')) {
                // Handle placeholders separately
                el.placeholder = text;
            } else {
                // For all other elements, use innerHTML but preserve data attributes
                el.innerHTML = text;
                // Restore the data attributes
                el.setAttribute('data-en', originalEn);
                el.setAttribute('data-ru', originalRu);
            }
        });

        // Handle placeholder inputs separately
        document.querySelectorAll('[data-en-placeholder][data-ru-placeholder]').forEach(el => {
            if (!el.hasAttribute('data-original-en-placeholder')) {
                el.setAttribute('data-original-en-placeholder', el.getAttribute('data-en-placeholder'));
                el.setAttribute('data-original-ru-placeholder', el.getAttribute('data-ru-placeholder'));
            }
            const originalEn = el.getAttribute('data-original-en-placeholder');
            const originalRu = el.getAttribute('data-original-ru-placeholder');
            el.placeholder = lang === 'en' ? originalEn : originalRu;
        });
        
        // Re-filter search if active
        const searchInput = document.getElementById('cliSearch');
        if (searchInput && searchInput.value) {
            searchInput.dispatchEvent(new Event('input'));
        }
    }

    if (langBtn && langMenu) {
        // Toggle menu
        langBtn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            const isExpanded = langMenu.classList.contains('show');
            
            if (isExpanded) {
                langMenu.classList.remove('show');
                langBtn.setAttribute('aria-expanded', 'false');
            } else {
                langMenu.classList.add('show');
                langBtn.setAttribute('aria-expanded', 'true');
            }
        });

        // Handle option click
        langOptions.forEach(opt => {
            opt.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                const selectedLang = opt.getAttribute('data-value');
                setLanguage(selectedLang);
                langMenu.classList.remove('show');
                langBtn.setAttribute('aria-expanded', 'false');
            });
        });

        // Close menu when clicking outside
        document.addEventListener('click', (e) => {
            if (langMenu.classList.contains('show') && !langMenu.contains(e.target) && !langBtn.contains(e.target)) {
                langMenu.classList.remove('show');
                langBtn.setAttribute('aria-expanded', 'false');
            }
        });
        
        // Close menu on Escape key
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && langMenu.classList.contains('show')) {
                langMenu.classList.remove('show');
                langBtn.setAttribute('aria-expanded', 'false');
                langBtn.focus();
            }
        });
    }

    // init
    setLanguage(currentLang);
}

/**
 * Mobile Menu Toggle
 */
function initMobileMenu() {
    const menuBtn = document.getElementById('mobileMenuBtn');
    const mobileMenu = document.getElementById('mobileMenu');
    
    if (menuBtn && mobileMenu) {
        menuBtn.addEventListener('click', function() {
            this.classList.toggle('active');
            mobileMenu.classList.toggle('active');
        });
        
        // Close menu when clicking on a link
        mobileMenu.querySelectorAll('a').forEach(link => {
            link.addEventListener('click', function() {
                menuBtn.classList.remove('active');
                mobileMenu.classList.remove('active');
            });
        });
        
        // Close menu when clicking outside
        document.addEventListener('click', function(e) {
            if (!menuBtn.contains(e.target) && !mobileMenu.contains(e.target)) {
                menuBtn.classList.remove('active');
                mobileMenu.classList.remove('active');
            }
        });
    }
}

/**
 * Smooth Scroll for Navigation Links
 */
function initSmoothScroll() {
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            const href = this.getAttribute('href');
            if (href === '#') return;
            
            e.preventDefault();
            const target = document.querySelector(href);
            
            if (target) {
                const headerHeight = document.querySelector('.header').offsetHeight;
                const targetPosition = target.getBoundingClientRect().top + window.pageYOffset - headerHeight - 20;
                
                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });
            }
        });
    });
}

/**
 * Tab Functionality for Usage Section
 */
function initTabs() {
    const tabButtons = document.querySelectorAll('.tab-btn');
    const tabPanes = document.querySelectorAll('.tab-pane');
    
    tabButtons.forEach(button => {
        button.addEventListener('click', function() {
            const tabId = this.getAttribute('data-tab');
            
            // Remove active class from all buttons and panes
            tabButtons.forEach(btn => btn.classList.remove('active'));
            tabPanes.forEach(pane => pane.classList.remove('active'));
            
            // Add active class to clicked button and corresponding pane
            this.classList.add('active');
            const activePane = document.getElementById(tabId);
            if (activePane) {
                activePane.classList.add('active');
            }
        });
    });
}

/**
 * Tab Functionality for Preparation Section
 */
function initPrepTabs() {
    const prepTabButtons = document.querySelectorAll('.prep-tab-btn');
    const prepPanes = document.querySelectorAll('.prep-pane');
    
    prepTabButtons.forEach(button => {
        button.addEventListener('click', function() {
            const prepId = this.getAttribute('data-prep');
            
            // Remove active class from all buttons and panes
            prepTabButtons.forEach(btn => btn.classList.remove('active'));
            prepPanes.forEach(pane => pane.classList.remove('active'));
            
            // Add active class to clicked button and corresponding pane
            this.classList.add('active');
            const activePane = document.getElementById(prepId);
            if (activePane) {
                activePane.classList.add('active');
            }
        });
    });
}

/**
 * CLI Search Functionality
 */
function initCLISearch() {
    const searchInput = document.getElementById('cliSearch');
    const cliItems = document.querySelectorAll('.cli-item');
    
    if (searchInput && cliItems.length > 0) {
        searchInput.addEventListener('input', function() {
            const searchTerm = this.value.toLowerCase().trim();
            
            cliItems.forEach(item => {
                const keywords = item.getAttribute('data-keywords') || '';
                const args = item.querySelector('.cli-args')?.textContent.toLowerCase() || '';
                const desc = item.querySelector('.cli-desc')?.textContent.toLowerCase() || '';
                
                const searchableText = (keywords + ' ' + args + ' ' + desc).toLowerCase();
                
                if (searchTerm === '' || searchableText.includes(searchTerm)) {
                    item.classList.remove('hidden');
                } else {
                    item.classList.add('hidden');
                }
            });
        });
        
        // Clear search on escape
        searchInput.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                this.value = '';
                cliItems.forEach(item => item.classList.remove('hidden'));
            }
        });
    }
}

/**
 * Copy to Clipboard Functionality
 */
function initCopyButtons() {
    const copyButtons = document.querySelectorAll('.copy-btn');
    
    copyButtons.forEach(button => {
        button.addEventListener('click', function() {
            const textToCopy = this.getAttribute('data-copy');
            
            if (textToCopy) {
                navigator.clipboard.writeText(textToCopy).then(() => {
                    // Show success feedback
                    const originalText = this.textContent;
                    this.textContent = '✓';
                    this.style.color = 'var(--accent-green)';
                    
                    setTimeout(() => {
                        this.textContent = originalText;
                        this.style.color = '';
                    }, 1500);
                }).catch(err => {
                    console.error('Failed to copy:', err);
                });
            }
        });
    });
}

/**
 * Terminal Animation Sequence
 */
function initTerminalAnimation() {
    const command1 = document.getElementById('command1');
    const output1 = document.getElementById('output1');
    const line2 = document.getElementById('line2');
    const output2 = document.getElementById('output2');
    const cursorLine = document.getElementById('cursorLine');
    
    if (!command1 || !output1) return;
    
    // Type command 1
    setTimeout(() => {
        if (command1) {
            command1.style.opacity = '1';
        }
    }, 500);
    
    // Show output 1
    setTimeout(() => {
        if (output1) {
            output1.classList.add('show');
        }
    }, 2000);
    
    // Show cursor line 2
    setTimeout(() => {
        if (line2) {
            line2.style.display = 'block';
            line2.style.opacity = '0';
        }
    }, 4000);
    
    // Type command 2
    setTimeout(() => {
        if (line2) {
            line2.style.opacity = '1';
        }
    }, 4500);
    
    // Show output 2
    setTimeout(() => {
        if (output2) {
            output2.classList.add('show');
        }
    }, 6000);
    
    // Show final cursor
    setTimeout(() => {
        if (cursorLine) {
            cursorLine.classList.add('show');
        }
    }, 8000);
}

/**
 * Scroll Animations using Intersection Observer
 */
function initScrollAnimations() {
    const animatedElements = document.querySelectorAll(
        '.feature-card, .format-item, .step, .usage-card, .cli-item, .prep-step, .merge-rule, .download-content'
    );
    
    const observerOptions = {
        root: null,
        rootMargin: '0px 0px -50px 0px',
        threshold: 0.1
    };
    
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0)';
                observer.unobserve(entry.target);
            }
        });
    }, observerOptions);
    
    animatedElements.forEach(el => {
        // Set initial styles
        el.style.opacity = '0';
        el.style.transform = 'translateY(30px)';
        el.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
        
        // Add staggered delay based on data-delay attribute
        if (el.hasAttribute('data-delay')) {
            el.style.transitionDelay = (parseInt(el.getAttribute('data-delay')) / 1000) + 's';
        }
        
        observer.observe(el);
    });
    
    // Also animate sections
    const sections = document.querySelectorAll('section');
    sections.forEach(section => {
        section.style.opacity = '0';
        section.style.transition = 'opacity 0.8s ease';
        
        const sectionObserver = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.style.opacity = '1';
                    sectionObserver.unobserve(entry.target);
                }
            });
        }, { threshold: 0.05 });
        
        sectionObserver.observe(section);
    });
}

/**
 * Header Background on Scroll
 */
function initHeaderScroll() {
    const header = document.querySelector('.header');
    
    if (header) {
        window.addEventListener('scroll', function() {
            if (window.scrollY > 50) {
                header.style.background = 'rgba(10, 10, 15, 0.95)';
                header.style.boxShadow = '0 4px 20px rgba(0, 0, 0, 0.3)';
            } else {
                header.style.background = 'rgba(10, 10, 15, 0.8)';
                header.style.boxShadow = 'none';
            }
        });
    }
}

/**
 * Add interactive hover effects to cards
 */
document.querySelectorAll('.feature-card, .usage-card, .format-item').forEach(card => {
    card.addEventListener('mouseleave', function() {
        this.style.transform = 'translateY(0) scale(1)';
    });
});

/**
 * Parallax effect for background orbs
 */
window.addEventListener('scroll', function() {
    const scrolled = window.pageYOffset;
    const orbs = document.querySelectorAll('.orb');
    
    orbs.forEach((orb, index) => {
        const speed = 0.05 * (index + 1);
        orb.style.transform = `translateY(${scrolled * speed}px)`;
    });
});

/**
 * Add loading state for download button
 */
document.querySelectorAll('.btn-primary').forEach(btn => {
    btn.addEventListener('click', function(e) {
        if (this.getAttribute('href') === '#download') {
            e.preventDefault();
            
            // Scroll to download section
            const downloadSection = document.querySelector('.download');
            if (downloadSection) {
                const headerHeight = document.querySelector('.header').offsetHeight;
                const targetPosition = downloadSection.getBoundingClientRect().top + window.pageYOffset - headerHeight - 20;
                
                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });
            }
        }
    });
});