document.addEventListener('DOMContentLoaded', function () {
    var active = document.querySelector('.oh-manage-nav__list .nav-link.active');
    if (active && typeof active.scrollIntoView === 'function') {
        active.scrollIntoView({ block: 'nearest', inline: 'center' });
    }
});
