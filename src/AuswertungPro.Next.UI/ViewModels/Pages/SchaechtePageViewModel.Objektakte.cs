using CommunityToolkit.Mvvm.Input;
namespace AuswertungPro.Next.UI.ViewModels.Pages;
public sealed partial class SchaechtePageViewModel
{
    public System.Func<System.Guid, ObjektakteViewModel?>? ObjektakteErstellen { get; }
    public IRelayCommand ObjektakteCommand { get; } = new RelayCommand(() => { });
}
