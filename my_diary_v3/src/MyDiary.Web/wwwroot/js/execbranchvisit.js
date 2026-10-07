// Executive Branch Visit — Add-Row focus management for JAWS/screen readers.
// Blazor's RenderTreeBuilder-generated inputs in ExecutiveBranchVisit.razor aren't
// individually captured as ElementReferences (they're built dynamically per row/column
// combo), so focus is moved via a plain CSS selector on a data-row-key attribute the
// component stamps onto each <tr> instead. Called right after StateHasChanged following
// AddOpenEndedRow — moves focus to the first focusable field in the newly added row so
// a screen reader user isn't left on the "Add Row" button with no indication where the
// new row landed.
window.execVisitFocusRow = function (rowKey) {
    var row = document.querySelector('[data-row-key="' + CSS.escape(rowKey) + '"]');
    if (!row) return;
    var field = row.querySelector('input, select, textarea');
    if (field) field.focus();
};
