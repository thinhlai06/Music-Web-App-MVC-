// =============================================
// USER ALBUM FUNCTIONALITY
// This code should be appended to site.js
// =============================================

// Global state for user albums
let currentUserAlbums = [];
let currentAddSongAlbumId = null;

// Helper function to update song count display
function updateAlbumSongCount(change) {
    const metaSpans = document.querySelectorAll('.album-meta span');
    for (let span of metaSpans) {
        if (span.textContent.includes('bài hát')) {
            const match = span.textContent.match(/(\d+)/);
            if (match) {
                let count = parseInt(match[1]) + change;
                if (count < 0) count = 0;
                span.textContent = `${count} bài hát`;
            }
            break;
        }
    }
}

// Helper: Safe safeShowToast function (uses existing or creates fallback)
function safeShowToast(message) {
    // Try to use existing toast element
    const toastEl = document.getElementById('toast');
    if (toastEl) {
        toastEl.textContent = message;
        toastEl.classList.add('show');
        setTimeout(() => toastEl.classList.remove('show'), 3000);
    } else {
        // Fallback: use alert
        console.log('Toast:', message);
    }
}

// Helper: Safe state access (creates local state if global not available)
const localState = {
    contextQueue: []
};

// Load user albums
window.loadUserAlbums = function () {
    const grid = document.getElementById('user-albums-grid');
    if (!grid) return;

    grid.innerHTML = '<div style="text-align: center; padding: 50px;"><i class="fa-solid fa-spinner fa-spin" style="font-size: 32px;"></i><p style="margin-top: 15px;">Đang tải albums...</p></div>';

    fetch('/useralbum/myalbums')
        .then(res => res.json())
        .then(data => {
            if (!data.success) throw new Error('Failed to load');
            currentUserAlbums = data.data;
            renderUserAlbums(data.data);
        })
        .catch(err => {
            grid.innerHTML = '<div style="text-align: center; padding: 50px; color: #ff7b7b;"><p>Không thể tải albums</p></div>';
        });
};

function renderUserAlbums(albums) {
    const grid = document.getElementById('user-albums-grid');
    if (!grid) return;

    if (!albums || albums.length === 0) {
        grid.innerHTML = `
            <div style="text-align: center; padding: 50px; color: var(--text-secondary);">
                <i class="fa-solid fa-record-vinyl" style="font-size: 64px; opacity: 0.3;"></i>
                <p style="margin-top: 20px; font-size: 18px;">Bạn chưa có album nào</p>
                <p style="margin-top: 10px;">Tạo album để quản lý bài hát của bạn</p>
            </div>
        `;
        return;
    }

    grid.innerHTML = '';
    albums.forEach(album => {
        const card = document.createElement('div');
        card.className = 'card';
        card.onclick = () => loadUserAlbum(album.id);
        card.innerHTML = `
            <div class="card-img-wrapper">
                <img src="${album.coverUrl}" class="card-img" alt="${album.name}">
                <div class="card-overlay">
                    <div class="play-btn-circle"><i class="fa-solid fa-play"></i></div>
                </div>
                <span class="album-badge ${album.isPublic ? 'public' : 'private'}">
                    <i class="fa-solid fa-${album.isPublic ? 'globe' : 'lock'}"></i>
                    ${album.isPublic ? 'Public' : 'Private'}
                </span>
            </div>
            <div class="card-title">${album.name}</div>
            <div class="card-subtitle">${album.songCount} bài hát</div>
        `;
        grid.appendChild(card);
    });
}

// Load user album detail
window.loadUserAlbum = function (albumId) {
    const mainView = document.querySelector('.main-view');
    if (!mainView) return;

    // Add timestamp to prevent caching
    fetch(`/useralbum/${albumId}?t=${new Date().getTime()}`)
        .then(res => {
            if (!res.ok) throw new Error('Không thể tải album');
            return res.text();
        })
        .then(html => {
            // Remove existing DETAIL view only
            const existing = document.getElementById('user-album-detail-view');
            if (existing) existing.remove();

            // Insert new content
            mainView.insertAdjacentHTML('beforeend', html);

            // Ensure other views are hidden (except sidebar/player)
            // We use a custom logic here instead of switchView to avoid flickering
            document.querySelectorAll('.app-container > .main-view > div').forEach(el => {
                if (el.id !== 'user-album-detail-view' && !el.classList.contains('hidden')) {
                    el.classList.add('hidden');
                }
            });

            // Show the new view
            const newView = document.getElementById('user-album-detail-view');
            if (newView) newView.classList.remove('hidden');

            // Parse songs for queue
            const albumView = document.getElementById('user-album-detail-view');
            if (albumView && albumView.dataset.albumSongs) {
                try {
                    localState.contextQueue = JSON.parse(albumView.dataset.albumSongs);
                } catch (e) {
                    console.error('Error parsing album songs', e);
                }
            }
        })
        .catch(err => {
            safeShowToast(err.message);
        });
};

// Play user album functions
window.playUserAlbumAll = function () {
    if (localState.contextQueue && localState.contextQueue.length > 0) {
        playQueue(localState.contextQueue, 0);
    }
};

