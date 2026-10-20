using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.Core;
using Nexus.Storage;

namespace Nexus.UI;

public sealed class MouseLabViewModel : ObservableObject
{
    public MouseLabStore Store {get;}
    private string status="Pronto. Nenhum teste ou ajuste ativo.";
    public string Status {get=>status;set=>SetProperty(ref status,value);}
    private bool advanced;
    public bool Advanced {get=>advanced;set=>SetProperty(ref advanced,value);}
    public MouseAnalysis? Analysis {get;set;}
    public MouseLabViewModel(string root)=>Store=new(Path.Combine(root,"nexus.db"));
}
