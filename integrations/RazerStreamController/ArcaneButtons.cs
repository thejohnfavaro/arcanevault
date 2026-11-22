namespace Loupedeck.ArcaneVaultRazer;

public sealed class ArcaneButtons : PluginDynamicCommand
{
    private readonly System.Threading.Timer snapshotWatcher;
    private long lastSnapshotStamp;

    public ArcaneButtons() : base()
    {
        this.AddParameter("deck", "Selected Deck W/L", "Arcane Vault");
        this.AddParameter("rank", "Arena Constructed Rank", "Arcane Vault");
        this.AddParameter("value", "Collection Value", "Arcane Vault");
        this.AddParameter("previous", "Previous Deck", "Arcane Vault");
        this.AddParameter("next", "Next Deck", "Arcane Vault");
        this.AddParameter("refresh", "Refresh Arcane Vault", "Arcane Vault");
        ArcaneSnapshot.SelectionChanged += RefreshLabels;
        // Re-read the existing local snapshot on file changes. This watcher
        // never opens the app's SQLite data and does not trigger new exports.
        snapshotWatcher = new System.Threading.Timer(_ =>
        {
            try
            {
                long stamp = File.Exists(ArcaneSnapshot.SnapshotPath)
                    ? File.GetLastWriteTimeUtc(ArcaneSnapshot.SnapshotPath).Ticks : 0;
                if (stamp == System.Threading.Interlocked.Read(ref lastSnapshotStamp)) return;
                System.Threading.Interlocked.Exchange(ref lastSnapshotStamp, stamp);
                RefreshLabels();
            }
            catch (Exception ex)
            {
                PluginLog.Info("Arcane Vault snapshot update unavailable: " + ex.Message);
            }
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
    }

    private void RefreshLabels()
    {
        foreach (var action in new[] { "deck", "rank", "value", "previous", "next", "refresh" })
            this.ActionImageChanged(action);
    }

    protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize)
    {
        var state = ArcaneSnapshot.Read();
        var deck = ArcaneSnapshot.Selected(state);
        return actionParameter switch
        {
            "deck" => deck is null ? "Deck: —" :
                deck.Name + " " + deck.Wins + "W/" + deck.Losses + "L",
            "rank" => "Rank " + state.Rank,
            "value" => "Value " + state.Value,
            "previous" => "◀ Deck",
            "next" => "Deck ▶",
            "refresh" => "Refresh Arcane Vault",
            _ => "Arcane Vault"
        };
    }

    protected override void RunCommand(string actionParameter)
    {
        try
        {
            switch (actionParameter)
            {
                case "previous": ArcaneSnapshot.Move(-1); break;
                case "next": ArcaneSnapshot.Move(1); break;
                case "refresh": ArcaneSnapshot.RequestRefresh(); break;
            }
            RefreshLabels();
        }
        catch (Exception ex)
        {
            PluginLog.Info("Arcane Vault command failed: " + ex.Message);
        }
    }
}
