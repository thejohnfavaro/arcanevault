namespace Loupedeck.ArcaneVaultRazer;

// Universal plugin: actions work even while MTG Arena has focus.
public sealed class ArcaneVaultPlugin : Plugin
{
    public override bool UsesApplicationApiOnly => true;
    public override bool HasNoApplication => true;

    public ArcaneVaultPlugin()
    {
        PluginLog.Init(this.Log);
        PluginResources.Init(this.Assembly);
    }

    public override void Load() { }
    public override void Unload() { }
}
