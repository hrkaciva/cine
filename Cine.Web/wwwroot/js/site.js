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

// Saved movies that are no longer in the scraped schedule have no server-rendered card,
// so build a fallback card from the wishlist entry itself. The movie key is "DetailUrl|Title".
const createWishlistFallbackCard = (movieKey, savedMovie) => {
    const separatorIndex = movieKey.indexOf('|');
    if (separatorIndex < 1) return null;
    const detailUrl = movieKey.slice(0, separatorIndex);
    const title = movieKey.slice(separatorIndex + 1) || savedMovie?.title || 'Untitled film';

    const card = document.createElement('article');
    card.className = 'wishlist-card';
    card.dataset.wishlistCard = '';
    card.dataset.movieKey = movieKey;

    const index = document.createElement('div');
    index.className = 'movie-index';
    card.append(index);

    const details = document.createElement('div');
    const titleRow = document.createElement('div');
    titleRow.className = 'wishlist-title';
    const heading = document.createElement('h2');
    heading.textContent = title;
    const removeButton = document.createElement('button');
    removeButton.type = 'button';
    removeButton.className = 'remove-wishlist-button';
    removeButton.dataset.removeWishlist = '';
    removeButton.textContent = 'Remove';
    titleRow.append(heading, removeButton);
    details.append(titleRow);

    if (detailUrl) {
        const detailLink = document.createElement('a');
        detailLink.className = 'movie-link';
        detailLink.href = detailUrl;
        detailLink.target = '_blank';
        detailLink.rel = 'noopener';
        detailLink.append('View film details ');
        const arrow = document.createElement('span');
        arrow.textContent = '↗';
        detailLink.append(arrow);
        details.append(detailLink);
    }

    const times = document.createElement('div');
    times.className = 'wishlist-times';
    const day = document.createElement('div');
    day.className = 'wishlist-day';
    const dayLabel = document.createElement('span');
    dayLabel.textContent = 'NOT CURRENTLY SHOWING';
    const timeList = document.createElement('div');
    timeList.className = 'time-list';
    const timePill = document.createElement('span');
    timePill.className = 'time-pill';
    timePill.textContent = 'No scheduled screenings';
    timeList.append(timePill);
    day.append(dayLabel, timeList);
    times.append(day);
    details.append(times);
    card.append(details);
    return card;
};

const wishlistGrid = document.querySelector('[data-wishlist-list]');
if (wishlistGrid) {
    const renderedMovieKeys = new Set([...document.querySelectorAll('[data-wishlist-card]')].map((card) => card.dataset.movieKey));
    Object.entries(readWishlist()).forEach(([movieKey, savedMovie]) => {
        if (renderedMovieKeys.has(movieKey)) return;
        const fallbackCard = createWishlistFallbackCard(movieKey, savedMovie);
        if (fallbackCard) wishlistGrid.append(fallbackCard);
    });

    // Keep the grid alphabetical so fallback cards blend into the server-rendered list.
    [...wishlistGrid.children]
        .sort((first, second) => (first.querySelector('h2')?.textContent ?? '').localeCompare(second.querySelector('h2')?.textContent ?? ''))
        .forEach((card) => wishlistGrid.append(card));
}

const updateWishlistList = () => {
    const movies = readWishlist();
    let visibleCount = 0;

    document.querySelectorAll('[data-wishlist-card]').forEach((card) => {
        const visible = Boolean(movies[card.dataset.movieKey]);
        card.classList.toggle('is-hidden-by-filter', !visible);
        if (!visible) return;
        visibleCount++;
        const index = card.querySelector('.movie-index');
        if (index) index.textContent = `${visibleCount}.0`;
    });

    const emptyState = document.querySelector('[data-wishlist-empty]');
    if (emptyState) emptyState.hidden = visibleCount > 0;
};

document.querySelectorAll('[data-wishlist-card]').forEach((card) => {
    card.querySelector('[data-remove-wishlist]')?.addEventListener('click', () => {
        const movies = readWishlist();
        delete movies[card.dataset.movieKey];
        writeWishlist(movies);
        updateWishlistCount();
        updateWishlistList();
    });
});

updateWishlistList();
updateWishlistCount();

applyWatchedFilter();

// "More info" dialogs. The native <dialog> element provides focus trapping, Esc handling and the backdrop.
document.querySelectorAll('[data-movie-dialog-open]').forEach((button) => {
    button.addEventListener('click', () => {
        button.closest('article')?.querySelector('[data-movie-dialog]')?.showModal();
    });
});

document.querySelectorAll('[data-movie-dialog]').forEach((dialog) => {
    dialog.addEventListener('click', (event) => {
        // Clicks on the ::backdrop area target the dialog element itself, not its body.
        if (event.target === dialog) dialog.close();
    });

    dialog.querySelector('[data-movie-dialog-close]')?.addEventListener('click', () => dialog.close());
});
