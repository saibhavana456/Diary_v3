// Generic IntersectionObserver-based lazy loader for below-the-fold ApexCharts.
// A page registers placeholder <div id="..."> elements; once one scrolls near the
// viewport this notifies Blazor (via dotNetRef.OnChartVisible) so the real chart
// component can be rendered in its place, then stops observing that element.
window.wireLazyCharts = function (dotNetRef, ids) {
    if (!('IntersectionObserver' in window)) {
        // No IntersectionObserver support — render everything immediately (safe fallback).
        ids.forEach(id => dotNetRef.invokeMethodAsync('OnChartVisible', id));
        return;
    }

    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (entry.isIntersecting) {
                dotNetRef.invokeMethodAsync('OnChartVisible', entry.target.id);
                observer.unobserve(entry.target);
            }
        });
    }, { rootMargin: '200px 0px' });

    ids.forEach((id) => {
        const el = document.getElementById(id);
        if (el) observer.observe(el);
    });
};
