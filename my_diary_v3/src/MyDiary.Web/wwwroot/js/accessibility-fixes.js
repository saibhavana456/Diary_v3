/**
 * Accessibility Fixes — Global JavaScript
 * Fixes MudBlazor MudSelect combobox announcement for JAWS/NVDA screen readers.
 * 
 * Issue: MudSelect does not render role="combobox" on its interactive element,
 * so screen readers don't announce it as a dropdown/combobox.
 * 
 * Fix: After Blazor renders, find all MudSelect elements and add proper ARIA
 * attributes to make them announced correctly.
 */

(function () {
    'use strict';

    /**
     * Patches all MudSelect components on the page with proper ARIA attributes.
     * MudBlazor renders MudSelect as:
     *   div.mud-select > div.mud-input-control > div.mud-input > input (or div.mud-input-slot)
     * 
     * The input element (or input-slot) needs role="combobox" for JAWS/NVDA.
     */
    function patchMudSelectAccessibility() {
        var selects = document.querySelectorAll('.mud-select');

        selects.forEach(function (selectEl) {
            // Find the interactive element inside the MudSelect
            // MudBlazor uses either an <input> or a <div class="mud-input-slot">
            var interactiveEl = selectEl.querySelector('input.mud-input-slot') ||
                                selectEl.querySelector('div.mud-input-slot') ||
                                selectEl.querySelector('.mud-input-control input[type="text"]') ||
                                selectEl.querySelector('.mud-input-control .mud-input input');

            if (!interactiveEl) return;

            // Skip if already properly patched
            if (interactiveEl.getAttribute('role') === 'combobox' && 
                interactiveEl.getAttribute('aria-haspopup') === 'listbox') return;

            // Set combobox semantics
            interactiveEl.setAttribute('role', 'combobox');
            interactiveEl.setAttribute('aria-haspopup', 'listbox');
            interactiveEl.setAttribute('aria-autocomplete', 'list');

            // Set aria-expanded based on whether the popover is open
            var isOpen = selectEl.classList.contains('mud-select-menu-open');
            interactiveEl.setAttribute('aria-expanded', isOpen ? 'true' : 'false');

            // Set aria-label from the label element if not already present
            if (!interactiveEl.getAttribute('aria-label')) {
                var label = selectEl.querySelector('label.mud-input-label');
                if (label) {
                    interactiveEl.setAttribute('aria-label', label.textContent.trim());
                }
            }
        });

        // Also patch any MudSelect that has opened its popover (update aria-expanded)
        document.querySelectorAll('.mud-select.mud-select-menu-open').forEach(function (openSelect) {
            var interactiveEl = openSelect.querySelector('[role="combobox"]');
            if (interactiveEl) {
                interactiveEl.setAttribute('aria-expanded', 'true');
            }
        });
    }

    /**
     * Fix #55: Remove focus from decorative search icon button.
     */
    function patchDecorativeSearchIcon() {
        var searchBars = document.querySelectorAll('.search-bar');
        searchBars.forEach(function (bar) {
            var adornmentBtns = bar.querySelectorAll('.mud-input-adornment-start .mud-icon-button');
            adornmentBtns.forEach(function (btn) {
                btn.setAttribute('tabindex', '-1');
                btn.setAttribute('aria-hidden', 'true');
            });
        });
    }

    /**
     * Fix #15: Remove tabindex from Digital Business tables/cards.
     */
    function patchNonInteractiveTabFocus() {
        var bottomCards = document.querySelectorAll('.dashboard-bottom-card');
        bottomCards.forEach(function (card) {
            var focusableChildren = card.querySelectorAll('[tabindex="0"]');
            focusableChildren.forEach(function (el) {
                var tag = el.tagName.toLowerCase();
                if (tag !== 'a' && tag !== 'button' && tag !== 'input' && tag !== 'select' && tag !== 'textarea') {
                    el.setAttribute('tabindex', '-1');
                }
            });
            card.setAttribute('tabindex', '-1');
        });
    }

    /**
     * Run all accessibility patches.
     */
    function runAllPatches() {
        patchMudSelectAccessibility();
        patchDecorativeSearchIcon();
        patchNonInteractiveTabFocus();
    }

    // Run patches after initial page load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            setTimeout(runAllPatches, 500);
        });
    } else {
        setTimeout(runAllPatches, 500);
    }

    // Use MutationObserver to re-apply patches when Blazor updates DOM
    var observer = new MutationObserver(function (mutations) {
        var shouldPatch = mutations.some(function (m) {
            return m.addedNodes.length > 0 || 
                   (m.type === 'attributes' && m.attributeName === 'class');
        });
        if (shouldPatch) {
            clearTimeout(window._a11yPatchTimeout);
            window._a11yPatchTimeout = setTimeout(runAllPatches, 200);
        }
    });

    // Start observing after Blazor has initialized
    setTimeout(function () {
        observer.observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['class']
        });
    }, 1000);

    // Expose for manual invocation from Blazor if needed
    window.patchAccessibility = runAllPatches;
})();
