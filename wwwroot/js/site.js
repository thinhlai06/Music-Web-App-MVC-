(function () {
    const state = {
        isPlaying: false,
        currentSong: null,
        playlists: [],
        isAuthenticated: false,
        pendingPlaylistSongId: null,
        searchTimeout: null,
        playbackSpeed: 1.0,
        queue: null, // New: List of song objects
        queueIndex: -1, // New: Current index in queue
        contextQueue: [], // New: Temporary queue from current view (Album)
        isShuffled: false, // Shuffle state
        loopMode: 'off', // Loop mode: 'off', 'all', 'one'
        originalQueue: null // Original queue before shuffle
    };

    const els = {
        audio: document.getElementById('audio-player'),
        playIcon: document.getElementById('play-icon'),
        playerImg: document.getElementById('player-img'),
        playerTitle: document.getElementById('player-title'),
        playerArtist: document.getElementById('player-artist'),
        playerFavorite: document.getElementById('player-favorite'),
        progress: document.getElementById('player-progress'),
        progressFill: document.getElementById('player-progress-fill'),
        playerCurrent: document.getElementById('player-current'),
        playerDuration: document.getElementById('player-duration'),
        lyricsContent: document.getElementById('lyrics-content'),
        lyricsOverlay: document.getElementById('lyrics-view'),
        authModal: document.getElementById('auth-modal'),
        playlistModal: document.getElementById('playlist-modal'),
        playlistList: document.getElementById('playlist-list'),
        toast: document.getElementById('toast'),
        searchBox: document.getElementById('search-box'),
        searchPanel: document.getElementById('search-results'),
        searchColumns: document.getElementById('search-columns'),
        searchEmpty: document.getElementById('search-empty'),
        volumeBar: document.getElementById('volume-bar'),
        volumeFill: document.getElementById('volume-fill'),
        volumeIcon: document.getElementById('volume-icon')
    };

    function formatTime(seconds) {
        if (!Number.isFinite(seconds)) return '0:00';
        const m = Math.floor(seconds / 60);
        const s = Math.floor(seconds % 60);
        return `${m}:${s.toString().padStart(2, '0')}`;
    }

    function updatePlayerUI() {
        const song = state.currentSong;
        if (!song) return;
        els.playerImg.src = song.cover;
        els.playerTitle.textContent = song.title;
        els.playerArtist.textContent = song.artist;
        els.playerFavorite.classList.toggle('active', song.isFavorite);
        els.playerFavorite.innerHTML = song.isFavorite ? '<i class="fa-solid fa-heart"></i>' : '<i class="fa-regular fa-heart"></i>';
    }

    function playSong(song) {
        if (!song.audio) {
            showToast('Bản thu âm chưa sẵn sàng.');
            return;
        }

        state.currentSong = song;
        els.audio.src = song.audio;
        els.audio.playbackRate = state.playbackSpeed;
        els.audio.play().then(() => {
            state.isPlaying = true;
            els.playIcon.classList.remove('fa-circle-play');
            els.playIcon.classList.add('fa-circle-pause');
            updatePlayerUI();
            recordPlay(song.id);
        }).catch(() => {
            showToast('Không thể phát bài hát.');
        });
    }

    // New: Play a specific list of songs
    window.playQueue = function (songs, startIndex = 0) {
        if (!songs || !songs.length) return;

        // If shuffle is already enabled, apply it to new queue
        if (state.isShuffled) {
            state.originalQueue = [...songs]; // Save original
            state.queue = shuffleArray(songs);

            // Find the start song in shuffled queue
            const startSongId = songs[startIndex]?.id;
            if (startSongId) {
                const newIndex = state.queue.findIndex(s => s.id === startSongId);
                state.queueIndex = newIndex !== -1 ? newIndex : 0;
            } else {
                state.queueIndex = 0;
            }
        } else {
            state.queue = songs;
            state.queueIndex = startIndex;
        }

        playSong(state.queue[state.queueIndex]);
    };

    function playSongFromCard(card) {
        const song = {
            id: Number(card.dataset.songId),
            title: card.dataset.title,
            artist: card.dataset.artist,
            cover: card.dataset.cover,
            audio: card.dataset.audio || '',
            durationLabel: card.dataset.duration,
            isFavorite: card.dataset.favorite === 'true' || card.dataset.favorite === 'True'
        };

        // If Playing from a card NOT via playQueue (e.g. Home, Search), clear the explicit queue
        // to fallback to DOM-based behavior (All Visible Songs) or just play this song.
        // However, if we want "Next" to work in Home, we typically relied on getAllSongCards().
        // So clearing state.queue ensures playNext() falls back to getAllSongCards().
        state.queue = null;
        state.queueIndex = -1;

        playSong(song);
    }

    function togglePlay() {
        if (!state.currentSong) {
            showToast('Chọn một bài hát để phát.');
            return;
        }

        state.isPlaying = !state.isPlaying;
        if (state.isPlaying) {
            els.audio.play();
            els.playIcon.classList.remove('fa-circle-play');
            els.playIcon.classList.add('fa-circle-pause');
        } else {
            els.audio.pause();
            els.playIcon.classList.remove('fa-circle-pause');
            els.playIcon.classList.add('fa-circle-play');
        }
    }

    function updateProgress() {
        if (!els.audio.duration) return;
        const percent = (els.audio.currentTime / els.audio.duration) * 100;
        els.progressFill.style.width = `${percent}%`;
        els.playerCurrent.textContent = formatTime(els.audio.currentTime);
        els.playerDuration.textContent = formatTime(els.audio.duration);
    }

    function seekBy(seconds) {
        if (!els.audio.duration) return;
        let target = els.audio.currentTime + seconds;
        if (target < 0) target = 0;
        if (target > els.audio.duration) target = els.audio.duration;
        els.audio.currentTime = target;
        updateProgress();
    }

    function seek(event) {
        if (!els.audio.duration) return;
        const rect = els.progress.getBoundingClientRect();
        const percent = (event.clientX - rect.left) / rect.width;
        els.audio.currentTime = percent * els.audio.duration;
    }

    function toggleFavorite(songId, sourceBtn) {
        if (!state.isAuthenticated) {
            toggleAuthModal(true);
            return;
        }

        fetch(`/favorites/${songId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        })
            .then(res => res.json())
            .then(data => {
                if (!data.success) return;
                const isFavorite = data.isFavorite;
                document.querySelectorAll(`.song-card[data-song-id="${songId}"]`).forEach(card => {
                    card.dataset.favorite = isFavorite.toString();
                    const btn = card.querySelector('.favorite-toggle');
                    if (btn) btn.classList.toggle('active', isFavorite);
                });
                // Update Profile Fav Grid
                const favGrid = document.getElementById('favorite-songs-grid');
                if (favGrid) {
                    if (isFavorite) {
                        const anyCard = document.querySelector(`.song-card[data-song-id="${songId}"]`);
                        if (anyCard) favGrid.prepend(anyCard.cloneNode(true));
                    } else {
                        const existing = favGrid.querySelector(`.song-card[data-song-id="${songId}"]`);
                        if (existing) existing.remove();
                    }
                }
                if (state.currentSong?.id === songId) {
                    state.currentSong.isFavorite = isFavorite;
                    updatePlayerUI();
                }
                showToast(isFavorite ? 'Đã thêm vào yêu thích' : 'Đã gỡ khỏi yêu thích');
            });
    }

    // PLAYLIST LOGIC
    function openPlaylistModal(songId) {
        if (!state.isAuthenticated) {
            toggleAuthModal(true);
            return;
        }
        const canSelectSong = Number.isInteger(songId);
        state.pendingPlaylistSongId = canSelectSong ? songId : null;

        document.getElementById('playlist-list-container').classList.remove('hidden');
        document.getElementById('playlist-create-container').classList.add('hidden');

        renderPlaylistList();
        els.playlistModal.classList.add('visible');
    }

    function renderPlaylistList() {
        els.playlistList.innerHTML = '';

        const createItem = document.createElement('div');
        createItem.className = 'playlist-item create-item';
        createItem.innerHTML = `<i class="fa-solid fa-plus-circle" style="font-size: 20px; color: var(--purple-primary); margin-right: 15px;"></i> <span>Tạo playlist mới</span>`;
        createItem.addEventListener('click', () => {
            document.getElementById('playlist-list-container').classList.add('hidden');
            document.getElementById('playlist-create-container').classList.remove('hidden');
        });
        els.playlistList.appendChild(createItem);

        state.playlists.forEach(pl => {
            const item = document.createElement('div');
            item.className = 'playlist-item';
            const cover = pl.coverUrl || `https://picsum.photos/seed/playlist-${pl.id}/50/50`;
            item.innerHTML = `
                <img src="${cover}" style="width: 40px; height: 40px; border-radius: 4px; object-fit: cover; margin-right: 15px;">
                <div style="flex: 1;">
                    <div style="font-weight: 600;">${pl.title}</div>
                    <div style="font-size: 12px; color: var(--text-secondary);">${pl.subtitle || '0 bài hát'}</div>
                </div>
            `;

            if (state.pendingPlaylistSongId) {
                item.addEventListener('click', () => addSongToPlaylist(pl.id, pl.title));
            } else {
                item.classList.add('disabled');
            }
            els.playlistList.appendChild(item);
        });
    }

    function closePlaylistModal() {
        els.playlistModal.classList.remove('visible');
        state.pendingPlaylistSongId = null;
        const formInput = document.querySelector('#playlist-create-form input[name="playlistName"]');
        if (formInput) formInput.value = '';
    }

    function addSongToPlaylist(playlistId, playlistTitle) {
        if (!state.pendingPlaylistSongId) return;
        fetch(`/playlists/${playlistId}/songs`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ songId: state.pendingPlaylistSongId })
        }).then(async res => {
            if (res.ok) {
                showToast(`Đã thêm vào playlist • ${playlistTitle}`);

                // Update local state to reflect change (optimistic update)
                const pl = state.playlists.find(p => p.id === playlistId);
                if (pl) {
                    const match = (pl.subtitle || '0').match(/\d+/);
                    const count = match ? parseInt(match[0]) : 0;
                    const newCount = count + 1;
                    pl.subtitle = `${newCount} bài hát`;

                    // Update Profile Grid Card
                    const profileCard = document.querySelector(`#profile-playlists-grid .card[data-playlist-id="${playlistId}"] .card-subtitle`);
                    if (profileCard) profileCard.textContent = pl.subtitle;
                }

                closePlaylistModal();
            } else {
                const data = await res.json().catch(() => ({}));
                showToast(data.message || 'Không thể thêm bài hát.');
            }
        });
    }

    function appendPersonalPlaylistCard(playlist) {
        const grid = document.getElementById('profile-playlists-grid');
        if (!grid) return;
        const cover = playlist.coverUrl || `https://picsum.photos/seed/user-playlist-${playlist.id}/300/300`;
        const card = document.createElement('div');
        card.className = 'card';
        card.dataset.playlistId = playlist.id;
        card.innerHTML = `
            <div class="card-img-wrapper">
                <img src="${cover}" class="card-img" alt="${playlist.title}">
                <div class="card-overlay">
                    <div class="play-btn-circle"><i class="fa-solid fa-play"></i></div>
                </div>
            </div>
            <div class="card-title">${playlist.title}</div>
            <div class="card-subtitle">${playlist.subtitle || 'Playlist mới'}</div>
        `;
        grid.appendChild(card);
    }

    function createPlaylist(name) {
        if (!name) return;
        fetch('/playlists', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name })
        })
            .then(res => res.json())
            .then(data => {
                if (!data.success) {
                    showToast(data.message ?? 'Không thể tạo playlist');
                    return;
                }
                state.playlists.unshift(data.playlist);
                appendPersonalPlaylistCard(data.playlist);
                if (state.pendingPlaylistSongId) {
                    addSongToPlaylist(data.playlist.id, data.playlist.title);
                } else {
                    showToast('Đã tạo playlist mới');
                    closePlaylistModal();
                }
            });
    }

    function createGridSongCard(song) {
        const div = document.createElement('div');
        div.className = 'card song-card';
        div.dataset.songId = song.id;
        div.dataset.title = song.title;
        div.dataset.artist = song.artist;
        div.dataset.cover = song.cover;
        div.dataset.audio = song.audio;
        div.dataset.duration = song.durationLabel;
        div.dataset.favorite = song.isFavorite;

        div.innerHTML = `
            <div class="card-actions">
                <button class="circle-btn favorite-toggle ${song.isFavorite ? 'active' : ''}"><i class="fa-solid fa-heart"></i></button>
                <button class="circle-btn playlist-add"><i class="fa-solid fa-plus"></i></button>
            </div>
            <div class="card-img-wrapper">
                <img src="${song.cover}" class="card-img" alt="${song.title}">
                <div class="card-overlay">
                    <div class="play-btn-circle"><i class="fa-solid fa-play"></i></div>
                </div>
            </div>
            <div class="card-title">${song.title}</div>
            <div class="card-subtitle">${song.artist}</div>
        `;
        return div;
    }

    function recordPlay(songId) {
        // if (!state.isAuthenticated) return; // Allow guests to record views
        fetch(`/player/play/${songId}`, { method: 'POST' });

        const historyGrid = document.getElementById('recent-songs-grid');
        if (historyGrid && state.currentSong && state.currentSong.id === songId) {
            const existing = historyGrid.querySelector(`.song-card[data-song-id="${songId}"]`);
            if (existing) existing.remove();

            const newCard = createGridSongCard(state.currentSong);
            historyGrid.prepend(newCard);

            while (historyGrid.children.length > 12) {
                if (historyGrid.lastElementChild) historyGrid.lastElementChild.remove();
            }
        }
    }

    function toggleLyrics() {
        if (!state.currentSong) {
            showToast('Hãy phát bài hát để xem lời.');
            return;
        }
        els.lyricsOverlay.classList.toggle('open');
        if (els.lyricsOverlay.classList.contains('open')) {
            loadLyrics(state.currentSong.id);
        }
    }

    function loadLyrics(songId) {
        fetch(`/lyrics/${songId}`)
            .then(res => res.json())
            .then(data => {
                if (!data.success) return;
                document.getElementById('lyrics-song-title').textContent = data.data.title;
                document.getElementById('lyrics-artist').textContent = data.data.artist;
                els.lyricsContent.innerHTML = '';
                data.data.lyrics.forEach((line, idx) => {
                    const p = document.createElement('p');
                    p.className = 'lyric-line' + (idx === 1 ? ' active' : '');
                    p.textContent = line;
                    els.lyricsContent.appendChild(p);
                });
            });
    }

    function bindEditProfile() {
        const modal = document.getElementById('edit-profile-modal');
        const form = document.getElementById('edit-profile-form');
        const btn = document.getElementById('profile-edit-btn');
        const preview = document.getElementById('edit-profile-preview');
        if (btn) {
            btn.addEventListener('click', () => {
                if (!state.isAuthenticated) return;
                const user = window.musicModel?.currentUser;
                if (user) {
                    form.displayName.value = user.displayName || '';
                    form.avatarUrl.value = user.avatarUrl || '';
                    preview.src = user.avatarUrl || 'https://ui-avatars.com/api/?name=User';
                }
                toggleEditProfileModal(true);
            });
        }
        if (form) {
            form.avatarUrl.addEventListener('input', (e) => {
                preview.src = e.target.value || 'https://ui-avatars.com/api/?name=User';
            });
            form.addEventListener('submit', e => {
                e.preventDefault();
                const payload = {
                    displayName: form.displayName.value,
                    avatarUrl: form.avatarUrl.value || null,
                    currentPassword: form.currentPassword.value || null,
                    newPassword: form.newPassword.value || null
                };
                fetch('/account/update-profile', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                })
                    .then(res => res.json())
                    .then(data => {
                        if (data.success) {
                            showToast('Cập nhật hồ sơ thành công');
                            toggleEditProfileModal(false);
                            setTimeout(() => window.location.reload(), 1000);
                        } else {
                            showToast(data.message || 'Có lỗi xảy ra');
                        }
                    });
            });
        }
    }

    function toggleEditProfileModal(forceOpen) {
        const modal = document.getElementById('edit-profile-modal');
        if (!modal) return;
        if (forceOpen === true) modal.classList.add('visible');
        else if (forceOpen === false) modal.classList.remove('visible');
        else modal.classList.toggle('visible');
    }

    function bindAuthForm() {
        const tabs = document.querySelectorAll('.auth-tab');
        const form = document.getElementById('auth-form');
        const registerFields = document.querySelectorAll('[data-auth-field="register"]');
        let mode = 'login';
        tabs.forEach(tab => {
            tab.addEventListener('click', () => {
                tabs.forEach(t => t.classList.remove('active'));
                tab.classList.add('active');
                mode = tab.dataset.mode;
                document.getElementById('auth-title').textContent = mode === 'login' ? 'Đăng nhập' : 'Đăng ký';
                const displayNameFn = form.querySelector('input[name="displayName"]');
                const confirmPassFn = form.querySelector('input[name="confirmPassword"]');
                if (displayNameFn) displayNameFn.style.display = mode === 'register' ? 'block' : 'none';
                if (confirmPassFn) confirmPassFn.style.display = mode === 'register' ? 'block' : 'none';
                if (mode === 'login') {
                    if (displayNameFn) displayNameFn.value = '';
                    if (confirmPassFn) confirmPassFn.value = '';
                }
                document.getElementById('auth-error').style.display = 'none';
            });
        });
        form.addEventListener('submit', e => {
            e.preventDefault();
            const formData = new FormData(form);
            const payload = {
                email: formData.get('email'),
                password: formData.get('password')
            };
            if (mode === 'register') {
                const confirmPass = formData.get('confirmPassword');
                if (payload.password !== confirmPass) {
                    const errorEl = document.getElementById('auth-error');
                    errorEl.textContent = 'Mật khẩu nhập lại không khớp.';
                    errorEl.style.display = 'block';
                    return;
                }
                payload.displayName = formData.get('displayName') || payload.email;
                payload.confirmPassword = confirmPass;
            }
            fetch(`/account/${mode}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            })
                .then(async res => {
                    if (!res.ok) {
                        const data = await res.json();
                        throw new Error(data.message || data.errors?.join(', ') || 'Có lỗi xảy ra');
                    }
                    return res.json();
                })
                .then(() => window.location.reload())
                .catch(err => {
                    const errorEl = document.getElementById('auth-error');
                    errorEl.textContent = err.message;
                    errorEl.style.display = 'block';
                });
        });
        const logoutBtn = document.getElementById('logout-btn');
        if (logoutBtn) {
            logoutBtn.addEventListener('click', () => {
                fetch('/account/logout', { method: 'POST' }).then(() => window.location.reload());
            });
        }
    }

    function toggleAuthModal(forceOpen) {
        if (forceOpen === true) els.authModal.classList.add('visible');
        else if (forceOpen === false) els.authModal.classList.remove('visible');
        else els.authModal.classList.toggle('visible');
    }

    function showToast(message) {
        if (!els.toast) return;
        els.toast.textContent = message;
        els.toast.classList.add('visible');
        clearTimeout(showToast.timeout);
        showToast.timeout = setTimeout(() => els.toast.classList.remove('visible'), 2500);
    }

    function handleSongCardClicks() {
        document.addEventListener('click', event => {
            const favoriteBtn = event.target.closest('.favorite-toggle');
            if (favoriteBtn) {
                event.stopPropagation();
                const card = favoriteBtn.closest('.song-card');
                if (card) {
                    toggleFavorite(Number(card.dataset.songId), favoriteBtn);
                } else if (state.currentSong) {
                    toggleFavorite(state.currentSong.id, favoriteBtn);
                }
                return;
            }
            const playlistBtn = event.target.closest('.playlist-add');
            if (playlistBtn) {
                event.stopPropagation();
                const card = playlistBtn.closest('.song-card');
                if (card) {
                    openPlaylistModal(Number(card.dataset.songId));
                }
                return;
            }
            const shareBtn = event.target.closest('.share-btn');
            if (shareBtn) {
                event.stopPropagation();
                const card = shareBtn.closest('.song-card');
                if (card) {
                    const title = card.dataset.title;
                    const artist = card.dataset.artist;
                    const text = `Nghe bài hát ${title} - ${artist} tại MusicWeb!`;
                    navigator.clipboard.writeText(text).then(() => {
                        showToast('Đã sao chép nội dung chia sẻ!');
                    });
                }
                return;
            }
            const downloadBtn = event.target.closest('.download-btn');
            if (downloadBtn) {
                event.stopPropagation();
                // Allow default behavior (download)
                return;
            }
            // Handle Album Song Row Click (Queue Mode)
            const songRow = event.target.closest('.song-row');
            if (songRow && !event.target.closest('.circle-btn') && !event.target.closest('.row-heart')) {
                const songId = Number(songRow.dataset.songId);
                // Find index in contextQueue
                if (state.contextQueue && state.contextQueue.length > 0) {
                    const index = state.contextQueue.findIndex(s => s.id === songId);
                    if (index !== -1) {
                        playQueue(state.contextQueue, index);
                    }
                }
                return;
            }

            // Handle Standard Song Card Click (Clear Queue Mode)
            const songCard = event.target.closest('.song-card');
            if (songCard && !event.target.closest('.circle-btn') && !event.target.closest('.row-heart')) {
                playSongFromCard(songCard);
            }
        });
    }

    // Global helper for "Play All" button in Album view
    window.playAlbumAll = function () {
        if (state.contextQueue && state.contextQueue.length > 0) {
            playQueue(state.contextQueue, 0);
        }
    };

    function bindPlaylistModal() {
        document.getElementById('open-playlist-modal')?.addEventListener('click', () => openPlaylistModal(null));
        document.getElementById('create-playlist-card')?.addEventListener('click', () => openPlaylistModal(null));
        document.getElementById('profile-open-playlist')?.addEventListener('click', event => {
            event.preventDefault();
            openPlaylistModal(null);
        });
        document.getElementById('playlist-create-form')?.addEventListener('submit', event => {
            event.preventDefault();
            const name = event.target.playlistName.value.trim();
            createPlaylist(name);
            event.target.reset();
        });
        els.playlistModal?.addEventListener('click', event => {
            if (event.target === els.playlistModal || event.target.id === 'playlist-close') closePlaylistModal();
        });
    }

    function bindPlayerControls() {
        document.getElementById('btn-play').addEventListener('click', togglePlay);
        document.getElementById('btn-next')?.addEventListener('click', playNext);
        document.getElementById('btn-prev')?.addEventListener('click', playPrev);
        document.getElementById('btn-shuffle')?.addEventListener('click', toggleShuffle);
        document.getElementById('btn-repeat')?.addEventListener('click', toggleLoop);
        els.audio.addEventListener('timeupdate', updateProgress);
        els.audio.addEventListener('loadedmetadata', updateProgress);
        els.audio.addEventListener('ended', playNext);
        els.progress.addEventListener('click', seek);
        els.playerFavorite.addEventListener('click', () => state.currentSong && toggleFavorite(state.currentSong.id, els.playerFavorite));
        els.volumeBar?.addEventListener('click', event => {
            const rect = els.volumeBar.getBoundingClientRect();
            const percent = (event.clientX - rect.left) / rect.width;
            setVolume(percent);
            if (els.audio.volume > 0) lastVolume = els.audio.volume;
        });
        els.volumeIcon?.addEventListener('click', () => {
            if (!els.audio) return;
            if (els.audio.volume > 0) {
                lastVolume = els.audio.volume;
                setVolume(0);
            } else {
                setVolume(lastVolume || 0.7);
            }
        });
        const speedBtn = document.getElementById('btn-speed');
        if (speedBtn) {
            speedBtn.addEventListener('click', toggleSpeed);
        }
    }

    function toggleSpeed() {
        const speeds = [1.0, 1.25, 1.5, 2.0, 0.5];
        let currentIndex = speeds.indexOf(state.playbackSpeed);
        if (currentIndex === -1) currentIndex = 0;
        const nextIndex = (currentIndex + 1) % speeds.length;
        state.playbackSpeed = speeds[nextIndex];
        if (els.audio) els.audio.playbackRate = state.playbackSpeed;
        const speedBtn = document.getElementById('btn-speed');
        if (speedBtn) {
            speedBtn.textContent = state.playbackSpeed + 'x';
            if (state.playbackSpeed !== 1.0) speedBtn.style.color = 'var(--purple-primary)';
            else speedBtn.style.color = 'inherit';
        }
        showToast(`Tốc độ phát: ${state.playbackSpeed}x`);
    }

    function bindSearch() {
        const searchBox = document.getElementById('search-box');
        const searchBtn = document.getElementById('search-btn');

        if (!searchBox) return;

        // Function to perform search
        function performSearch() {
            const term = searchBox.value.trim();
            if (term.length < 2) {
                showToast('Vui lòng nhập ít nhất 2 ký tự');
                return;
            }

            // Navigate to search view and fetch results
            switchView('search-results');
            search(term);
        }

        // Trigger search on Enter key
        searchBox.addEventListener('keypress', (event) => {
            if (event.key === 'Enter') {
                event.preventDefault();
                performSearch();
            }
        });

        // Trigger search on search button click
        searchBtn?.addEventListener('click', (event) => {
            event.preventDefault();
            performSearch();
        });
    }

    function search(term) {
        if (!term) return;

        // Display search term
        const searchTermDisplay = document.getElementById('search-term-display');
        if (searchTermDisplay) {
            searchTermDisplay.textContent = `Đang tìm kiếm cho: "${term}"`;
        }

        fetch(`/search?term=${encodeURIComponent(term)}`)
            .then(res => res.json())
            .then(data => {
                if (searchTermDisplay) {
                    searchTermDisplay.textContent = `Kết quả tìm kiếm cho: "${term}"`;
                }
                renderSearchResults(data.data, term);
            })
            .catch(err => {
                console.error('Search error:', err);
                showToast('Có lỗi xảy ra khi tìm kiếm');
            });
    }

    function renderSearchResults(data, term) {
        console.log('Rendering search results:', data);

        // Get containers
        const songsSection = document.getElementById('songs-section');
        const artistsSection = document.getElementById('artists-section');
        const usersSection = document.getElementById('users-section');
        const emptyState = document.getElementById('search-empty-state');

        const songsResults = document.getElementById('songs-results');
        const artistsResults = document.getElementById('artists-results');
        const usersResults = document.getElementById('users-results');

        const songsCount = document.getElementById('songs-count');
        const artistsCount = document.getElementById('artists-count');
        const usersCount = document.getElementById('users-count');

        // Clear previous results
        songsResults.innerHTML = '';
        artistsResults.innerHTML = '';
        usersResults.innerHTML = '';

        // Check if we have any results
        const hasResults = data && (
            (data.songs && data.songs.length > 0) ||
            (data.artists && data.artists.length > 0) ||
            (data.users && data.users.length > 0)
        );

        // Show/hide empty state
        emptyState.classList.toggle('hidden', hasResults);

        // Render Songs
        if (data.songs && data.songs.length > 0) {
            songsSection.classList.remove('hidden');
            songsCount.textContent = `Bài hát (${data.songs.length})`;
            data.songs.forEach(song => {
                const cover = song.coverUrl || `https://picsum.photos/seed/${song.id}/200/200`;
                const songCard = document.createElement('div');
                songCard.className = 'song-card playlist-item';
                songCard.dataset.songId = song.id;
                songCard.dataset.title = song.title;
                songCard.dataset.artist = song.artist;
                songCard.dataset.cover = cover;
                songCard.dataset.audio = song.audioUrl;
                songCard.dataset.duration = song.duration;
                songCard.dataset.favorite = song.isFavorite;

                songCard.innerHTML = `
                    <img src="${cover}" style="width: 60px; height: 60px; border-radius: 8px; object-fit: cover; margin-right: 15px;">
                    <div style="flex: 1;">
                        <div style="font-weight: 500; margin-bottom: 4px;">${song.title}</div>
                        <div style="font-size: 13px; color: var(--text-secondary);">${song.artist}</div>
                    </div>
                    <div style="font-size: 13px; color: var(--text-secondary); margin-right: 15px;">${song.duration}</div>
                    <button class="circle-btn favorite-toggle ${song.isFavorite ? 'active' : ''}" title="Yêu thích">
                        <i class="fa-solid fa-heart"></i>
                    </button>
                `;
                songsResults.appendChild(songCard);
            });
        } else {
            songsSection.classList.add('hidden');
        }

        // Render Artists
        if (data.artists && data.artists.length > 0) {
            artistsSection.classList.remove('hidden');
            artistsCount.textContent = `Nghệ sĩ (${data.artists.length})`;
            data.artists.forEach(artist => {
                const avatar = artist.avatarUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(artist.name)}&size=200&background=8a2be2&color=fff`;
                const artistCard = document.createElement('div');
                artistCard.className = 'artist-card';
                artistCard.style.cssText = 'display: flex; align-items: center; padding: 15px; background: rgba(255,255,255,0.05); border-radius: 8px; cursor: pointer; transition: all 0.3s;';

                artistCard.innerHTML = `
                    <img src="${avatar}" style="width: 60px; height: 60px; border-radius: 50%; object-fit: cover; margin-right: 15px;">
                    <div>
                        <div style="font-weight: 500; margin-bottom: 4px;">${artist.name}</div>
                        ${artist.bio ? `<div style="font-size: 13px; color: var(--text-secondary);">${artist.bio}</div>` : ''}
                    </div>
                `;
                artistsResults.appendChild(artistCard);
            });
        } else {
            artistsSection.classList.add('hidden');
        }

        // Render Users
        if (data.users && data.users.length > 0) {
            usersSection.classList.remove('hidden');
            usersCount.textContent = `Người dùng (${data.users.length})`;
            data.users.forEach(user => {
                const avatar = user.avatarUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(user.displayName)}&size=200&background=random`;
                const userCard = document.createElement('div');
                userCard.className = 'user-card';
                userCard.style.cssText = 'display: flex; align-items: center; padding: 15px; background: rgba(255,255,255,0.05); border-radius: 8px; cursor: pointer; transition: all 0.3s;';

                userCard.innerHTML = `
                    <img src="${avatar}" style="width: 60px; height: 60px; border-radius: 50%; object-fit: cover; margin-right: 15px;">
                    <div>
                        <div style="font-weight: 500; margin-bottom: 4px;">${user.displayName}</div>
                        ${user.email ? `<div style="font-size: 13px; color: var(--text-secondary);">${user.email}</div>` : ''}
                    </div>
                `;
                usersResults.appendChild(userCard);
            });
        } else {
            usersSection.classList.add('hidden');
        }
    }

    function renderSongResult(song) {
        const div = document.createElement('div');
        div.className = 'playlist-item song-card';
        const cover = song.coverUrl || `https://picsum.photos/seed/search-${song.id}/200/200`;
        div.dataset.songId = song.id;
        div.dataset.title = song.title;
        div.dataset.artist = song.artist;
        div.dataset.cover = cover;
        div.dataset.audio = song.audioUrl ?? '';
        div.dataset.duration = song.duration;
        div.dataset.favorite = song.isFavorite;
        div.innerHTML = `
            <img src="${cover}" style="width:40px;height:40px;border-radius:4px;margin-right:10px;">
            <div style="flex:1;">
                <div style="font-weight:600;">${song.title}</div>
                <div style="font-size:12px;color:var(--text-secondary);">${song.artist}</div>
            </div>
        `;
        return div;
    }

    function renderArtistResult(artist) {
        const div = document.createElement('div');
        div.className = 'playlist-item';
        div.textContent = artist.name;
        return div;
    }

    function renderPlaylistResult(playlist) {
        const div = document.createElement('div');
        div.className = 'playlist-item';
        div.textContent = `${playlist.title} · ${playlist.subtitle}`;
        return div;
    }

    function renderAlbumResult(album) {
        const div = document.createElement('div');
        div.className = 'playlist-item';
        div.textContent = `${album.title} · ${album.artistName}`;
        return div;
    }

    function getAllSongCards() {
        // Get song cards from currently visible main view
        // Views use IDs like: home-view, library-view, profile-view, etc.
        const mainViews = [
            '#home-view',
            '#library-view',
            '#profile-view',
            '#all-songs-view',
            '#all-genres-view',
            '#playlist-view-container',
            '#genre-view-container',
            '#album-view'
        ];

        const cards = [];
        const seenSongIds = new Set(); // Track unique song IDs

        mainViews.forEach(selector => {
            const view = document.querySelector(selector);
            if (view && !view.classList.contains('hidden')) {
                const viewCards = view.querySelectorAll('.song-card');
                viewCards.forEach(card => {
                    const songId = card.dataset.songId;
                    // Only add if we haven't seen this song ID before
                    if (songId && !seenSongIds.has(songId)) {
                        seenSongIds.add(songId);
                        cards.push(card);
                    }
                });
            }
        });

        return Array.from(cards);
    }

    function playNext() {
        // Handle Loop One - replay current song
        if (state.loopMode === 'one' && state.currentSong) {
            els.audio.currentTime = 0;
            els.audio.play();
            return;
        }

        if (state.queue && state.queue.length > 0) {
            // Queue Mode
            let nextIndex = state.queueIndex + 1;

            // Handle end of queue based on loop mode
            if (nextIndex >= state.queue.length) {
                if (state.loopMode === 'all') {
                    nextIndex = 0; // Wrap to beginning
                } else {
                    // Loop off - stop playing
                    return;
                }
            }

            state.queueIndex = nextIndex;
            playSong(state.queue[nextIndex]);
            return;
        }

        // Fallback: DOM Mode
        const cards = getAllSongCards();
        if (!cards.length) return;
        if (!state.currentSong) {
            playSongFromCard(cards[0]);
            return;
        }
        const index = cards.findIndex(c => Number(c.dataset.songId) === state.currentSong.id);
        let nextIndex = index === -1 ? 0 : index + 1;

        // Handle end of cards based on loop mode
        if (nextIndex >= cards.length) {
            if (state.loopMode === 'all') {
                nextIndex = 0; // Wrap to beginning
            } else {
                // Loop off - stop playing
                return;
            }
        }

        playSongFromCard(cards[nextIndex]);
    }

    function playPrev() {
        if (state.queue && state.queue.length > 0) {
            // Queue Mode
            let prevIndex = state.queueIndex - 1;
            if (prevIndex < 0) prevIndex = state.queue.length - 1;
            state.queueIndex = prevIndex;
            playSong(state.queue[prevIndex]);
            return;
        }

        // Fallback: DOM Mode
        const cards = getAllSongCards();
        if (!cards.length) return;
        if (!state.currentSong) {
            playSongFromCard(cards[0]);
            return;
        }
        const index = cards.findIndex(c => Number(c.dataset.songId) === state.currentSong.id);
        const prevIndex = index <= 0 ? cards.length - 1 : index - 1;
        playSongFromCard(cards[prevIndex]);
    }

    // Fisher-Yates Shuffle Algorithm
    function shuffleArray(array) {
        const shuffled = [...array]; // Create a copy
        for (let i = shuffled.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]]; // Swap
        }
        return shuffled;
    }

    function toggleShuffle() {
        // If no queue exists, create one from visible song cards
        if (!state.queue || state.queue.length === 0) {
            const cards = getAllSongCards();
            if (!cards.length) {
                showToast('Không có bài hát để xáo trộn');
                return;
            }

            // Create queue from song cards
            state.queue = cards.map(card => ({
                id: Number(card.dataset.songId),
                title: card.dataset.title,
                artist: card.dataset.artist,
                cover: card.dataset.cover,
                audio: card.dataset.audio || '',
                durationLabel: card.dataset.duration,
                isFavorite: card.dataset.favorite === 'true' || card.dataset.favorite === 'True'
            }));

            // Set current index
            if (state.currentSong) {
                const currentIndex = state.queue.findIndex(s => s.id === state.currentSong.id);
                state.queueIndex = currentIndex !== -1 ? currentIndex : 0;
            } else {
                state.queueIndex = 0;
            }

            // Don't return here - continue to enable shuffle
        }

        if (state.isShuffled) {
            // Disable shuffle - restore original queue
            if (state.originalQueue) {

                // Find current song in original queue
                const currentSongId = state.currentSong?.id;
                state.queue = [...state.originalQueue];

                // Update index to match current song in original queue
                if (currentSongId) {
                    const newIndex = state.queue.findIndex(s => s.id === currentSongId);
                    if (newIndex !== -1) {
                        state.queueIndex = newIndex;
                    }
                }

                state.originalQueue = null;
                state.isShuffled = false;

                // Update UI
                const shuffleBtn = document.getElementById('btn-shuffle');
                if (shuffleBtn) shuffleBtn.classList.remove('active');

                showToast('Đã tắt phát ngẫu nhiên');
            }
        } else {
            // Enable shuffle
            // IMPORTANT: Save original BEFORE shuffling
            if (!state.originalQueue) {
                state.originalQueue = [...state.queue]; // Save original
            }
            const currentSongId = state.currentSong?.id;

            // Shuffle the queue - IMPORTANT: shuffle from originalQueue, not state.queue!
            state.queue = shuffleArray(state.originalQueue);

            // Update index to match current song in shuffled queue
            if (currentSongId) {
                const newIndex = state.queue.findIndex(s => s.id === currentSongId);
                if (newIndex !== -1) {
                    state.queueIndex = newIndex;
                }
            }

            state.isShuffled = true;

            // Update UI
            const shuffleBtn = document.getElementById('btn-shuffle');
            if (shuffleBtn) shuffleBtn.classList.add('active');

            showToast('Đã bật phát ngẫu nhiên');
        }
    }

    function toggleLoop() {
        // Cycle through: off -> all -> one -> off
        if (state.loopMode === 'off') {
            state.loopMode = 'all';
            showToast('Lặp lại: Tất cả');
        } else if (state.loopMode === 'all') {
            state.loopMode = 'one';
            showToast('Lặp lại: Một bài');
        } else {
            state.loopMode = 'off';
            showToast('Tắt lặp lại');
        }

        // Update UI
        const repeatBtn = document.getElementById('btn-repeat');
        if (repeatBtn) {
            if (state.loopMode === 'off') {
                repeatBtn.classList.remove('active');
                repeatBtn.innerHTML = '<i class="fa-solid fa-repeat"></i>';
            } else if (state.loopMode === 'all') {
                repeatBtn.classList.add('active');
                repeatBtn.innerHTML = '<i class="fa-solid fa-repeat"></i>';
            } else { // 'one'
                repeatBtn.classList.add('active');
                repeatBtn.innerHTML = '<i class="fa-solid fa-repeat"></i><span style="position: absolute; font-size: 10px; font-weight: bold; margin-left: -8px; margin-top: 8px;">1</span>';
            }
        }
    }

    function setVolume(value) {
        if (!els.audio) return;
        const v = Math.min(1, Math.max(0, value));
        els.audio.volume = v;
        if (els.volumeFill) els.volumeFill.style.width = `${v * 100}%`;
        if (els.volumeIcon) {
            if (v === 0) els.volumeIcon.innerHTML = '<i class="fa-solid fa-volume-xmark"></i>';
            else if (v < 0.5) els.volumeIcon.innerHTML = '<i class="fa-solid fa-volume-low"></i>';
            else els.volumeIcon.innerHTML = '<i class="fa-solid fa-volume-high"></i>';
        }
    }

    // FULL SCREEN PLAYER LOGIC
    // FULL SCREEN PLAYER LOGIC
    function toggleFullPlayer(open) {
        const fp = document.getElementById('full-player');
        if (!fp) return;

        if (open) {
            fp.classList.remove('hidden');
            // Small delay to allow display:block to apply before transition
            requestAnimationFrame(() => fp.classList.add('active'));
            updateFullPlayerUI();
        } else {
            fp.classList.remove('active');
            // Wait for transition to finish
            setTimeout(() => fp.classList.add('hidden'), 500);
        }
    }

    function updateFullPlayerUI() {
        if (!state.currentSong) return;

        // Sync Info
        document.getElementById('fp-img').src = state.currentSong.cover;
        document.getElementById('fp-bg').style.backgroundImage = `url('${state.currentSong.cover}')`;
        document.getElementById('fp-title').textContent = state.currentSong.title;
        document.getElementById('fp-artist').textContent = state.currentSong.artist;

        // Sync Favorite
        const fpFav = document.getElementById('fp-favorite');
        fpFav.classList.toggle('active', state.currentSong.isFavorite);
        fpFav.innerHTML = state.currentSong.isFavorite ? '<i class="fa-solid fa-heart"></i>' : '<i class="fa-regular fa-heart"></i>';

        // Sync Play Icon
        const fpPlayIcon = document.getElementById('fp-play-icon');
        if (state.isPlaying) {
            fpPlayIcon.classList.remove('fa-circle-play');
            fpPlayIcon.classList.add('fa-circle-pause');
            document.querySelector('.fp-cd-wrapper')?.classList.add('playing');
        } else {
            fpPlayIcon.classList.remove('fa-circle-pause');
            fpPlayIcon.classList.add('fa-circle-play');
            document.querySelector('.fp-cd-wrapper')?.classList.remove('playing');
        }

        // Sync Rating
        updateStarRating(state.currentSong.userRating || 0);

        // Load lyrics for FP
        loadLyricsToFP(state.currentSong.id);
    }

    function loadLyricsToFP(songId) {
        const container = document.getElementById('fp-lyrics');
        if (!container) return;

        // Re-use logic but render to FP container
        fetch(`/lyrics/${songId}`)
            .then(res => res.json())
            .then(data => {
                if (!data.success) return;
                container.innerHTML = '';
                data.data.lyrics.forEach((line, idx) => {
                    const p = document.createElement('p');
                    p.textContent = line;
                    if (idx === 0) p.classList.add('active'); // Dummy active first line
                    container.appendChild(p);
                });
            });
    }

    // Sync Progress to FP
    function updateFPProgress() {
        if (!els.audio.duration) return;
        const percent = (els.audio.currentTime / els.audio.duration) * 100;
        const fpFill = document.getElementById('fp-progress-fill');
        if (fpFill) fpFill.style.width = `${percent}%`;

        document.getElementById('fp-current').textContent = formatTime(els.audio.currentTime);
        document.getElementById('fp-duration').textContent = formatTime(els.audio.duration);
    }

    let lastVolume = 0.7;

    // STAR RATING FUNCTIONS
    function updateStarRating(rating) {
        const stars = document.querySelectorAll('#fp-star-rating i');
        stars.forEach((star, index) => {
            if (index < Math.floor(rating)) {
                star.classList.remove('fa-regular');
                star.classList.add('fa-solid');
            } else {
                star.classList.remove('fa-solid');
                star.classList.add('fa-regular');
            }
        });
    }

    function handleStarClick(event) {
        if (!state.currentSong) return;
        const rating = parseInt(event.target.dataset.rating);
        setUserRating(state.currentSong.id, rating);
    }

    function handleStarHover(event) {
        const rating = parseInt(event.target.dataset.rating);
        const stars = document.querySelectorAll('#fp-star-rating i');
        stars.forEach((star, index) => {
            if (index < rating) {
                star.classList.add('hover');
            } else {
                star.classList.remove('hover');
            }
        });
    }

    function resetStarHover() {
        const stars = document.querySelectorAll('#fp-star-rating i');
        stars.forEach(star => star.classList.remove('hover'));
    }

    function setUserRating(songId, rating) {
        fetch(`/songs/${songId}/rating`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ rating: rating })
        })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    // Update current song rating in state
                    if (state.currentSong && state.currentSong.id === songId) {
                        state.currentSong.userRating = rating;
                    }
                    updateStarRating(rating);
                    showToast(`Đã đánh giá ${rating} sao!`);
                } else {
                    showToast(data.message || 'Không thể cập nhật đánh giá');
                }
            })
            .catch(err => {
                console.error('Rating error:', err);
                showToast('Vui lòng đăng nhập để đánh giá');
            });
    }

    // VIEW NAVIGATION
    window.switchView = function (viewName) {
        // Hide all views
        document.getElementById('home-view')?.classList.add('hidden');
        document.getElementById('library-view')?.classList.add('hidden');
        document.getElementById('profile-view')?.classList.add('hidden');
        document.getElementById('all-songs-view')?.classList.add('hidden');
        document.getElementById('all-genres-view')?.classList.add('hidden');
        document.getElementById('playlist-view-container')?.classList.add('hidden');
        document.getElementById('genre-view-container')?.classList.add('hidden');
        document.getElementById('search-results-view')?.classList.add('hidden');
        document.getElementById('upload-view')?.classList.add('hidden');

        // Hide dynamic views
        const albumView = document.getElementById('album-view');
        if (albumView) albumView.remove(); // Remove dynamic album view to keep DOM clean or hide it

        // Show selected
        const selected = document.getElementById(`${viewName}-view`);
        if (selected) selected.classList.remove('hidden');

        // Update Nav
        document.querySelectorAll('.nav-item').forEach(item => item.classList.remove('active'));
        const navItem = document.querySelector(`.nav-item[data-view="${viewName}"]`);
        if (navItem) navItem.classList.add('active');

        // Scroll to top
        document.querySelector('.main-view')?.scrollTo(0, 0);
    };

    window.loadAlbum = function (albumId) {
        const mainView = document.querySelector('.main-view');
        if (!mainView) return;

        fetch(`/album/${albumId}`)
            .then(res => {
                if (!res.ok) throw new Error('Không thể tải album');
                return res.text();
            })
            .then(html => {
                // Hide current view
                document.getElementById('home-view')?.classList.add('hidden');
                document.getElementById('library-view')?.classList.add('hidden');
                document.getElementById('profile-view')?.classList.add('hidden');
                document.getElementById('all-songs-view')?.classList.add('hidden');
                document.getElementById('search-results-view')?.classList.add('hidden');

                // Remove existing album view if any
                const existing = document.getElementById('album-view');
                if (existing) existing.remove();

                // Append new album view
                mainView.insertAdjacentHTML('beforeend', html);

                // Parse Album Songs for Queue
                const albumView = document.getElementById('album-view');
                if (albumView && albumView.dataset.albumSongs) {
                    try {
                        state.contextQueue = JSON.parse(albumView.dataset.albumSongs);
                    } catch (e) { console.error('Error parsing album songs', e); }
                }

                // Re-bind events if needed (delegation handles clicks)
            })
            .catch(err => {
                showToast(err.message);
            });
    };

    function renderLibraryChart() {
        const container = document.getElementById('library-chart-container');
        if (!container || !window.musicModel || !window.musicModel.chart) return;
        container.innerHTML = '';
        window.musicModel.chart.forEach(item => {
            const cover = item.coverUrl || `https://picsum.photos/seed/chart-${item.songId}/300/300`;
            const row = document.createElement('div');
            row.className = 'chart-item song-card';
            row.dataset.songId = item.songId;
            row.dataset.title = item.title;
            row.dataset.artist = item.artist;
            row.dataset.cover = cover;
            row.dataset.audio = (window.musicModel.newReleases || []).find(s => s.id === item.songId)?.audioUrl || '';
            row.dataset.duration = '00:00';
            row.innerHTML = `
                <div class="chart-number">${item.rank}</div>
                <img src="${cover}" class="current-song-img" alt="${item.title}">
                <div class="song-info">
                    <h4>${item.title}</h4>
                    <p>${item.artist}</p>
                </div>
                <div class="chart-percent">${Math.round(item.percentage * 100)}%</div>
            `;
            container.appendChild(row);
        });
    }

    function syncModel() {
        if (window.musicModel) state.playlists = window.musicModel.personalPlaylists ?? [];
        if (typeof window.isAuthenticated !== 'undefined') state.isAuthenticated = !!window.isAuthenticated;
    }

    // Global Playlist Click Delegation
    function handlePlaylistClicks() {
        document.addEventListener('click', event => {
            const card = event.target.closest('.card[data-playlist-id]');
            if (card && !event.target.closest('.play-btn-circle')) { // Optional: if you have a play button on card that does something else
                const id = card.dataset.playlistId;
                if (id) loadPlaylist(id);
            }
        });
    }

    // SORT FUNCTIONALITY FOR ALL SONGS VIEW
    function initSortControls() {
        const sortSelect = document.getElementById('sort-select');
        const resetBtn = document.getElementById('reset-sort');
        const songsList = document.querySelector('.all-songs-list');

        if (!sortSelect || !songsList) return;

        // Sort function
        function sortSongs(sortBy, order) {
            const songsArray = Array.from(songsList.querySelectorAll('.song-card'));

            songsArray.sort((a, b) => {
                let valueA, valueB;

                switch (sortBy) {
                    case 'viewCount':
                        valueA = parseInt(a.dataset.viewCount) || 0;
                        valueB = parseInt(b.dataset.viewCount) || 0;
                        break;
                    case 'rating':
                        valueA = parseFloat(a.dataset.averageRating) || 0;
                        valueB = parseFloat(b.dataset.averageRating) || 0;
                        break;
                    case 'releaseDate':
                        valueA = new Date(a.dataset.releaseDate);
                        valueB = new Date(b.dataset.releaseDate);
                        break;
                    case 'title':
                        valueA = a.dataset.title.toLowerCase();
                        valueB = b.dataset.title.toLowerCase();
                        if (order === 'asc') {
                            return valueA.localeCompare(valueB);
                        } else {
                            return valueB.localeCompare(valueA);
                        }
                    default:
                        return 0;
                }

                return order === 'asc' ? valueA - valueB : valueB - valueA;
            });

            // Clear and re-append in sorted order
            songsList.innerHTML = '';
            songsArray.forEach(song => songsList.appendChild(song));

            // Show toast notification
            const sortLabels = {
                'viewCount': 'Lượt xem',
                'rating': 'Điểm đánh giá',
                'releaseDate': 'Ngày phát hành',
                'title': 'Tên bài hát'
            };
            const orderLabel = order === 'asc' ? 'tăng dần' : 'giảm dần';
            showToast(`Đã sắp xếp theo ${sortLabels[sortBy]} (${orderLabel})`);
        }

        // Event listener for sort select
        sortSelect.addEventListener('change', (e) => {
            const [sortBy, order] = e.target.value.split('-');
            sortSongs(sortBy, order);
        });

        // Reset button
        resetBtn?.addEventListener('click', () => {
            sortSelect.value = 'releaseDate-desc';
            sortSongs('releaseDate', 'desc');
        });
    }

    // UPLOAD LOGIC
    function bindUploadForm() {
        const form = document.getElementById('upload-form');
        if (!form) return;

        form.addEventListener('submit', function (e) {
            e.preventDefault();

            if (!state.isAuthenticated) {
                toggleAuthModal(true);
                return;
            }

            const submitBtn = form.querySelector('button[type="submit"]');
            const originalText = submitBtn.innerHTML;
            const progress = document.getElementById('upload-progress');

            submitBtn.disabled = true;
            submitBtn.innerHTML = '<i class="fa-solid fa-circle-notch fa-spin"></i> Đang xử lý...';
            if (progress) progress.classList.remove('hidden');

            const formData = new FormData(form);

            fetch('/Upload', {
                method: 'POST',
                body: formData
            })
                .then(res => res.json())
                .then(data => {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalText;
                    if (progress) progress.classList.add('hidden');

                    if (data.success) {
                        showToast(data.message);
                        form.reset();
                        // Reset file boxes
                        document.querySelectorAll('.file-upload-box').forEach(box => {
                            box.classList.remove('has-file');
                            box.querySelector('span').textContent = box.id === 'audio-upload-box' ? 'Chọn file nhạc' : 'Chọn ảnh bìa';
                        });

                        // Redirect to Home or whatever logic
                        switchView('home');
                        // Optionally refresh "All Songs" or "Profile"
                        window.location.reload(); // Simplest way to refresh lists for now
                    } else {
                        showToast(data.message || 'Có lỗi xảy ra khi upload');
                        console.error(data.errors);
                    }
                })
                .catch(err => {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = originalText;
                    if (progress) progress.classList.add('hidden');
                    showToast('Lỗi kết nối server');
                    console.error(err);
                });
        });
    }

    function bindProfileTabs() {
        const tabItems = document.querySelectorAll('.profile-nav-tabs .tab-item');
        const tabContents = document.querySelectorAll('.profile-tab-content');

        if (!tabItems.length || !tabContents.length) return;

        tabItems.forEach((tab, index) => {
            tab.addEventListener('click', () => {
                // Remove active class from all tabs and contents
                tabItems.forEach(t => t.classList.remove('active'));
                tabContents.forEach(c => c.classList.remove('active'));

                // Add active class to clicked tab and corresponding content
                tab.classList.add('active');
                const targetContent = document.querySelector(`.profile-tab-content[data-tab="${index}"]`);
                if (targetContent) {
                    targetContent.classList.add('active');
                }
            });
        });

        // Bind create playlist buttons in tabs
        document.getElementById('profile-open-playlist-tab')?.addEventListener('click', event => {
            event.preventDefault();
            openPlaylistModal(null);
        });
        document.getElementById('create-playlist-card-tab')?.addEventListener('click', () => openPlaylistModal(null));
    }

    function init() {
        if (!els.audio) return;
        syncModel();
        setVolume(0.7);
        bindAuthForm();
        bindPlayerControls();
        handleSongCardClicks();
        handlePlaylistClicks(); // Bind new listener
        bindPlaylistModal();
        bindSearch();
        bindProfileTabs(); // Bind profile tab switching
        renderLibraryChart();
        initSortControls(); // Initialize sort controls
        document.getElementById('lyrics-view')?.addEventListener('click', () => { });
        window.toggleLyrics = toggleLyrics;
        window.toggleAuthModal = toggleAuthModal;
        window.toggleEditProfileModal = toggleEditProfileModal;
        window.closePlaylistModal = closePlaylistModal;
        window.switchView = switchView;

        bindUploadForm(); // NEW

        // BIND FULL PLAYER EVENTS
        document.getElementById('btn-full-player')?.addEventListener('click', (e) => {
            e.preventDefault();
            toggleFullPlayer(true);
        });
        document.getElementById('fp-close-btn')?.addEventListener('click', () => toggleFullPlayer(false));

        // Bind FP Controls (Proxy to main controls)
        document.getElementById('fp-play')?.addEventListener('click', togglePlay);
        document.getElementById('fp-next')?.addEventListener('click', playNext);
        document.getElementById('fp-prev')?.addEventListener('click', playPrev);
        document.getElementById('fp-favorite')?.addEventListener('click', () => state.currentSong && toggleFavorite(state.currentSong.id, null));
        document.getElementById('fp-progress')?.addEventListener('click', seek); // Reuse seek? Need check target

        // Bind Star Rating Events
        document.querySelectorAll('#fp-star-rating i').forEach(star => {
            star.addEventListener('click', handleStarClick);
            star.addEventListener('mouseenter', handleStarHover);
        });
        document.getElementById('fp-star-rating')?.addEventListener('mouseleave', resetStarHover);

        els.audio.addEventListener('timeupdate', updateFPProgress);

        document.getElementById('auth-modal')?.addEventListener('click', event => {
            if (event.target === els.authModal) toggleAuthModal(false);
        });
        document.getElementById('edit-profile-modal')?.addEventListener('click', event => {
            if (event.target === document.getElementById('edit-profile-modal')) toggleEditProfileModal(false);
        });
        bindEditProfile();
        document.addEventListener('keydown', event => {
            if (event.target.tagName === 'INPUT' || event.target.tagName === 'TEXTAREA') return;
            if (event.code === 'ArrowRight') seekBy(5);
            else if (event.code === 'ArrowLeft') seekBy(-5);
        });

        // Hook into update functions
        const originalUpdateUI = updatePlayerUI;
        // Override updatePlayerUI to also update Full Player if active
        // Better: updateFullPlayerUI() called inside togglePlay etc or create observer
        // Hack: Call updateFullPlayerUI() periodically or inside playback events
        els.audio.addEventListener('play', () => updateFullPlayerUI());
        els.audio.addEventListener('pause', () => updateFullPlayerUI());
        // We'll update FP UI when updatePlayerUI is called by observing state changes or just calling it
    }

    // PLAYLIST FEATURES
    window.loadPlaylist = function (id) {
        if (!state.isAuthenticated) {
            toggleAuthModal(true);
            return;
        }
        fetch(`/playlists/${id}`)
            .then(res => {
                if (!res.ok) throw new Error('Cannot load playlist');
                return res.text();
            })
            .then(html => {
                // Hide all main static views
                document.getElementById('home-view')?.classList.add('hidden');
                document.getElementById('library-view')?.classList.add('hidden');
                document.getElementById('profile-view')?.classList.add('hidden');
                document.getElementById('all-songs-view')?.classList.add('hidden');
                document.getElementById('search-results-view')?.classList.add('hidden');

                // Hide Album View if exists
                document.getElementById('album-view')?.remove();

                // Load content into container
                const container = document.getElementById('playlist-view-container');
                if (container) {
                    container.innerHTML = html;
                    container.classList.remove('hidden');

                    // Parse songs for Queue
                    const view = container.querySelector('[data-playlist-songs]');
                    if (view) {
                        const data = view.dataset.playlistSongs;
                        if (data) {
                            try {
                                state.contextQueue = JSON.parse(data);
                            } catch (e) { console.error(e); }
                        }
                    }
                }
            })
            .catch(err => showToast(err.message));
    };

    window.playPlaylistAll = function () {
        if (state.contextQueue && state.contextQueue.length > 0) {
            playQueue(state.contextQueue, 0);
        }
    };

    window.removeSongFromPlaylist = function (playlistId, songId, btn) {
        if (!confirm('Bạn có chắc muốn xóa bài hát này khỏi playlist?')) return;

        fetch(`/playlists/${playlistId}/songs/${songId}`, { method: 'DELETE' })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    const row = btn.closest('.song-row');
                    if (row) row.remove();
                    showToast('Đã xóa bài hát khỏi playlist');

                    // Update contextQueue
                    state.contextQueue = state.contextQueue.filter(s => s.id !== songId);

                    // Update Global Playlist State (Song Count)
                    const pl = state.playlists.find(p => p.id === playlistId);
                    if (pl) {
                        const match = (pl.subtitle || '0').match(/\d+/);
                        let count = match ? parseInt(match[0]) : 0;
                        if (count > 0) count--;
                        pl.subtitle = `${count} bài hát`;

                        // Update Profile Grid Card
                        const profileCard = document.querySelector(`#profile-playlists-grid .card[data-playlist-id="${playlistId}"] .card-subtitle`);
                        if (profileCard) profileCard.textContent = pl.subtitle;
                    }
                }
            });
    };

    // Edit Playlist Modal Logic
    window.openEditPlaylistModal = function (id, currentName, currentCover) {
        // Create modal if not exists
        let modal = document.getElementById('edit-playlist-modal');
        if (!modal) {
            modal = document.createElement('div');
            modal.id = 'edit-playlist-modal';
            modal.className = 'modal-overlay';
            modal.innerHTML = `
            <div class="modal-content" style="width:400px;">
                <h3 style="margin-bottom:20px;">Sửa Playlist</h3>
                <form id="edit-playlist-form">
                    <input type="hidden" name="id">
                    <div class="form-group mb-3">
                        <label>Tên Playlist</label>
                        <input type="text" name="name" class="auth-input" required>
                    </div>
                    <div class="form-group mb-3">
                        <label>Ảnh bìa (File)</label>
                        <input type="file" name="coverFile" class="auth-input" accept="image/*">
                    </div>
                    <div class="form-group mb-3">
                        <label>Hoặc Link Ảnh (URL)</label>
                        <input type="text" name="coverUrlInput" class="auth-input" placeholder="https://...">
                    </div>
                    <div style="display:flex; justify-content:flex-end; gap:10px; margin-top:20px;">
                        <button type="button" class="btn btn-secondary" onclick="document.getElementById('edit-playlist-modal').classList.remove('visible')">Hủy</button>
                        <button type="submit" class="btn btn-primary">Lưu</button>
                    </div>
                </form>
            </div>
        `;
            document.body.appendChild(modal);

            modal.querySelector('#edit-playlist-form').addEventListener('submit', handleEditPlaylistSubmit);
            modal.addEventListener('click', e => {
                if (e.target === modal) modal.classList.remove('visible');
            });
        }

        const form = modal.querySelector('form');
        form.id.value = id;
        form.name.value = currentName;
        form.coverUrlInput.value = '';
        form.coverFile.value = '';

        modal.classList.add('visible');
    };

    function handleEditPlaylistSubmit(e) {
        e.preventDefault();
        const form = e.target;
        const formData = new FormData(form);
        const id = form.id.value;

        fetch(`/playlists/${id}/update`, {
            method: 'POST',
            body: formData
        })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    showToast('Cập nhật playlist thành công');
                    document.getElementById('edit-playlist-modal').classList.remove('visible');

                    // Update UI
                    document.getElementById('playlist-detail-title').textContent = form.name.value;
                    if (data.coverUrl) {
                        document.getElementById('playlist-detail-cover').src = data.coverUrl;
                    }

                    // Should also update sidebar list eventually
                    // Trigger a reload of sidebar?
                    // For now UI update in detail view is enough.
                } else {
                    showToast('Cập nhật thất bại');
                }
            });
    }

    // GENRE LOGIC
    window.loadGenre = function (id) {
        if (!state.isAuthenticated) {
            toggleAuthModal(true);
            return;
        }
        fetch(`/genre/${id}`)
            .then(res => {
                if (!res.ok) throw new Error('Cannot load genre');
                return res.text();
            })
            .then(html => {
                switchView('genre-placeholder'); // Helper to hide others
                // Hide specific containers manually if switchView doesn't cover dynamic "genre" name
                // Actually switchView takes a name and shows {name}-view.
                // But we have #genre-view-container. So we can't use switchView directly for this container unless we rename it or add logic.
                // Let's just do manual hiding like loadPlaylist.
                document.getElementById('home-view')?.classList.add('hidden');
                document.getElementById('library-view')?.classList.add('hidden');
                document.getElementById('profile-view')?.classList.add('hidden');
                document.getElementById('all-songs-view')?.classList.add('hidden');
                document.getElementById('all-genres-view')?.classList.add('hidden');
                document.getElementById('playlist-view-container')?.classList.add('hidden');
                document.getElementById('search-results-view')?.classList.add('hidden');
                document.getElementById('album-view')?.remove();

                const container = document.getElementById('genre-view-container');
                if (container) {
                    container.innerHTML = html;
                    container.classList.remove('hidden');

                    const view = container.querySelector('[data-genre-songs]');
                    if (view) {
                        try {
                            state.contextQueue = JSON.parse(view.dataset.genreSongs);
                        } catch (e) { console.error(e); }
                    }
                }
            })
            .catch(err => showToast(err.message));
    };

    window.playGenreAll = function () {
        if (state.contextQueue && state.contextQueue.length > 0) {
            playQueue(state.contextQueue, 0);
        }
    };

    document.addEventListener('DOMContentLoaded', init);
})();
