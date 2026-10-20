using Microsoft.UI.Xaml;

namespace Nexus.UI;

public sealed partial class MainWindow
{
    private MouseLabView? mouseLab;
    private void InitializeMouseLab(string root)
    {
        try { mouseLab=new(this,root,optimizer,library,()=>operationRunning||sessions.Active is not null||autoCleanupBusy||benchmarkCancellation is not null||temporaryCancellation is not null,Confirm,
            ()=>operationRunning||autoCleanupBusy||benchmarkCancellation is not null||temporaryCancellation is not null); }
        catch(Exception ex){MouseLabHost.Content=new Microsoft.UI.Xaml.Controls.TextBlock{Text="Mouse Lab indisponível: "+ex.Message,TextWrapping=TextWrapping.Wrap};Nexus.Diagnostics.AppLog.Error(ex,"Inicializar Mouse Lab");return;}
        MouseLabHost.Content=mouseLab;
        mouseLab.Notification+=message=>Notice(message,message.StartsWith("Automação interrompida",StringComparison.Ordinal));
        Activated+=(_,e)=>{if(e.WindowActivationState==WindowActivationState.Deactivated)mouseLab.LoseFocus();};
        AppWindow.Closing+=(_,args)=>
        {
            if(args.Cancel)return;
            try{mouseLab.PrepareClose();}
            catch(Exception ex){args.Cancel=true;Notice(ex.Message,true);ShowPage("mouselab");mouseLab.SelectSection(9);}
        };
        Closed+=(_,_)=>mouseLab.Dispose();
    }
    private async void ActivateMouseLab()
    {if(mouseLab is null)return;try{await mouseLab.ActivateAsync();}catch(Exception ex){Notice("Mouse Lab: "+ex.Message,true);}}
}
