using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Explicit exported-backup preview shell. Only the standalone preview remains
/// a Window. Actual Collection and Deck Library now live inside MainWindow and
/// both use NativeCollectionWorkspace's authoritative shared implementation.
/// </summary>
public partial class NativeCollectionWindow : Window
{
    public NativeCollectionWindow(NativeBackupView backup,
        CollectionCatalogReview.Result review, string? catalogPath,
        NativeSetProgress.Result? progress, bool nativeWorkspace = false,
        string? nativePath = null, string? expectedBackupSha = null,
        Func<Task>? reopenNative = null,
        IReadOnlyList<NativeDeckEditing.DeckRow>? nativeDecks = null)
    {
        InitializeComponent();
        PreviewHost.Content = new NativeCollectionWorkspace(
            backup, review, catalogPath, progress,
            nativeWorkspace: nativeWorkspace,
            nativePath: nativePath,
            expectedBackupSha: expectedBackupSha,
            reopenNative: async () =>
            {
                Close();
                if (reopenNative is not null) await reopenNative();
            },
            nativeDecks: nativeDecks);
    }
}
