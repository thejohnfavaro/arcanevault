using ArcaneVault.Next.Collection;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

public partial class NativeCardDetailsWindow : Window
{
    public NativeCardDetailsWindow(string name, string ownership,
        CollectionCatalogReview.CardDetails? info)
    {
        InitializeComponent();
        CardName.Text = name;
        Ownership.Text = ownership;
        if (info is null)
        {
            CardMetadata.Text = "No local card metadata available — open a completed Scryfall catalog snapshot.";
            OracleText.Text = "Card name and owned quantities are preserved from the backup. " +
                "No Scryfall details are currently loaded.";
        }
        else
        {
            CardMetadata.Text = $"{info.TypeLine}  ·  {info.Rarity}  ·  Mana value {info.ManaValue:G}  · " +
                $"Color identity: {(string.IsNullOrEmpty(info.ColorIdentity) ? "Colorless" : info.ColorIdentity)}" +
                $"\nCached legalities: {info.Legalities}";
            OracleText.Text = info.OracleText;
        }
    }
}