window.playUserAlbumSong = function (index) {
    if (localState.contextQueue && localState.contextQueue.length > 0) {
        playQueue(localState.contextQueue, index);
    }
};

// Modal functions
window.openCreateAlbumModal = function () {
    // Check if user is authenticated
    if (!window.isAuthenticated) {
        safeShowToast('Vui lòng đăng nhập để tạo album');
        toggleAuthModal();
        return;
    }

    const modal = document.getElementById('create-album-modal');
    if (!modal) {
        console.error('Create album modal not found');
        return;
    }

    modal.classList.add('active');
    const form = document.getElementById('create-album-form');
    if (form) {
        form.reset();
    }
};

window.closeCreateAlbumModal = function () {
    document.getElementById('create-album-modal')?.classList.remove('active');
};

window.openEditUserAlbumModal = function (id, name, coverUrl, isPublic, description) {
    const modal = document.getElementById('edit-album-modal');
    if (!modal) return;

    const form = document.getElementById('edit-album-form');
    form.querySelector('[name="albumId"]').value = id;
    form.querySelector('[name="name"]').value = name;
    form.querySelector('[name="description"]').value = description || '';
    form.querySelector('[name="isPublic"]').checked = isPublic;

    modal.classList.add('active');
};

window.closeEditAlbumModal = function () {
    document.getElementById('edit-album-modal')?.classList.remove('active');
};

window.openAddSongToAlbumModal = function (albumId) {
    currentAddSongAlbumId = albumId;
    const modal = document.getElementById('add-song-album-modal');
    if (!modal) return;

    // Reset tabs
    document.querySelectorAll('#add-song-album-modal .auth-tab').forEach(tab => {
        tab.classList.remove('active');
    });
    document.querySelector('#add-song-album-modal .auth-tab[data-mode="existing"]')?.classList.add('active');

    document.getElementById('existing-songs-tab')?.classList.remove('hidden');
    document.getElementById('upload-song-tab')?.classList.add('hidden');

    // Load user's songs
    loadUserSongsForAlbum();

    modal.classList.add('active');
};

window.closeAddSongToAlbumModal = function () {
    document.getElementById('add-song-album-modal')?.classList.remove('active');
    currentAddSongAlbumId = null;
};

function loadUserSongsForAlbum() {
    const container = document.getElementById('user-songs-list');
    if (!container) return;

    container.innerHTML = '<div style="text-align: center; padding: 20px;"><i class="fa-solid fa-spinner fa-spin"></i></div>';

    // Get uploaded songs from model
    const uploadedSongs = window.musicModel?.uploadedSongs || [];

    if (uploadedSongs.length === 0) {
        container.innerHTML = '<div style="text-align: center; padding: 20px; color: var(--text-secondary);">Bạn chưa upload bài hát nào</div>';
        return;
    }

    container.innerHTML = '';
    uploadedSongs.forEach(song => {
        const item = document.createElement('div');
        item.className = 'song-item';
        item.style.cssText = 'display: flex; align-items: center; padding: 10px; border-radius: 4px; cursor: pointer; transition: background 0.2s;';
        item.innerHTML = `
            <img src="${song.coverUrl || 'https://picsum.photos/300/300'}" style="width: 40px; height: 40px; border-radius: 4px; margin-right: 12px;">
            <div style="flex: 1;">
                <div style="font-weight: 500;">${song.title}</div>
                <div style="font-size: 13px; color: var(--text-secondary);">${song.artist}</div>
            </div>
            <button class="pill-btn" style="padding: 6px 15px; background: var(--purple-primary);" onclick="addExistingSongToAlbum(${song.id})">Thêm</button>
        `;
        container.appendChild(item);
    });
}

window.addExistingSongToAlbum = function (songId) {
    if (!currentAddSongAlbumId) return;

    // Find song details first
    const song = window.musicModel?.uploadedSongs?.find(s => s.id === songId);
    if (!song) {
        console.error('Song details not found for ID', songId);
    }

    fetch(`/useralbum/${currentAddSongAlbumId}/addsong`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ songId: songId })
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                // Close modal first
                const modal = document.getElementById('add-song-album-modal');
                if (modal) modal.classList.remove('active');

                safeShowToast(data.message);

                // Navigate to albums list view
                switchView('user-albums');
            } else {
                safeShowToast(data.message || 'Không thể thêm bài hát');
            }
        })
        .catch(err => safeShowToast('Lỗi kết nối'));
};

