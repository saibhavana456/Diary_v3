// Intentionally minimal. MyDiary.Core.* are NOT global-imported because this
// assembly now also hosts the RequestPortal.Core.* namespaces (Project A, merged
// in), and several model/abstraction type names exist in both. Each file imports
// the namespace it needs explicitly to avoid ambiguity.
