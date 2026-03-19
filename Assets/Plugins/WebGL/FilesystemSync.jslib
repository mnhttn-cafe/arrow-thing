mergeInto(LibraryManager.library, {
    SyncFilesystem: function() {
        FS.syncfs(false, function(err) {
            if (err) console.error('[SaveManager] IndexedDB sync error:', err);
        });
    }
});