window.removeUserAlbumSong = function (albumId, songId, songTitle) {
    if (!confirm(`Xóa "${songTitle}" khỏi album?\n\nBạn muốn xóa file nhạc không?`)) return;

    const deleteFile = confirm('Xóa cả file nhạc trên Cloudflare? (Không thể hoàn tác)');

    fetch(`/useralbum/${albumId}/removesong/${songId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ deleteFile: deleteFile })
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                safeShowToast(data.message);

                // Navigate to albums list view
                switchView('user-albums');
            } else {
                safeShowToast(data.message);
            }
        })
        .catch(err => safeShowToast('Lỗi kết nối'));
};

window.deleteUserAlbumConfirm = function () {
    const form = document.getElementById('edit-album-form');
    const albumId = form.querySelector('[name="albumId"]').value;
    const albumName = form.querySelector('[name="name"]').value;

    if (!confirm(`Xác nhận xóa album "${albumName}"?\n\nBài hát trong album sẽ không bị xóa.`)) return;

    fetch(`/useralbum/${albumId}/delete`, {
        method: 'POST'
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                safeShowToast(data.message);
                closeEditAlbumModal();
                switchView('user-albums');
            } else {
                safeShowToast(data.message);
            }
        })
        .catch(err => safeShowToast('Lỗi kết nối'));
};

// Form submissions
document.addEventListener('DOMContentLoaded', function () {
    // Create album form
    const createForm = document.getElementById('create-album-form');
    if (createForm) {
        createForm.addEventListener('submit', function (e) {
            e.preventDefault();

            const formData = new FormData(createForm);

            fetch('/useralbum/create', {
                method: 'POST',
                body: formData
            })
                .then(res => res.json())
                .then(data => {
                    console.log('Create response:', data);
                    if (data.success) {
                        // 1. Close Modal immediately
                        try {
                            const modal = document.getElementById('create-album-modal');
                            if (modal) modal.classList.remove('active');
                        } catch (e) { console.error(e); }

                        // 2. Show success message
                        try { safeShowToast(data.message); } catch (e) { console.error(e); }

                        // 3. Refresh list
                        try {
                            switchView('user-albums');
                            setTimeout(() => loadUserAlbums(), 50);
                        } catch (e) { console.error(e); }
                    } else {
                        safeShowToast(data.message || 'Không thể tạo album');
                    }
                })
                .catch(err => {
                    console.error(err);
                    safeShowToast('Lỗi kết nối');
                });
        });
    }

    // Edit album form
    const editForm = document.getElementById('edit-album-form');
    if (editForm) {
        editForm.addEventListener('submit', function (e) {
            e.preventDefault();

            const albumId = editForm.querySelector('[name="albumId"]').value;
            const formData = new FormData(editForm);

            // Ensure isPublic is set correctly (checkbox sends nothing when unchecked)
            const isPublicCheckbox = editForm.querySelector('[name="isPublic"]');
            formData.set('isPublic', isPublicCheckbox.checked ? 'true' : 'false');

            fetch(`/useralbum/${albumId}/update`, {
                method: 'POST',
                body: formData
            })
                .then(res => res.json())
                .then(data => {
                    if (data.success) {
                        safeShowToast(data.message);
                        closeEditAlbumModal();
                        // Navigate to albums list to show updated data
                        switchView('user-albums');
                        setTimeout(() => loadUserAlbums(), 50);
                    } else {
                        safeShowToast(data.message);
                    }
                })
                .catch(err => safeShowToast('Lỗi kết nối'));
        });
    }

    // Upload song to album form
    const uploadSongForm = document.getElementById('upload-song-album-form');
    if (uploadSongForm) {
        uploadSongForm.addEventListener('submit', function (e) {
            e.preventDefault();

            if (!currentAddSongAlbumId) return;

            const formData = new FormData(uploadSongForm);

            safeShowToast('Đang upload...');

            fetch(`/useralbum/${currentAddSongAlbumId}/uploadsong`, {
                method: 'POST',
                body: formData
            })
                .then(res => res.json())
                .then(data => {
                    if (data.success) {
                        // Close modal first
                        try {
                            const modal = document.getElementById('add-song-album-modal');
                            if (modal) modal.classList.remove('active');
                        } catch (e) { console.error(e); }

                        safeShowToast(data.message);

                        // Force reload album detail
                        setTimeout(() => loadUserAlbum(currentAddSongAlbumId), 50);
                    } else {
                        safeShowToast(data.message);
                    }
                })
                .catch(err => safeShowToast('Lỗi kết nối'));
        });
    }

    // Tab switching in add song modal
    document.querySelectorAll('#add-song-album-modal .auth-tab').forEach(tab => {
        tab.addEventListener('click', function () {
            const mode = this.dataset.mode;

            document.querySelectorAll('#add-song-album-modal .auth-tab').forEach(t => t.classList.remove('active'));
            this.classList.add('active');

            if (mode === 'existing') {
                document.getElementById('existing-songs-tab')?.classList.remove('hidden');
                document.getElementById('upload-song-tab')?.classList.add('hidden');
            } else {
                document.getElementById('existing-songs-tab')?.classList.add('hidden');
                document.getElementById('upload-song-tab')?.classList.remove('hidden');
            }
        });
    });

    // Event delegation for edit album buttons (using data-attributes for safety)
    document.addEventListener('click', function (e) {
        const btn = e.target.closest('.edit-album-btn');
        if (btn) {
            const id = btn.dataset.id;
            const name = btn.dataset.name;
            const cover = btn.dataset.cover;
            const isPublic = btn.dataset.public === 'true';
            const description = btn.dataset.description || '';
            openEditUserAlbumModal(id, name, cover, isPublic, description);
        }
    });
});
