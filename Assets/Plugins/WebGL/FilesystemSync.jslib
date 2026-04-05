mergeInto(LibraryManager.library, {
    SyncFilesystem: function() {
        FS.syncfs(false, function(err) {
            if (err) console.error('[FilesystemSync] IndexedDB sync error:', err);
        });
    },

    RequestPersistentStorage: function() {
        if (navigator.storage && navigator.storage.persist) {
            navigator.storage.persist().then(function(granted) {
                if (granted)
                    console.log('[FilesystemSync] Persistent storage granted');
                else
                    console.warn('[FilesystemSync] Persistent storage denied');
            });
        }
    },

    LogStorageEstimate: function() {
        if (navigator.storage && navigator.storage.estimate) {
            navigator.storage.estimate().then(function(est) {
                var usedMB = (est.usage / (1024 * 1024)).toFixed(1);
                var quotaMB = (est.quota / (1024 * 1024)).toFixed(0);
                var pct = ((est.usage / est.quota) * 100).toFixed(1);
                console.log('[FilesystemSync] Storage: ' + usedMB + ' MB / ' + quotaMB + ' MB (' + pct + '%)');
            });
        }
    }
});
