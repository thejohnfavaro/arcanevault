namespace Loupedeck.ArcaneVaultRazer;

/// <summary>Turn to cycle saved decks, press to reset to the first deck.</summary>
public sealed class ArcaneDeckDial : PluginDynamicAdjustment
{
    public ArcaneDeckDial() :
        base(displayName: "Select Saved Deck", description:
            "Rotate through Arcane Vault saved decks; press to reset.", groupName:
            "Arcane Vault", hasReset: true) { }

    protected override void ApplyAdjustment(string actionParameter, int diff)
    {
        if (diff == 0) return;
        ArcaneSnapshot.Move(diff);
        this.AdjustmentValueChanged();
    }

    protected override void RunCommand(string actionParameter)
    {
        ArcaneSnapshot.ResetSelection();
        this.AdjustmentValueChanged();
    }

    protected override string GetAdjustmentValue(string actionParameter) =>
        ArcaneSnapshot.Selected(ArcaneSnapshot.Read())?.Name ?? "No decks";
}
