// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

const dateStrip = document.querySelector('#date-strip');
const dateStripPositionKey = 'cinestar-date-strip-position';

if (dateStrip) {
    const savedPosition = sessionStorage.getItem(dateStripPositionKey);
    if (savedPosition !== null) {
        dateStrip.scrollLeft = Number(savedPosition);
    }

    dateStrip.querySelectorAll('a').forEach((dateLink) => {
        dateLink.addEventListener('click', () => {
            sessionStorage.setItem(dateStripPositionKey, String(dateStrip.scrollLeft));
        });
    });
}
