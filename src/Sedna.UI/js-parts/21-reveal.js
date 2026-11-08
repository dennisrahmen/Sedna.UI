/* ── Password reveal ─────────────────────────────────────────────────────────
   A button with `data-reveal` shows the password in the field its `aria-controls`
   names — else the password field beside it in its `.input-group` — as text, and hides
   it again. The field's `type` follows, and so does the button's `aria-pressed`; the
   button's label is the app's and is never rewritten, because the pressed state is what
   a screen reader announces as the change:

     <div class="input-group">
       <input class="form-input" id="pw" type="password" autocomplete="current-password">
       <button class="input-reveal" type="button" data-reveal aria-controls="pw" aria-pressed="false" aria-label="Show password">
         <i class="ri-eye-line"></i><i class="ri-eye-off-line"></i>
       </button>
     </div>

   The button's own `click` still reaches the app. This part adds no member to `sednaUi`.
   ─────────────────────────────────────────────────────────────────────────── */
(function () {

    function fieldOf(button) {
        var id = button.getAttribute('aria-controls');
        if (id) {
            var named = document.getElementById(id.split(/\s+/)[0]);
            if (named instanceof HTMLInputElement) return named;
        }
        var group = button.parentElement;
        if (!group) return null;
        return group.querySelector('input[type="password"], input[data-sedna-revealed]');
    }

    function show(field, button, on) {
        if (on) {
            field.type = 'text';
            field.setAttribute('data-sedna-revealed', '');
        } else {
            field.type = 'password';
            field.removeAttribute('data-sedna-revealed');
        }
        if (button) button.setAttribute('aria-pressed', on ? 'true' : 'false');
    }

    document.addEventListener('click', function (e) {
        var t = e.target;
        var button = t instanceof Element ? t.closest('[data-reveal]') : null;
        if (!button || button.disabled || button.getAttribute('aria-disabled') === 'true') return;
        var field = fieldOf(button);
        if (!field) return;
        show(field, button, field.type === 'password');
    });

})();
