// EMS consoles: chart tooltips, the attendance register and day view, and add/remove rows in editable tables.
(function () {
    'use strict';

    // Tooltip for any element with data-tip (chart columns and bars). Follows the pointer; keyboard focus shows it too.
    var tip = document.createElement('div');
    tip.className = 'tip';
    tip.hidden = true;
    tip.setAttribute('role', 'tooltip');
    document.body.appendChild(tip);

    function showTip(target, x, y) {
        tip.textContent = target.getAttribute('data-tip');
        tip.hidden = false;
        var w = tip.offsetWidth, h = tip.offsetHeight;
        tip.style.left = Math.max(8, Math.min(window.innerWidth - w - 8, x - w / 2)) + 'px';
        tip.style.top = Math.max(8, y - h - 12) + 'px';
    }
    document.addEventListener('pointermove', function (e) {
        var target = e.target.closest && e.target.closest('[data-tip]');
        if (target) showTip(target, e.clientX, e.clientY); else tip.hidden = true;
    });
    document.addEventListener('focusin', function (e) {
        var target = e.target.closest && e.target.closest('[data-tip]');
        if (!target) return;
        var r = target.getBoundingClientRect();
        showTip(target, r.left + r.width / 2, r.top);
    });
    document.addEventListener('focusout', function () { tip.hidden = true; });

    // Attendance register and day view: colour each status by its value; "fill" marks every empty unlocked cell.
    document.querySelectorAll('.register select, select.status').forEach(function (s) {
        s.setAttribute('data-v', s.value);
        s.addEventListener('change', function () { s.setAttribute('data-v', s.value); });
    });
    document.querySelectorAll('[data-fill]').forEach(function (button) {
        button.addEventListener('click', function () {
            var weekday = button.getAttribute('data-fill'), weekend = button.getAttribute('data-fill-weekend') || weekday;
            document.querySelectorAll('.register select').forEach(function (s) {
                if (s.value !== '') return;
                s.value = s.hasAttribute('data-weekend') ? weekend : weekday;
                s.setAttribute('data-v', s.value);
            });
        });
    });

    // Punch in / out day view: worked time, late and status update while typing, using the same rules as
    // PunchRules.cs. The status follows the times until it is changed by hand (for leave, holidays…).
    function minutes(value) {
        if (!value) return null;
        var parts = value.split(':');
        return parseInt(parts[0], 10) * 60 + parseInt(parts[1], 10);
    }
    function duration(m) { return m < 60 ? m + 'm' : Math.floor(m / 60) + 'h ' + String(m % 60).padStart(2, '0') + 'm'; }

    document.querySelectorAll('[data-punch-row]').forEach(function (row) {
        var timeIn = row.querySelector('[data-in]'), timeOut = row.querySelector('[data-out]');
        var status = row.querySelector('[data-status]'), worked = row.querySelector('[data-worked]');
        var start = minutes(row.getAttribute('data-start')), end = minutes(row.getAttribute('data-end'));
        var grace = +(row.getAttribute('data-grace') || 0), full = +(row.getAttribute('data-full') || 0), half = +(row.getAttribute('data-half') || 0);

        function evaluate() {
            var i = minutes(timeIn.value), o = minutes(timeOut.value);
            if (i === null) return { text: '\u2014', late: false, status: '' };
            var late = false;
            if (start !== null) {
                // Night shift (ends after midnight): measure from the start the nearest way round the clock.
                var offset = i - start;
                if (end !== null && end <= start) {
                    offset = (offset + 1440) % 1440;
                    if (offset > 720) offset -= 1440;
                }
                late = offset > grace;
            }
            if (o === null) return { text: 'No out time', late: late, status: '1' };
            var m = (o - i + 1440) % 1440;
            var s = start === null || full <= 0 || m >= full ? '1' : half > 0 && m >= half ? '3' : '2';
            return { text: duration(m), late: late, status: s };
        }
        function update() {
            var result = evaluate();
            worked.textContent = result.text + ' ';
            if (result.late) {
                var small = document.createElement('small');
                small.className = 'late';
                small.textContent = 'Late';
                worked.appendChild(small);
            }
            if (status.hasAttribute('data-auto')) {
                status.value = result.status;
                status.setAttribute('data-v', status.value);
            }
        }

        // A saved status that matches the times keeps following them.
        if (status.value === '' || status.value === evaluate().status) status.setAttribute('data-auto', '');
        status.addEventListener('change', function () {
            if (status.value === '') status.setAttribute('data-auto', ''); else status.removeAttribute('data-auto');
        });
        timeIn.addEventListener('input', update);
        timeOut.addEventListener('input', update);
        row.fillFromShift = function () {
            if (start === null || timeIn.value || status.value) return;
            timeIn.value = row.getAttribute('data-start');
            timeOut.value = row.getAttribute('data-end');
            update();
        };
    });
    document.querySelectorAll('[data-fill-shift]').forEach(function (button) {
        button.addEventListener('click', function () {
            document.querySelectorAll('[data-punch-row]').forEach(function (row) { row.fillFromShift && row.fillFromShift(); });
        });
    });

    // Editable tables (shifts, employees): add a row from the <template>, remove a row, and renumber
    // the input names (Rows[0].Name, Rows[1].Name…) so ASP.NET binds the list without gaps.
    document.querySelectorAll('[data-rows]').forEach(function (table) {
        var body = table.querySelector('tbody');
        var template = document.getElementById(table.getAttribute('data-rows'));
        var prefix = table.getAttribute('data-prefix');
        var pattern = new RegExp('^' + prefix + '\\[\\d+\\]');

        function renumber() {
            Array.prototype.forEach.call(body.rows, function (row, i) {
                row.querySelectorAll('[name]').forEach(function (input) {
                    // __Invariant fields name the number/time inputs to parse culture-invariantly in their value.
                    if (input.name === '__Invariant') input.value = input.value.replace(pattern, prefix + '[' + i + ']');
                    else input.name = input.name.replace(pattern, prefix + '[' + i + ']');
                    if (input.id) input.id = input.name.replace(/[\[\].]/g, '_');
                });
                row.querySelectorAll('[data-valmsg-for]').forEach(function (m) {
                    m.setAttribute('data-valmsg-for', m.getAttribute('data-valmsg-for').replace(pattern, prefix + '[' + i + ']'));
                });
            });
            table.dispatchEvent(new CustomEvent('rows-changed', { bubbles: true }));
        }

        document.querySelectorAll('[data-add-row="' + table.id + '"]').forEach(function (button) {
            button.addEventListener('click', function () {
                body.appendChild(template.content.cloneNode(true));
                renumber();
                var first = body.rows[body.rows.length - 1].querySelector('input:not([type=hidden])');
                if (first) first.focus();
            });
        });
        body.addEventListener('click', function (e) {
            var remove = e.target.closest('[data-remove-row]');
            if (!remove) return;
            remove.closest('tr').remove();
            renumber();
        });
        table.renumber = renumber;
    });

    // Shift setup: "one shift" keeps a single row; "several shifts" offers the 24x7 preset when only one row exists.
    var editor = document.getElementById('shift-rows');
    document.querySelectorAll('input[name="OperatesInShifts"]').forEach(function (radio) {
        radio.addEventListener('change', function () {
            if (!editor) return;
            var multi = radio.value === 'true';
            editor.closest('form').setAttribute('data-multi', multi);
            var body = editor.querySelector('tbody');
            if (!multi) {
                while (body.rows.length > 1) body.deleteRow(body.rows.length - 1);
            } else if (body.rows.length < 2) {
                var preset = document.getElementById('shift-preset');
                if (preset) {
                    body.innerHTML = '';
                    body.appendChild(preset.content.cloneNode(true));
                }
            }
            editor.renumber && editor.renumber();
        });
    });
})();
