// Filters the reporting-manager dropdown by the verified designation hierarchy.
// The server repeats every rule, so this is a usability aid, not the security boundary.
(function () {
    'use strict';

    var form = document.querySelector('[data-hierarchy-form]');
    if (!form) {
        return;
    }

    var designationInput = form.querySelector('[data-designation-input]');
    var managerSelect = form.querySelector('[data-manager-select]');
    var hint = form.querySelector('[data-manager-hint]');

    if (!designationInput || !managerSelect) {
        return;
    }

    var levels = {};
    try {
        levels = JSON.parse(form.getAttribute('data-designation-map') || '{}');
    } catch (error) {
        levels = {};
    }

    var expected = {
        CXO: [],
        Head: ['CXO'],
        Manager: ['Head'],
        Officer: ['Manager']
    };

    var prettyLevel = {
        CXO: 'CXO',
        Head: 'Head',
        Manager: 'Sales Manager or Supervisor'
    };

    function normalize(value) {
        return (value || '').trim().replace(/\s+/g, ' ').toLowerCase();
    }

    function levelFor(value) {
        var key = normalize(value);
        return key && Object.prototype.hasOwnProperty.call(levels, key) ? levels[key] : '';
    }

    function allowedFor(level) {
        return Object.prototype.hasOwnProperty.call(expected, level) ? expected[level] : null;
    }

    function describeAllowed(level) {
        var allowed = allowedFor(level);
        if (allowed === null) {
            return 'any manager';
        }

        return allowed.map(function (item) { return prettyLevel[item] || item; }).join(' or ');
    }

    function apply(preserveSelection) {
        var level = levelFor(designationInput.value);
        var allowed = allowedFor(level);
        var emptyOption = managerSelect.querySelector('option[value=""]');
        var current = managerSelect.options[managerSelect.selectedIndex];
        var currentInvalid = current && current.value &&
            allowed !== null && allowed.indexOf(current.getAttribute('data-level') || '') === -1;

        Array.prototype.forEach.call(managerSelect.options, function (option) {
            if (!option.value) {
                return;
            }

            var optionLevel = option.getAttribute('data-level') || '';
            var isAllowed = allowed === null || allowed.indexOf(optionLevel) !== -1;
            var keepInvalidSelection = preserveSelection && option.selected && !isAllowed;

            option.disabled = !isAllowed && !keepInvalidSelection;
            option.hidden = !isAllowed && !keepInvalidSelection;
        });

        if (emptyOption) {
            emptyOption.hidden = false;
            emptyOption.disabled = false;
            if (level === 'CXO') {
                emptyOption.textContent = 'No manager (CXO is top level)';
            } else if (allowed === null) {
                emptyOption.textContent = 'No manager (top level / legacy designation)';
            } else {
                emptyOption.textContent = 'Select a ' + describeAllowed(level);
            }
        }

        if (!preserveSelection && currentInvalid) {
            managerSelect.value = '';
            currentInvalid = false;
        }

        if (hint) {
            hint.classList.toggle('text-danger', currentInvalid);
            if (currentInvalid) {
                hint.textContent = 'The selected manager is not valid for this designation. Choose a ' + describeAllowed(level) + ' or clear the selection.';
            } else if (level === 'CXO') {
                hint.textContent = 'A CXO is top level and normally has no reporting manager.';
            } else if (allowed === null) {
                hint.textContent = 'This is a custom or legacy designation. Any valid manager can be selected.';
            } else {
                hint.textContent = level + ' records report to a ' + describeAllowed(level) + '. Only eligible people are shown.';
            }
        }
    }

    designationInput.addEventListener('input', function () {
        apply(true);
    });

    designationInput.addEventListener('change', function () {
        apply(false);
    });

    apply(true);
})();
