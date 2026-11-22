namespace Loupedeck.ArcaneVaultRazer;

public sealed class ArcaneButtons : PluginDynamicCommand
{
    public ArcaneButtons() : base()
    {
        this.AddParameter("deck", "Selected Deck W/L", "Arcane Vault");
        this.AddParameter("rank", "Arena Constructed Rank", "Arcane Vault");
        this.AddParameter("value", "Collection Value", "Arcane Vault");
        this.AddParameter("previous", "Previous Deck", "Arcane Vault");
        this.AddParameter("next", "Next Deck", "Arcane Vault");
        this.AddParameter("refresh", "Refresh Arcane Vault", "Arcane Vault");
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
            foreach (var action in new[] { "deck", "rank", "value", "previous", "next", "refresh" })
                this.ActionImageChanged(action);
        }
        catch (Exception ex)
        {
            PluginLog.Warning("Arcane Vault command failed: " + ex.Message);
        }
    }
}
