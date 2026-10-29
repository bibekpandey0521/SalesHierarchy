// Accessible, searchable multi-select for salesperson POD assignments.
// Selected POD IDs are submitted as repeated hidden inputs named "PODIds".
(function () {
    'use strict';

    function createChip(root, selected, pod, render) {
        var chip = document.createElement('li');
        chip.className = 'pod-chip';

        var label = document.createElement('span');
        label.textContent = pod.name;
        chip.appendChild(label);

        var remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'pod-chip-remove';
        remove.setAttribute('aria-label', 'Remove ' + pod.name);
        remove.innerHTML = '&times;';
        remove.addEventListener('click', function () {
            selected.delete(pod.id);
            render();
        });

        chip.appendChild(remove);
        return chip;
    }

    function init(root) {
        var search = root.querySelector('[data-pod-search]');
        var list = root.querySelector('[data-pod-options]');
        var chips = root.querySelector('[data-pod-chips]');
        var inputs = root.querySelector('[data-pod-inputs]');
        var empty = root.querySelector('[data-pod-empty]');
        var optionElements = Array.prototype.slice.call(root.querySelectorAll('[data-pod-option]'));

        if (!search || !list || !chips || !inputs) {
            return;
        }

        var noResults = root.getAttribute('data-no-results') || 'No PODs match your search.';
        var noOptions = root.getAttribute('data-no-options') || 'No PODs are available.';
        var podById = new Map();
        var selected = new Set();

        optionElements.forEach(function (option) {
            var pod = {
                id: option.getAttribute('data-pod-id'),
                name: option.getAttribute('data-pod-name'),
                label: option.getAttribute('data-pod-label') || option.getAttribute('data-pod-name'),
                search: (option.getAttribute('data-pod-search') || option.textContent || '').toLowerCase()
            };
            podById.set(pod.id, pod);
            if (option.getAttribute('data-selected') === 'true') {
                selected.add(pod.id);
            }
        });

        function setOpen(open) {
            list.hidden = !open;
            search.setAttribute('aria-expanded', open ? 'true' : 'false');
            if (open) {
                filter(search.value);
            }
        }

        function render() {
            chips.textContent = '';
            inputs.textContent = '';

            Array.from(selected)
                .map(function (id) { return podById.get(id); })
                .filter(Boolean)
                .sort(function (a, b) { return a.name.localeCompare(b.name); })
                .forEach(function (pod) {
                    chips.appendChild(createChip(root, selected, pod, render));

                    var input = document.createElement('input');
                    input.type = 'hidden';
                    input.name = 'PODIds';
                    input.value = pod.id;
                    inputs.appendChild(input);
                });

            if (chips.children.length === 0) {
                var placeholder = document.createElement('li');
                placeholder.className = 'pod-chip-placeholder';
                placeholder.textContent = 'No PODs selected';
                chips.appendChild(placeholder);
            }

            optionElements.forEach(function (option) {
                var isSelected = selected.has(option.getAttribute('data-pod-id'));
                option.classList.toggle('is-selected', isSelected);
                option.setAttribute('aria-selected', isSelected ? 'true' : 'false');
            });
        }

        function filter(term) {
            var query = (term || '').trim().toLowerCase();
            var visible = 0;

            optionElements.forEach(function (option) {
                var matches = !query || (option.getAttribute('data-pod-search') || '').toLowerCase().indexOf(query) !== -1;
                option.hidden = !matches;
                if (matches) {
                    visible++;
                }
            });

            if (empty) {
                if (optionElements.length === 0) {
                    empty.textContent = noOptions;
                    empty.hidden = false;
                } else if (visible === 0) {
                    empty.textContent = noResults;
                    empty.hidden = false;
                } else {
                    empty.hidden = true;
                }
            }
        }

        function visibleOptions() {
            return optionElements.filter(function (option) { return !option.hidden; });
        }

        function toggle(option) {
            if (!option) {
                return;
            }

            var id = option.getAttribute('data-pod-id');
            if (selected.has(id)) {
                selected.delete(id);
            } else {
                selected.add(id);
            }

            render();
            search.focus();
        }

        search.addEventListener('focus', function () {
            setOpen(true);
        });

        search.addEventListener('input', function () {
            setOpen(true);
            filter(search.value);
        });

        search.addEventListener('keydown', function (event) {
            var options = visibleOptions();

            if (event.key === 'ArrowDown' && options.length > 0) {
                event.preventDefault();
                setOpen(true);
                options[0].focus();
                return;
            }

            if (event.key === 'Enter' && options.length > 0 && !list.hidden) {
                event.preventDefault();
                toggle(options[0]);
                return;
            }

            if (event.key === 'Escape') {
                setOpen(false);
                return;
            }

            if (event.key === 'Backspace' && search.value === '' && selected.size > 0) {
                var last = Array.from(selected).map(function (id) { return podById.get(id); }).filter(Boolean).pop();
                if (last) {
                    event.preventDefault();
                    selected.delete(last.id);
                    render();
                }
            }
        });

        optionElements.forEach(function (option) {
            option.addEventListener('click', function () {
                toggle(option);
            });

            option.addEventListener('keydown', function (event) {
                var options = visibleOptions();
                var index = options.indexOf(option);

                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    toggle(option);
                } else if (event.key === 'ArrowDown' && index < options.length - 1) {
                    event.preventDefault();
                    options[index + 1].focus();
                } else if (event.key === 'ArrowUp') {
                    event.preventDefault();
                    if (index > 0) {
                        options[index - 1].focus();
                    } else {
                        search.focus();
                    }
                } else if (event.key === 'Escape') {
                    event.preventDefault();
                    setOpen(false);
                    search.focus();
                }
            });
        });

        document.addEventListener('click', function (event) {
            if (!root.contains(event.target)) {
                setOpen(false);
            }
        });

        render();
        filter('');
    }

    document.querySelectorAll('[data-pod-multiselect]').forEach(init);
})();
