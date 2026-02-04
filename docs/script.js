/**
 * UEExtractor Landing Page - Pure Vanilla JavaScript
 * No dependencies, GitHub Pages compatible
 */

(function() {
    'use strict';

    // Wait for DOM to be fully loaded
    document.addEventListener('DOMContentLoaded', function() {
        initFooterYear();
        initMobileMenu();
        initSmoothScroll();
        initUsageTabs();
        initPrepTabs();
        initCLISearch();
        initCopyButtons();
        initTerminalAnimation();
        initScrollAnimations();
        initHeaderScroll();
    });

    /**
     * Set current year in footer
     */
    function initFooterYear() {
        var yearEl = document.getElementById('year');
        if (yearEl) {
            yearEl.textContent = new Date().getFullYear();
        }
    }

    /**
     * Mobile hamburger menu toggle
     */
    function initMobileMenu() {
        var menuBtn = document.getElementById('mobileMenuBtn');
        var mobileMenu = document.getElementById('mobileMenu');
        
        if (!menuBtn || !mobileMenu) return;

        menuBtn.addEventListener('click', function(e) {
            e.stopPropagation();
            var isActive = menuBtn.classList.toggle('active');
            mobileMenu.classList.toggle('active');
            menuBtn.setAttribute('aria-expanded', isActive ? 'true' : 'false');
        });

        // Close menu when clicking a link
        var menuLinks = mobileMenu.querySelectorAll('a');
        for (var i = 0; i < menuLinks.length; i++) {
            menuLinks[i].addEventListener('click', function() {
                menuBtn.classList.remove('active');
                mobileMenu.classList.remove('active');
                menuBtn.setAttribute('aria-expanded', 'false');
            });
        }

        // Close menu when clicking outside
        document.addEventListener('click', function(e) {
            if (!menuBtn.contains(e.target) && !mobileMenu.contains(e.target)) {
                menuBtn.classList.remove('active');
                mobileMenu.classList.remove('active');
                menuBtn.setAttribute('aria-expanded', 'false');
            }
        });
    }

    /**
     * Smooth scroll for anchor links
     */
    function initSmoothScroll() {
        var anchors = document.querySelectorAll('a[href^="#"]');
        
        for (var i = 0; i < anchors.length; i++) {
            anchors[i].addEventListener('click', function(e) {
                var href = this.getAttribute('href');
                if (!href || href === '#') return;

                var target = document.querySelector(href);
                if (!target) return;

                e.preventDefault();
                
                var header = document.querySelector('.header');
                var headerHeight = header ? header.offsetHeight : 0;
                var targetTop = target.getBoundingClientRect().top + window.pageYOffset;
                var scrollPosition = targetTop - headerHeight - 20;

                window.scrollTo({
                    top: scrollPosition,
                    behavior: 'smooth'
                });
            });
        }
    }

    /**
     * Usage section tabs (Drag & Drop / CLI Commands / CSV Format)
     */
    function initUsageTabs() {
        var tabContainer = document.querySelector('.usage-tabs');
        if (!tabContainer) return;

        var tabButtons = tabContainer.querySelectorAll('.tab-btn');
        var tabPanes = tabContainer.querySelectorAll('.tab-pane');

        for (var i = 0; i < tabButtons.length; i++) {
            tabButtons[i].addEventListener('click', function() {
                var tabId = this.getAttribute('data-tab');
                if (!tabId) return;

                // Remove active from all buttons
                for (var j = 0; j < tabButtons.length; j++) {
                    tabButtons[j].classList.remove('active');
                    tabButtons[j].setAttribute('aria-selected', 'false');
                }

                // Remove active from all panes
                for (var k = 0; k < tabPanes.length; k++) {
                    tabPanes[k].classList.remove('active');
                }

                // Add active to clicked button
                this.classList.add('active');
                this.setAttribute('aria-selected', 'true');

                // Show corresponding pane
                var targetPane = document.getElementById(tabId);
                if (targetPane) {
                    targetPane.classList.add('active');
                }
            });
        }
    }

    /**
     * Preparation section tabs (UE4 Pre-ZenLoader / UE4-5 ZenLoader)
     */
    function initPrepTabs() {
        var prepContainer = document.querySelector('.prep-tabs');
        if (!prepContainer) return;

        var prepButtons = prepContainer.querySelectorAll('.prep-tab-btn');
        var prepPanes = prepContainer.querySelectorAll('.prep-pane');

        for (var i = 0; i < prepButtons.length; i++) {
            prepButtons[i].addEventListener('click', function() {
                var prepId = this.getAttribute('data-prep');
                if (!prepId) return;

                // Remove active from all buttons
                for (var j = 0; j < prepButtons.length; j++) {
                    prepButtons[j].classList.remove('active');
                    prepButtons[j].setAttribute('aria-selected', 'false');
                }

                // Remove active from all panes
                for (var k = 0; k < prepPanes.length; k++) {
                    prepPanes[k].classList.remove('active');
                }

                // Add active to clicked button
                this.classList.add('active');
                this.setAttribute('aria-selected', 'true');

                // Show corresponding pane
                var targetPane = document.getElementById(prepId);
                if (targetPane) {
                    targetPane.classList.add('active');
                }
            });
        }
    }

    /**
     * CLI Reference search/filter
     */
    function initCLISearch() {
        var searchInput = document.getElementById('cliSearch');
        var cliItems = document.querySelectorAll('.cli-item');
        
        if (!searchInput || cliItems.length === 0) return;

        searchInput.addEventListener('input', function() {
            var searchTerm = this.value.toLowerCase().trim();

            for (var i = 0; i < cliItems.length; i++) {
                var item = cliItems[i];
                var keywords = (item.getAttribute('data-keywords') || '').toLowerCase();
                var argsEl = item.querySelector('.cli-args');
                var descEl = item.querySelector('.cli-desc');
                var argsText = argsEl ? argsEl.textContent.toLowerCase() : '';
                var descText = descEl ? descEl.textContent.toLowerCase() : '';
                var searchableText = keywords + ' ' + argsText + ' ' + descText;

                if (searchTerm === '' || searchableText.indexOf(searchTerm) !== -1) {
                    item.classList.remove('hidden');
                } else {
                    item.classList.add('hidden');
                }
            }
        });

        // Clear search on Escape
        searchInput.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                this.value = '';
                for (var i = 0; i < cliItems.length; i++) {
                    cliItems[i].classList.remove('hidden');
                }
            }
        });
    }

    /**
     * Copy to clipboard buttons
     */
    function initCopyButtons() {
        var copyButtons = document.querySelectorAll('.copy-btn');
        
        for (var i = 0; i < copyButtons.length; i++) {
            copyButtons[i].addEventListener('click', handleCopy);
            copyButtons[i].addEventListener('keydown', function(e) {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    handleCopy.call(this);
                }
            });
        }

        function handleCopy() {
            var btn = this;
            var textToCopy = btn.getAttribute('data-copy');
            if (!textToCopy) return;

            var originalText = btn.textContent;

            // Try modern clipboard API first
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(textToCopy).then(function() {
                    showCopySuccess(btn, originalText);
                }).catch(function() {
                    fallbackCopy(textToCopy);
                    showCopySuccess(btn, originalText);
                });
            } else {
                fallbackCopy(textToCopy);
                showCopySuccess(btn, originalText);
            }
        }

        function fallbackCopy(text) {
            var textarea = document.createElement('textarea');
            textarea.value = text;
            textarea.style.position = 'fixed';
            textarea.style.top = '-9999px';
            textarea.style.left = '-9999px';
            document.body.appendChild(textarea);
            textarea.select();
            try {
                document.execCommand('copy');
            } catch (err) {
                console.error('Copy failed:', err);
            }
            document.body.removeChild(textarea);
        }

        function showCopySuccess(btn, originalText) {
            btn.textContent = '✓';
            btn.style.color = '#22c55e';
            setTimeout(function() {
                btn.textContent = originalText;
                btn.style.color = '';
            }, 1200);
        }
    }

    /**
     * Terminal typing animation in hero section
     */
    function initTerminalAnimation() {
        var output1 = document.getElementById('output1');
        var line2 = document.getElementById('line2');
        var output2 = document.getElementById('output2');
        var cursorLine = document.getElementById('cursorLine');

        // Initially hide elements
        if (line2) {
            line2.style.display = 'none';
        }

        // Show first output after delay
        setTimeout(function() {
            if (output1) {
                output1.classList.add('show');
            }
        }, 1200);

        // Show second command line
        setTimeout(function() {
            if (line2) {
                line2.style.display = 'block';
                line2.style.opacity = '0';
                line2.style.transition = 'opacity 0.3s ease';
                setTimeout(function() {
                    line2.style.opacity = '1';
                }, 50);
            }
        }, 2800);

        // Show second output
        setTimeout(function() {
            if (output2) {
                output2.classList.add('show');
            }
        }, 4300);

        // Show cursor line
        setTimeout(function() {
            if (cursorLine) {
                cursorLine.classList.add('show');
            }
        }, 6000);
    }

    /**
     * Scroll-triggered animations using Intersection Observer
     */
    function initScrollAnimations() {
        var animatedElements = document.querySelectorAll(
            '.feature-card, .format-item, .step, .usage-card, .cli-item, .prep-step, .merge-rule, .download-content'
        );

        // Fallback for browsers without IntersectionObserver
        if (!('IntersectionObserver' in window)) {
            for (var i = 0; i < animatedElements.length; i++) {
                animatedElements[i].style.opacity = '1';
                animatedElements[i].style.transform = 'none';
            }
            return;
        }

        var observer = new IntersectionObserver(function(entries) {
            for (var i = 0; i < entries.length; i++) {
                if (entries[i].isIntersecting) {
                    entries[i].target.style.opacity = '1';
                    entries[i].target.style.transform = 'translateY(0)';
                    observer.unobserve(entries[i].target);
                }
            }
        }, {
            root: null,
            rootMargin: '0px 0px -50px 0px',
            threshold: 0.1
        });

        for (var j = 0; j < animatedElements.length; j++) {
            var el = animatedElements[j];
            el.style.opacity = '0';
            el.style.transform = 'translateY(30px)';
            el.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
            
            var delay = el.getAttribute('data-delay');
            if (delay) {
                el.style.transitionDelay = (parseInt(delay, 10) / 1000) + 's';
            }
            
            observer.observe(el);
        }

        // Section fade-in
        var sections = document.querySelectorAll('section');
        var sectionObserver = new IntersectionObserver(function(entries) {
            for (var i = 0; i < entries.length; i++) {
                if (entries[i].isIntersecting) {
                    entries[i].target.style.opacity = '1';
                    sectionObserver.unobserve(entries[i].target);
                }
            }
        }, {
            threshold: 0.05
        });

        for (var k = 0; k < sections.length; k++) {
            sections[k].style.opacity = '0';
            sections[k].style.transition = 'opacity 0.8s ease';
            sectionObserver.observe(sections[k]);
        }
    }

    /**
     * Header background change on scroll
     */
    function initHeaderScroll() {
        var header = document.querySelector('.header');
        if (!header) return;

        var lastScrollY = 0;
        var ticking = false;

        function updateHeader() {
            if (window.scrollY > 50) {
                header.style.background = 'rgba(10, 10, 15, 0.95)';
                header.style.boxShadow = '0 4px 20px rgba(0, 0, 0, 0.3)';
            } else {
                header.style.background = 'rgba(10, 10, 15, 0.8)';
                header.style.boxShadow = 'none';
            }
            ticking = false;
        }

        window.addEventListener('scroll', function() {
            lastScrollY = window.scrollY;
            if (!ticking) {
                window.requestAnimationFrame(updateHeader);
                ticking = true;
            }
        });
    }

})();
