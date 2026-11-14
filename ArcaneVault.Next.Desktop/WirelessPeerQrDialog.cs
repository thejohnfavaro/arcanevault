using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Renders a one-use peer link entirely locally. The bearer URL is never sent
/// to a third-party QR service or network endpoint. Nothing is stored here.
/// </summary>
internal static class WirelessPeerQrDialog
{
    public static void Show(Window owner,string title,string url,string purpose)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||
            uri.Scheme!="http"||
            !uri.AbsolutePath.StartsWith("/arcane-peer/",StringComparison.Ordinal)||
            uri.Query.Length>0)
            throw new InvalidDataException("Cannot display an invalid peer link.");

        using var generator=new QRCodeGenerator();
        using var data=generator.CreateQrCode(url,QRCodeGenerator.ECCLevel.Q);
        var encoded=new PngByteQRCode(data).GetGraphic(8);
        var bitmap=new BitmapImage();
        using(var buffer=new MemoryStream(encoded))
        {
            bitmap.BeginInit();
            bitmap.CacheOption=BitmapCacheOption.OnLoad;
            bitmap.StreamSource=buffer;
            bitmap.EndInit();
            bitmap.Freeze();
        }

        var dialog=new Window
        {
            Owner=owner,
            Title=title,
            Width=490, Height=650, MinWidth=400,MinHeight=570,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Background=new SolidColorBrush(Color.FromRgb(19,26,38)),
            Foreground=Brushes.White,
            ResizeMode=ResizeMode.CanResize,
            ShowInTaskbar=false
        };
        var layout=new StackPanel
        {
            Margin=new Thickness(22,20,22,20),
            HorizontalAlignment=HorizontalAlignment.Stretch
        };
        layout.Children.Add(new TextBlock
        {
            Text=title, FontSize=22,FontWeight=FontWeights.SemiBold,
            Margin=new Thickness(0,0,0,9)
        });
        layout.Children.Add(new TextBlock
        {
            Text=purpose+"\nScan this QR in Arcane Vault Android → Data & Sync → "+
                "Scan Windows Wi-Fi QR code.",
            TextWrapping=TextWrapping.Wrap,
            Foreground=new SolidColorBrush(Color.FromRgb(175,199,215)),
            FontSize=13,Margin=new Thickness(0,0,0,13)
        });
        var qrFrame=new Border
        {
            Background=Brushes.White,
            Padding=new Thickness(13),
            CornerRadius=new CornerRadius(13),
            HorizontalAlignment=HorizontalAlignment.Center,
            Child=new Image
            {
                Source=bitmap, Width=295,Height=295,
                Stretch=Stretch.Uniform,
                SnapsToDevicePixels=true
            }
        };
        layout.Children.Add(qrFrame);
        layout.Children.Add(new TextBlock
        {
            Text="ONE-TIME WI-FI LINK · EXPIRES IN 5 MINUTES",
            FontSize=11,FontWeight=FontWeights.Bold,
            Foreground=new SolidColorBrush(Color.FromRgb(111,225,209)),
            Margin=new Thickness(0,15,0,7),
            TextAlignment=TextAlignment.Center
        });
        var field=new TextBox
        {
            Text=url,IsReadOnly=true,FontSize=12,
            Foreground=Brushes.White,
            Background=new SolidColorBrush(Color.FromRgb(32,43,59)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(58,74,94)),
            TextWrapping=TextWrapping.Wrap,
            Padding=new Thickness(9,7,9,7)
        };
        layout.Children.Add(field);
        var actions=new StackPanel
        {
            Orientation=Orientation.Horizontal,
            HorizontalAlignment=HorizontalAlignment.Center,
            Margin=new Thickness(0,13,0,0)
        };
        var copy=new Button{Content="Copy link",Padding=new Thickness(15,9,15,9),Margin=new Thickness(0,0,9,0)};
        copy.Click+=(s,e)=>{Clipboard.SetText(url);copy.Content="Copied";};
        actions.Children.Add(copy);
        var dismiss=new Button{Content="Close",Padding=new Thickness(15,9,15,9)};
        dismiss.Click+=(s,e)=>dialog.Close();
        actions.Children.Add(dismiss);
        layout.Children.Add(actions);
        layout.Children.Add(new TextBlock
        {
            Text="This link grants temporary snapshot access. Use trusted home Wi-Fi only; "+
                "the current peer transfer is not encrypted.",
            TextWrapping=TextWrapping.Wrap,
            Foreground=new SolidColorBrush(Color.FromRgb(175,199,215)),
            FontSize=11,Margin=new Thickness(0,13,0,0)
        });
        dialog.Content=new ScrollViewer
        {
            Content=layout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto
        };
        dialog.ShowDialog();
    }
}
