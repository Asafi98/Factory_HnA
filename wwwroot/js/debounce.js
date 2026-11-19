// Debounced input for Blazor Server — prevents keystroke loss during fast typing.
// The browser controls the input natively; .NET only receives the value after a pause.
window.blazorDebounce = {
    _timers: {},
    init: function (inputEl, dotNetRef, methodName, delayMs) {
        if (!inputEl) return;
        var id = inputEl.id || ('db_' + Math.random().toString(36).slice(2, 9));
        inputEl.id = id;
        // Clear any previous listener (in case of re-render)
        if (this._timers[id] && this._timers[id].handler) {
            inputEl.removeEventListener('input', this._timers[id].handler);
        }
        var self = this;
        var handler = function (e) {
            clearTimeout(self._timers[id].tid);
            self._timers[id].tid = setTimeout(function () {
                dotNetRef.invokeMethodAsync(methodName, e.target.value);
            }, delayMs);
        };
        this._timers[id] = { tid: null, handler: handler };
        inputEl.addEventListener('input', handler);
    }
};
