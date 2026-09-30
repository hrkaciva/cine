// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

const dateStrip = document.querySelector('#date-strip');
const dateStripPositionKey = 'cinestar-date-strip-position';
const watchedMoviesKey = 'cinestar-watched-movies';
const hideWatchedKey = 'cinestar-hide-watched';
const wishlistKey = 'cinestar-wishlist-v2';

const readWishlist = () => {
    try {
        return JSON.parse(localStorage.getItem(wishlistKey) || '{}');
    } catch {
        return {};
    }
};

const writeWishlist = (movies) => localStorage.setItem(wishlistKey, JSON.stringify(movies));
const updateWishlistCount = () => document.querySelectorAll('[data-wishlist-count]').forEach((count) => { count.textContent = Object.keys(readWishlist()).length; });

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

const readWatchedMovies = () => {
    try {
        return JSON.parse(localStorage.getItem(watchedMoviesKey) || '{}');
    } catch {
        return {};
    }
};

const writeWatchedMovies = (movies) => {
    localStorage.setItem(watchedMoviesKey, JSON.stringify(movies));
};

const updateWatchCard = (card, watchedMovie) => {
    const button = card.querySelector('[data-watch-action]');
    const form = card.querySelector('[data-review-form]');
    const rating = card.querySelector('[data-rating-input]');
    const ratingDisplay = card.querySelector('[data-rating-display]');
    const input = card.querySelector('[data-review-input]');

    card.classList.toggle('is-watched', Boolean(watchedMovie));
    button.textContent = watchedMovie ? 'Watched - Edit review' : 'Mark as watched';
    button.classList.toggle('is-active', Boolean(watchedMovie));
    rating.value = watchedMovie?.rating || '';
    ratingDisplay.textContent = watchedMovie?.rating ? `Rating ${watchedMovie.rating}/10` : '';
    ratingDisplay.hidden = !watchedMovie?.rating;
    input.value = watchedMovie?.review || '';
    form.hidden = true;
};

const watchedFilter = document.querySelector('#hide-watched');
const visibleMovieCount = document.querySelector('#visible-movie-count');
const applyWatchedFilter = () => {
    const hideWatched = watchedFilter?.checked ?? true;
    let visibleCount = 0;

    document.querySelectorAll('.movie-card[data-movie-key]').forEach((card) => {
        const isHidden = hideWatched && card.classList.contains('is-watched');
        card.classList.toggle('is-hidden-by-filter', isHidden);
        if (!isHidden) visibleCount++;
    });

    if (visibleMovieCount) visibleMovieCount.textContent = String(visibleCount).padStart(2, '0');
};

if (watchedFilter) {
    watchedFilter.checked = localStorage.getItem(hideWatchedKey) !== 'false';
    watchedFilter.addEventListener('change', () => {
        localStorage.setItem(hideWatchedKey, String(watchedFilter.checked));
        applyWatchedFilter();
    });
}

document.querySelectorAll('.movie-card[data-movie-key]').forEach((card) => {
    const movies = readWatchedMovies();
    updateWatchCard(card, movies[card.dataset.movieKey]);

    card.querySelector('[data-watch-action]').addEventListener('click', () => {
        const currentMovies = readWatchedMovies();
        const movieKey = card.dataset.movieKey;

        if (!currentMovies[movieKey]) {
            currentMovies[movieKey] = {
                title: card.dataset.movieTitle,
                rating: '',
                review: '',
                watchedAt: new Date().toISOString()
            };
            writeWatchedMovies(currentMovies);
            updateWatchCard(card, currentMovies[movieKey]);
        }

        card.querySelector('[data-review-form]').hidden = false;
        card.querySelector('[data-review-input]').focus();
    });

    card.querySelector('[data-review-form]').addEventListener('submit', (event) => {
        event.preventDefault();
        const currentMovies = readWatchedMovies();
        const movieKey = card.dataset.movieKey;
        const existingMovie = currentMovies[movieKey];

        if (!existingMovie) return;

        existingMovie.rating = card.querySelector('[data-rating-input]').value;
        existingMovie.review = card.querySelector('[data-review-input]').value.trim();
        writeWatchedMovies(currentMovies);
        updateWatchCard(card, existingMovie);
        applyWatchedFilter();
    });

    card.querySelector('[data-cancel-review]').addEventListener('click', () => {
        updateWatchCard(card, readWatchedMovies()[card.dataset.movieKey]);
    });
});

document.querySelectorAll('[data-wishlist-action]').forEach((button) => {
    const card = button.closest('[data-movie-key]');
    const movieKey = card.dataset.movieKey;
    const update = () => {
        const saved = Boolean(readWishlist()[movieKey]);
        button.textContent = saved ? 'In wishlist' : 'Add to wishlist';
        button.classList.toggle('is-active', saved);
    };
    update();
    button.addEventListener('click', () => {
        const movies = readWishlist();
        if (movies[movieKey]) delete movies[movieKey];
        else movies[movieKey] = { title: card.dataset.movieTitle };
        writeWishlist(movies);
        update();
        updateWishlistCount();
    });
});

const wishlistMovies = readWishlist();
document.querySelectorAll('[data-wishlist-card]').forEach((card) => {
    card.classList.toggle('is-hidden-by-filter', !wishlistMovies[card.dataset.movieKey]);
    card.querySelector('[data-remove-wishlist]')?.addEventListener('click', () => {
        const movies = readWishlist();
        delete movies[card.dataset.movieKey];
        writeWishlist(movies);
        card.classList.add('is-hidden-by-filter');
        updateWishlistCount();
        const hasVisibleMovies = [...document.querySelectorAll('[data-wishlist-card]')].some((item) => !item.classList.contains('is-hidden-by-filter'));
        const emptyState = document.querySelector('[data-wishlist-empty]');
        if (emptyState) emptyState.hidden = hasVisibleMovies;
    });
});

const wishlistEmpty = document.querySelector('[data-wishlist-empty]');
if (wishlistEmpty) wishlistEmpty.hidden = [...document.querySelectorAll('[data-wishlist-card]')].some((card) => !card.classList.contains('is-hidden-by-filter'));
updateWishlistCount();

applyWatchedFilter();
