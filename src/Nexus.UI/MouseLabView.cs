using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Nexus.Core;
using Nexus.Hardware;
using Nexus.Storage;
using Nexus.Optimization;
using Nexus.Gaming;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Nexus.UI;

public sealed class MouseLabView : UserControl, IDisposable
{
    public static IReadOnlyList<string> Sections {get;}=new[]{"Dashboard","Mouse Settings","Sensor Control","Performance Analyzer","Aim Trainer","Sensitivity Lab","Gaming Profiles","Mouse Health","Advanced Settings","Optimization History"};
    public MouseLabViewModel Model {get;}
    private readonly Window owner;
    private readonly SystemOptimizer optimizer;
    private readonly GameLibrary library;
    private readonly Func<bool> conflict;
    private readonly Func<string,string,Task<bool>> confirm;
    private readonly WindowsMouseProvider provider=new();
    private readonly MouseProfileSession profileSession;
    private readonly StackPanel body=new(){Spacing=16};
    private readonly ComboBox navigation=new(){Header="Ferramenta",HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly ComboBox devices=new(){Header="Dispositivo do teste",HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly ToggleSwitch advanced=new(){Header="Modo de utilização",OffContent="Simples",OnContent="Avançado"};
    private readonly DispatcherTimer captureTimer=new(){Interval=TimeSpan.FromMilliseconds(200)};
    private readonly DispatcherTimer aimTimer=new(){Interval=TimeSpan.FromMilliseconds(33)};
    private readonly DispatcherTimer automationTimer=new(){Interval=TimeSpan.FromSeconds(5)};
    private MouseRawCapture? capture;
    private IReadOnlyList<MouseDevice> known=[];
    private bool active,disposed,busy,automationBusy;
    private string? lastOfferedProcess;
    private bool automationEnabled;
    private IReadOnlyList<LibraryApp> automationLibrary=[];
    private IReadOnlyList<MouseProfile> automationProfiles=[];
    private TextBlock? captureText,trainingText;
    private Canvas? graph,arena;
    private Ellipse? target,secondTarget;
    private AimExercise? exercise;
    private readonly Stopwatch trainingClock=new();
    private int trainingDpi;
    private double trainingSensitivity;
    public bool Busy=>busy;
    public bool HasActiveSession=>profileSession.Active;
    public bool RecoveryRequired=>profileSession.RecoveryRequired;
    public bool Capturing=>capture is not null;
    public bool AutomationEnabled=>automationEnabled;
    public int DeviceCount=>known.Count;
    private static SolidColorBrush Brush(byte shade)=>new(Windows.UI.Color.FromArgb(255,shade,shade,shade));
    private static TextBlock Text(string text,double size=14)=>new(){Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Foreground=Brush(225)};
    private static StackPanel Stack(params UIElement[] elements){var s=new StackPanel{Spacing=10};foreach(var e in elements)s.Children.Add(e);return s;}
    private static Border Card(string title,params UIElement[] elements)
    {var s=Stack(Text(title,19));foreach(var e in elements)s.Children.Add(e);return new(){Child=s,Background=Brush(22),BorderBrush=Brush(55),BorderThickness=new(1),CornerRadius=new(14),Padding=new(18)};}
    private Button Button(string label,Func<Task> action,string? help=null)
    {var b=new Button{Content=label,HorizontalAlignment=HorizontalAlignment.Stretch};b.Click+=async(_,_)=>await Execute(action);if(help is not null)ToolTipService.SetToolTip(b,help);return b;}
    private Button Link(string label,string uri)=>Button(label,async()=>{if(!await Windows.System.Launcher.LaunchUriAsync(new Uri(uri)))throw new IOException("Não foi possível abrir a página oficial.");});
    private async Task Execute(Func<Task> action)
    {
        if(disposed || busy)return;
        busy=true;
        try{await action();}
        catch(Exception e){Model.Status="Não foi possível concluir: "+e.Message;Nexus.Diagnostics.AppLog.Error(e,"Mouse Lab");}
        finally{busy=false;}
    }
    private static NumberBox Number(string label,double value,double min,double max)=>new(){Header=label,Value=value,Minimum=min,Maximum=max,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact};
    private static ComboBox Choice(string label,IEnumerable<string> choices,int index=0)
    {var c=new ComboBox{Header=label,ItemsSource=choices.ToArray(),HorizontalAlignment=HorizontalAlignment.Stretch};c.SelectedIndex=index;return c;}
    public MouseLabView(Window owner,string root,SystemOptimizer optimizer,GameLibrary library,Func<bool> conflict,Func<string,string,Task<bool>> confirm)
    {
        this.owner=owner;this.optimizer=optimizer;this.library=library;this.conflict=conflict;this.confirm=confirm;
        Model=new(root);profileSession=new(optimizer);DataContext=Model;RequestedTheme=ElementTheme.Dark;
        var header=Stack(Text("NEXOS MOUSE LAB",27),Text("Conhece o teu rato. Mede, treina e compara — com controlo sobre cada alteração."));
        var status=Text("");status.SetBinding(TextBlock.TextProperty,new Binding{Source=Model,Path=new PropertyPath(nameof(Model.Status)),Mode=BindingMode.OneWay});
        var rootPanel=Stack(Card("O teu espaço de precisão",header,advanced,navigation,devices,Button("Atualizar dispositivos",DetectAsync),status),body);
        Content=new Border{Child=rootPanel,Background=Brush(12),Padding=new(4),CornerRadius=new(14)};
        navigation.ItemsSource=Sections;navigation.SelectionChanged+=(_,_)=>Render();
        advanced.Toggled+=(_,_)=>{Model.Advanced=advanced.IsOn;Render();};
        devices.SelectionChanged+=(_,_)=>{if(capture is not null)FinishCapture("Dispositivo selecionado mudou.");Render();};
        navigation.SelectedIndex=0;
        captureTimer.Tick+=(_,_)=>CaptureTick();aimTimer.Tick+=(_,_)=>AimTick();
        automationTimer.Tick+=async(_,_)=>await PollAutomationAsync();
    }
    public async Task ActivateAsync()
    {active=true;if(known.Count==0)await Execute(DetectAsync);else Render();}
    public void Deactivate()
    {active=false;FinishCapture("Teste interrompido ao sair do Mouse Lab.");FinishTraining("Treino interrompido ao sair da página.");}
    public void LoseFocus()
    {FinishCapture("Captura interrompida ao perder o foco.");FinishTraining("Treino interrompido ao perder o foco.");}
    public void SelectSection(int index){if(index<0||index>=Sections.Count)throw new ArgumentOutOfRangeException(nameof(index));navigation.SelectedIndex=index;}
    private async Task DetectAsync()
    {
        if(capture is not null)FinishCapture("Lista de dispositivos atualizada.");
        var result=await Task.Run(provider.Detect);if(disposed)return;
        foreach(var missing in known.Where(k=>result.All(d=>!d.Id.Equals(k.Id,StringComparison.OrdinalIgnoreCase))))
            Model.Store.Record("health","Desconexão observada",new{Name=missing.Name,Note="Ausente entre duas consultas; momento exato desconhecido."});
        var selected=(devices.SelectedItem as MouseDevice)?.Id;known=result;
        devices.ItemsSource=result;devices.SelectedItem=result.FirstOrDefault(d=>d.Id==selected)??result.FirstOrDefault();
        Model.Status=result.Count==0?"Nenhum apontador Raw Input disponível nesta sessão.":$"{result.Count} apontador(es) encontrado(s). Seleciona o rato que vais testar.";Render();
    }
    private void Render()
    {
        if(body is null || disposed)return;
        FinishCapture("Teste interrompido ao mudar de separador.");FinishTraining("Treino interrompido ao mudar de separador.");
        body.Children.Clear();captureText=null;graph=null;trainingText=null;arena=null;target=null;secondTarget=null;
        try { switch(navigation.SelectedIndex)
        {
            case 0: Dashboard();break;case 1: Settings();break;case 2: Sensor();break;case 3: Analyzer();break;case 4: Trainer();break;
            case 5: Sensitivity();break;case 6: Profiles();break;case 7: Health();break;case 8: Advanced();break;case 9: History();break;
        } } catch(Exception ex) { Model.Status="Não foi possível carregar esta ferramenta: "+ex.Message;body.Children.Add(Card("Ferramenta temporariamente indisponível",Text("As restantes funções da NEXUS continuam disponíveis. Não foram aplicadas configurações.")));Nexus.Diagnostics.AppLog.Error(ex,"Mouse Lab render"); }
    }
    private void Dashboard()
    {
        var d=devices.SelectedItem as MouseDevice;
        body.Children.Add(Card("O teu rato",Text(d?.ToString()??"Seleciona um dispositivo na lista."),Text(d?.Connection??"Ligação desconhecida"),
            Text("DPI atual, sensor, bateria e firmware: não disponíveis pela interface genérica."),Text("O perfil guarda valores pretendidos; guardar não configura o hardware.")));
        body.Children.Add(Card("Começa em três passos",Button("1 · Rever definições do rato",()=>Navigate(1)),Button("2 · Medir movimento e cliques",()=>Navigate(3)),Button("3 · Treinar a mira",()=>Navigate(4))));
        body.Children.Add(Card("Assistente de calibração",Text("1. Confirma o DPI no software oficial. 2. Escolhe uma sensibilidade confortável. 3. Faz um treino de 30 segundos. 4. Repete uma configuração de cada vez e compara o histórico."),Button("Abrir Sensitivity Lab",()=>Navigate(5))));
        var advice=MouseRecommendations.Create("Balanced",Model.Analysis,MousePrecision.ParseState(MousePrecision.Resolve().Read())[2]!=0,1000);
        body.Children.Add(Card("Recomendações locais",Text(string.Join("\n\n",advice.Select(r=>$"{r.Title}\n{r.Reason}\n{r.Benefit}\nLimite: {r.Tradeoff}\nConfiança: {r.Confidence}"))),Button("Testar configuração",()=>Navigate(3))));
    }
    private Task Navigate(int index){navigation.SelectedIndex=index;return Task.CompletedTask;}
    private void Settings()
    {
        var speed=Number("Velocidade do ponteiro (Windows, 1–20)",int.Parse(MousePrecision.ResolveSpeed().Read()),1,20);
        var accel=MousePrecision.ParseState(MousePrecision.Resolve().Read())[2]!=0;
        var disable=new CheckBox{Content="Desligar Melhorar precisão do ponteiro",IsChecked=false};
        body.Children.Add(Card("Definições Windows",Text("Precisão melhorada: "+(accel?"ativa":"desligada")),speed,disable,
            Text("Afeta o ponteiro e aplicações que usam a entrada processada do Windows. Jogos com Raw Input podem ignorar estas opções."),
            Button("Rever e aplicar",async()=>
            {
                EnsureCanChange();if(!double.IsFinite(speed.Value)||speed.Value!=Math.Truncate(speed.Value))throw new ArgumentException("Usa uma velocidade inteira.");
                var p=MouseProfile.Defaults[2] with{WindowsSpeed=(int)speed.Value,DisableAcceleration=disable.IsChecked==true};
                var prepared=MouseProfileSession.Prepare(p,WindowsSettings.Resolve);
                if(!await confirm("Aplicar definições do ponteiro?",Describe(prepared)))return;
                EnsureCanChange();var applied=optimizer.ApplyBatch(prepared);Model.Status=$"{applied} alteração(ões) aplicada(s) e verificada(s). Valores anteriores guardados no histórico.";
            }),Button("Restaurar alterações do rato",RestoreMouseAsync),Link("Abrir acessibilidade do rato","ms-settings:easeofaccess-mousepointer")));
        body.Children.Add(Card("DPI e polling",Text("Escolhe o DPI e polling no software oficial. Perfis NEXOS guardam a intenção; não enviam comandos HID de configuração."),
            Text("125 / 250 / 500 / 1000 / 2000 / 4000 / 8000 Hz. Taxas elevadas podem aumentar CPU e consumo; mede a estabilidade e o jogo antes de decidir."),
            Button("Guardar ou escolher perfil",()=>Navigate(6)),OfficialLink()));
    }
    private Button OfficialLink()=>Link("Abrir página oficial Attack Shark R5 Ultra","https://attackshark.com/products/attack-shark-r5ultra-carbon-fibre-wireless-8k-paw3950max-gaming-mouse");
    private void Sensor()
    {
        body.Children.Add(Card("Controlo do sensor",Text("A NEXOS não encontrou uma integração pública validada para escrever as definições do R5 Ultra. Usa o software ou Web Driver indicado pelo fabricante."),OfficialLink()));
        var descriptions=new[]{
            ("Motion Sync","Pode sincronizar amostras com transmissões; implementação e compromissos dependem do fabricante."),
            ("Angle Snapping","Pode corrigir trajetórias; em jogos pode alterar o movimento pretendido."),
            ("Ripple Control","Filtragem de ruído; pode introduzir suavização e diferenças no movimento."),
            ("Lift-off Distance","Altura em que o sensor deixa de seguir a superfície. Ajusta ao tapete no software oficial."),
            ("Debounce Time","Filtragem de transições dos switches; valores baixos podem aumentar cliques não desejados."),
            ("Energia e perfis DPI","Autonomia, suspensão e níveis DPI dependem de suporte do rato e da ligação.")};
        foreach(var item in descriptions)
        {var button=new Button{Content="Configuração no fabricante",IsEnabled=false};ToolTipService.SetToolTip(button,"Sem interface validada de escrita pela NEXOS.");body.Children.Add(Card(item.Item1,Text(item.Item2),button));}
        if(Model.Advanced)body.Children.Add(Card("Capacidades do fornecedor",Text(string.Join("\n\n",provider.Capabilities(devices.SelectedItem as MouseDevice??new("","",null,null,"",provider.Name)).Select(c=>$"{c.Name} · {c.Access}\n{c.Explanation}")))));
        body.Children.Add(Card("R5 Ultra — especificação, não leitura",Text("PAW3950MAX; até 42000 DPI. O fabricante indica até 8000 Hz com recetor 8K em 2.4 GHz e 1000 Hz por cabo. Não confirma as definições do rato selecionado.")));
    }
    private void Analyzer()
    {
        captureText=Text("Sem teste ativo. Movimenta continuamente o rato durante o teste e experimenta os cinco botões e a roda.");
        graph=new Canvas{Height=110,Background=Brush(10),HorizontalAlignment=HorizontalAlignment.Stretch};
        body.Children.Add(Card("Performance Analyzer",Text("Teste de 15 segundos, apenas com esta janela em primeiro plano e um dispositivo selecionado. Interrompe ao sair ou perder foco."),
            Button("Iniciar teste de movimento, polling e cliques",StartCaptureAsync),Button("Parar e guardar resultado",()=>{FinishCapture("Teste parado pelo utilizador.");return Task.CompletedTask;}),captureText,graph,
            Text(MouseAnalysis.Limitation),Text("Micro-stutter: observa intervalos e pausas; não é um diagnóstico de falha do sensor. Aceleração: a configuração Windows é legível; este teste Raw Input não mede a curva de aceleração do ponteiro.")));
        if(Model.Analysis is {} a)body.Children.Add(Card("Último resultado",Text(Format(a))));
    }
    private async Task StartCaptureAsync()
    {
        if(!active || capture is not null)return;
        if(devices.SelectedItem is not MouseDevice d)throw new InvalidOperationException("Seleciona um rato detetado.");
        if(!await confirm("Iniciar teste local?","Durante até 15 segundos a NEXOS recolhe movimento relativo, botões e roda deste dispositivo, apenas com a janela em primeiro plano. Guarda um resumo local; não altera definições."))return;
        var next=new MouseRawCapture(WinRT.Interop.WindowNative.GetWindowHandle(owner),d.Id);
        try{next.Start();capture=next;captureTimer.Start();Model.Status="Teste ativo. Move o rato continuamente; podes parar a qualquer momento.";}
        catch{next.Dispose();throw;}
    }
    private void CaptureTick()
    {
        if(capture is not {} c){captureTimer.Stop();return;}
        if(!c.Active || c.ElapsedMs>=15000){FinishCapture(c.StopReason??"Teste de 15 segundos concluído.");return;}
        if(captureText is not null)
        {
            var b=c.Samples.Aggregate(0,(flags,s)=>flags|s.Buttons);
            captureText.Text=$"{c.ElapsedMs/1000:F1} / 15 s · {c.Samples.Count} eventos · botões observados: esquerdo {(b&1)!=0}, direito {(b&4)!=0}, meio {(b&16)!=0}, lateral 1 {(b&64)!=0}, lateral 2 {(b&256)!=0} · roda: {c.Samples.Count(s=>s.Wheel!=0)}";
        }
        var recent=c.Samples.TakeLast(160).Where(s=>!s.Absolute && (s.X!=0||s.Y!=0)).ToArray();
        DrawGraph(recent.Zip(recent.Skip(1),(a,b)=>b.Ms-a.Ms).ToArray());
    }
    private void FinishCapture(string reason)
    {
        captureTimer.Stop();if(capture is not {} c)return;capture=null;
        try
        {
            c.Stop(reason);var a=MouseAnalyzer.Analyze(c.Samples,c.Discarded);Model.Analysis=a;
            var name=(devices.SelectedItem as MouseDevice)?.Name??"Rato selecionado";
            Model.Store.Record("analysis",name,new MouseAnalysisRecord(a,reason,name));
            if(captureText is not null)captureText.Text=reason+"\n"+Format(a);DrawGraph(a.IntervalGraph);Model.Status=reason+" Resumo guardado localmente.";
        }
        catch(Exception e){Model.Status="Não foi possível guardar o resultado: "+e.Message;}
        finally{c.Dispose();}
    }
    private static string Format(MouseAnalysis a)=>$"{a.Movements} eventos de movimento relativo · frequência média observada: {N(a.EventsPerSecond)} eventos/s\nIntervalo mediano {N(a.MedianIntervalMs)} ms · P95 {N(a.P95IntervalMs)} ms · desvio {N(a.IntervalDeviationMs)} ms\n{a.GapsOver20Ms} intervalos >20 ms (inclui pausas) · {a.Clicks} cliques esquerdos · intervalo médio entre cliques {N(a.MeanClickIntervalMs)} ms · {a.CloseClickPairs} pares <30 ms\n{a.WheelEvents} eventos de roda · {a.AbsoluteEvents} eventos absolutos · {a.Discarded} leituras perdidas\nÍndice de continuidade de entrega: {(a.DiagnosticScore is {} score?score+"/100 (% de intervalos >0 e ≤20 ms)":"amostra insuficiente ou incompatível")}. Não avalia a habilidade, o sensor ou FPS.";
    private static string N(double? value)=>value?.ToString("F2",CultureInfo.CurrentCulture)??"não disponível";
    private void DrawGraph(double[] values)
    {
        if(graph is null)return;graph.Children.Clear();if(values.Length<2||graph.ActualWidth<=0)return;
        var line=new Polyline{Stroke=Brush(245),StrokeThickness=1.5};var max=Math.Max(1,values.Max());
        for(var i=0;i<values.Length;i++)line.Points.Add(new(i*graph.ActualWidth/(values.Length-1),100-95*values[i]/max));graph.Children.Add(line);
    }
    private void Trainer()
    {
        var mode=Choice("Exercício",new[]{"Flick","Tracking","Microajustes","Reação","Troca de alvos"});
        var difficulty=Choice("Dificuldade",new[]{"1 · Confortável","2 · Intermédia","3 · Pequenos alvos"});
        var dpi=Number("DPI declarado para esta sessão",800,50,100000);var sensitivity=Number("Sensibilidade declarada (registo)",1,.001,1000);
        trainingText=Text("Treino local de 30 segundos. Estes campos guardam contexto; não alteram o rato nem reproduzem a mira do jogo.");
        arena=new Canvas{Height=360,Background=Brush(7),HorizontalAlignment=HorizontalAlignment.Stretch};
        arena.PointerMoved+=(_,e)=>{if(exercise is null||arena is null)return;var p=e.GetCurrentPoint(arena);if(p.PointerDeviceType==Microsoft.UI.Input.PointerDeviceType.Mouse){var normalized=Normalize(p.Position.X,p.Position.Y);exercise.Move(normalized.X,normalized.Y);}};
        arena.PointerExited+=(_,_)=>exercise?.Move(null,null);
        arena.PointerPressed+=(_,e)=>
        {
            if(exercise is null||arena is null)return;var p=e.GetCurrentPoint(arena);
            if(p.PointerDeviceType!=Microsoft.UI.Input.PointerDeviceType.Mouse||!p.Properties.IsLeftButtonPressed)return;
            var normalized=Normalize(p.Position.X,p.Position.Y);exercise.Click(normalized.X,normalized.Y,trainingClock.Elapsed.TotalSeconds);PaintTarget();e.Handled=true;
        };
        body.Children.Add(Card("NEXOS Aim Trainer",mode,difficulty,dpi,sensitivity,
            Button("Começar treino",()=>
            {
                if(!active)return Task.CompletedTask;FinishCapture("Treino iniciado.");FinishTraining("Sessão anterior parada.");
                if(dpi.Value!=Math.Truncate(dpi.Value))throw new ArgumentException("Introduz um DPI inteiro.");MouseLabMath.Edpi(dpi.Value,sensitivity.Value);
                trainingDpi=(int)dpi.Value;trainingSensitivity=sensitivity.Value;exercise=new((AimMode)mode.SelectedIndex,difficulty.SelectedIndex+1);
                trainingClock.Restart();aimTimer.Start();PaintTarget();Model.Status="Treino em curso. Sair ou perder foco termina e guarda a sessão parcial.";return Task.CompletedTask;
            }),Button("Terminar treino",()=>{FinishTraining("Treino terminado pelo utilizador.");return Task.CompletedTask;}),trainingText,arena,
            Text("Tracking: mantém o ponteiro dentro do alvo. Nos outros modos, clica no alvo branco. Reação penaliza cliques antes de aparecer. Tempos observados incluem a aplicação e o ecrã; não são latência do switch. Não interage com jogos externos.")));
        RenderTrainingHistory();
    }
    private (double X,double Y) Normalize(double x,double y)
    {var side=Math.Max(1,Math.Min(arena!.ActualWidth,arena.ActualHeight));return((x-(arena.ActualWidth-side)/2)/side,y/side);}
    private void AimTick()
    {try{if(exercise is null){aimTimer.Stop();return;}exercise.Tick(trainingClock.Elapsed.TotalSeconds);PaintTarget();if(trainingText is not null)trainingText.Text=$"{exercise.Elapsed:F1} / 30 s · {exercise.Hits} acertos · {exercise.Misses} falhas";if(exercise.Finished)FinishTraining("Treino de 30 segundos concluído.");}catch(Exception e){FinishTraining("Treino interrompido: "+e.Message);}}
    private void PaintTarget()
    {
        if(arena is null||exercise is null)return;
        var side=Math.Max(1,Math.Min(arena.ActualWidth,arena.ActualHeight));var radius=exercise.Radius*side;
        if(target is null){target=new Ellipse{Fill=Brush(245),IsHitTestVisible=false};arena.Children.Add(target);}
        target.Width=target.Height=2*radius;Canvas.SetLeft(target,(arena.ActualWidth-side)/2+exercise.X*side-radius);Canvas.SetTop(target,exercise.Y*side-radius);target.Visibility=exercise.Visible?Visibility.Visible:Visibility.Collapsed;
        if(exercise.Mode==AimMode.TargetSwitching)
        {if(secondTarget is null){secondTarget=new Ellipse{Stroke=Brush(100),StrokeThickness=2,IsHitTestVisible=false};arena.Children.Add(secondTarget);}secondTarget.Width=secondTarget.Height=radius*2;Canvas.SetLeft(secondTarget,(arena.ActualWidth-side)/2+exercise.SecondaryX*side-radius);Canvas.SetTop(secondTarget,exercise.SecondaryY*side-radius);}
    }
    private void FinishTraining(string reason)
    {
        aimTimer.Stop();trainingClock.Stop();if(exercise is not {} e)return;exercise=null;
        try
        {
            if(e.Elapsed>.1){var result=e.Result(trainingSensitivity,trainingDpi);Model.Store.Record("training",e.Mode.ToString(),new MouseTrainingRecord(result,e.Finished,reason));if(trainingText is not null)trainingText.Text=$"{reason}\nPrecisão {result.Accuracy:F1}% · {result.Hits} acertos · {result.Misses} falhas · resposta média observada {N(result.MeanResponseMs)} ms · tracking {result.TrackingPercent:F1}%";}
            Model.Status=reason;
        }catch(Exception ex){Model.Status="Não foi possível guardar o treino: "+ex.Message;}
        if(arena is not null){arena.Children.Clear();target=null;secondTarget=null;}
    }
    private void RenderTrainingHistory()
    {
        var entries=Model.Store.History("training");var completed=entries.Select(e=>(e.At,Record:JsonSerializer.Deserialize<MouseTrainingRecord>(e.Json)!)).Where(x=>x.Record.Completed).ToArray();
        var rows=completed.Take(8).Select(x=>$"{x.At.ToLocalTime():dd/MM HH:mm} · {x.Record.Result.Mode} · nível {x.Record.Result.Difficulty} · {x.Record.Result.Accuracy:F1}% · tracking {x.Record.Result.TrackingPercent:F1}%");
        body.Children.Add(Card("Evolução e recordes",Text(rows.Any()?string.Join("\n",rows):"Completa uma sessão de 30 segundos para iniciar a evolução."),
            Text(string.Join("\n",completed.GroupBy(x=>(x.Record.Result.Mode,x.Record.Result.Difficulty)).Select(g=>$"Recorde {g.Key.Mode} nível {g.Key.Difficulty}: {(g.Key.Mode==AimMode.Tracking?g.Max(x=>x.Record.Result.TrackingPercent):g.Max(x=>x.Record.Result.Accuracy)):F1}%")))));
        var week=completed.Where(e=>e.At>=DateTimeOffset.UtcNow.AddDays(-7)).GroupBy(e=>e.At.ToLocalTime().Date).OrderBy(g=>g.Key).Select(g=>$"{g.Key:dd/MM}: {g.Count()} treinos completos");
        body.Children.Add(Card("Últimos sete dias",Text(week.Any()?string.Join("\n",week):"Ainda sem treinos completos nesta semana.")));
    }
    private void Sensitivity()
    {
        var game=Choice("Jogo",new[]{"Counter-Strike 2","FiveM / GTA V"});var dpi=Number("DPI declarado",800,50,100000);var sens=Number("Sensibilidade do jogo",1,.0001,1000);var targetDpi=Number("DPI pretendido",1600,50,100000);
        var yaw=Number("Coeficiente angular conhecido (graus/contagem/sensibilidade)",double.NaN,.000001,360);var result=Text("Preenche valores que conheças. Não lemos as definições do jogo.");
        body.Children.Add(Card("Calculadora de sensibilidade",game,dpi,sens,targetDpi,Model.Advanced?yaw:Text("cm/360 por coeficiente angular está no modo avançado; a calibração manual está disponível abaixo."),
            Button("Calcular e guardar comparação",()=>
            {
                var edpi=MouseLabMath.Edpi(dpi.Value,sens.Value);var equivalent=MouseLabMath.ConvertDpi(dpi.Value,sens.Value,targetDpi.Value);
                double? cm=Model.Advanced && double.IsFinite(yaw.Value)?MouseLabMath.Cm360(dpi.Value,sens.Value,yaw.Value):null;
                result.Text=$"eDPI: {edpi:F2}\nNa mesma escala do mesmo jogo, a {targetDpi.Value:F0} DPI: {equivalent:F4}\ncm/360: {N(cm)}.\nNão é uma equivalência entre CS2 e FiveM; FOV, perspetiva, ADS e métodos de entrada podem diferir.";
                Model.Store.Record("sensitivity","Comparação de DPI",new{Game=game.SelectedItem,Dpi=dpi.Value,Sensitivity=sens.Value,TargetDpi=targetDpi.Value,Equivalent=equivalent,Cm360=cm});Model.Status="Cálculo guardado; nenhuma definição foi alterada.";return Task.CompletedTask;
            }),result));
        var distance=Number("Distância física medida (cm)",20,.01,1000);var angle=Number("Rotação observada no jogo (graus)",180,.01,36000);var calibration=Text("Mede manualmente no jogo, sem alterar ficheiros. Repete várias vezes e regista perspetiva e zoom.");
        body.Children.Add(Card("Calibração manual para CS2 e FiveM",distance,angle,Button("Calcular cm/360 medidos",()=>{var cm=MouseLabMath.Calibrate(distance.Value,angle.Value);calibration.Text=$"{cm:F2} cm/360 com estes dados declarados. Não foi medido automaticamente pela NEXOS.";Model.Store.Record("sensitivity","Calibração manual",new{Distance=distance.Value,Rotation=angle.Value,Cm360=cm});return Task.CompletedTask;}),calibration));
        body.Children.Add(Card("Ajuda rápida",Text("DPI: contagens por polegada no sensor. Sensibilidade: escala do jogo. eDPI: produto dos dois, comparável apenas na mesma escala. cm/360: distância física para uma volta. Polling: frequência de comunicação, independente destes cálculos.")));
    }
    private void Profiles()
    {
        var profiles=new ComboBox{Header="Perfil",ItemsSource=Model.Store.Profiles(),HorizontalAlignment=HorizontalAlignment.Stretch};
        var name=new TextBox{Header="Nome"};var game=Choice("Jogo",new[]{"CS2","FiveM","Other"});
        var dpi=Number("DPI pretendido (fabricante)",800,50,100000);var polling=Choice("Polling pretendido (fabricante)",MouseLabMath.PollingRates.Select(p=>p+" Hz"),3);
        var sens=Number("Sensibilidade declarada",1,.0001,1000);var speed=Number("Velocidade Windows",10,1,20);var accel=new CheckBox{Content="Desligar aceleração Windows"};
        var activation=Choice("Quando o jogo abrir",new[]{"Perguntar antes","Automático — apenas alterações Windows autorizadas","Não alterar"});var notes=new TextBox{Header="Preferências de sensor / notas",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxLength=1000};
        profiles.SelectionChanged+=(_,_)=>{if(profiles.SelectedItem is not MouseProfile p)return;name.Text=p.Name;game.SelectedItem=p.Game;dpi.Value=p.DesiredDpi;polling.SelectedIndex=MouseLabMath.PollingRates.ToList().IndexOf(p.DesiredPolling);sens.Value=p.Sensitivity;speed.Value=p.WindowsSpeed;accel.IsChecked=p.DisableAcceleration;activation.SelectedIndex=(int)p.Activation;notes.Text=p.SensorNotes;};profiles.SelectedIndex=0;
        MouseProfile Read(){if(dpi.Value!=Math.Truncate(dpi.Value)||speed.Value!=Math.Truncate(speed.Value))throw new ArgumentException("DPI e velocidade devem ser inteiros.");var p=new MouseProfile(name.Text.Trim(),(string)game.SelectedItem,(int)dpi.Value,MouseLabMath.PollingRates[polling.SelectedIndex],sens.Value,(int)speed.Value,accel.IsChecked==true,(MouseActivation)activation.SelectedIndex,notes.Text);p.Validate();return p;}
        var auto=new CheckBox{Content="Acompanhar jogos da biblioteca nesta execução da NEXOS (consulta a cada 5 s)",IsChecked=automationEnabled};
        auto.Click+=async(_,_)=>await Execute(async()=>
        {
            if(auto.IsChecked!=true){automationEnabled=false;automationTimer.Stop();Model.Status="Acompanhamento desligado. Uma sessão ativa continua a ser reposta ao fechar ou ao terminar o jogo.";if(profileSession.Active)automationTimer.Start();return;}
            var acceptedLibrary=library.Read().ToArray();var acceptedProfiles=Model.Store.Profiles().ToArray();
            var automaticSummary=string.Join("\n",acceptedProfiles.Where(p=>p.Activation==MouseActivation.Automatic).Select(p=>$"{p.Name} ({p.Game}): velocidade {p.WindowsSpeed}/20; desligar aceleração {p.DisableAcceleration}"));
            if(!await confirm("Autorizar acompanhamento de jogos?","Enquanto a NEXOS estiver aberta, consulta os processos dos jogos registados na tua biblioteca a cada 5 segundos. Perfis 'Perguntar' exigem confirmação. Automático aplica apenas os valores guardados abaixo, com restauro ao terminar. Não escreve DPI, sensor, ficheiros do jogo nem prioridade. A autorização não persiste após fechar.\n\nPerfis automáticos revistos:\n"+(automaticSummary.Length==0?"Nenhum; todos exigem confirmação ou estão desativados.":automaticSummary))){auto.IsChecked=false;return;}
            automationLibrary=acceptedLibrary;automationProfiles=acceptedProfiles;
            automationEnabled=true;lastOfferedProcess=null;automationTimer.Start();Model.Status="Acompanhamento autorizado nesta execução. Usa os perfis e jogos guardados no momento da autorização; alterações exigem reativar.";
        });
        body.Children.Add(Card("Gaming Profiles",profiles,name,game,dpi,polling,sens,speed,accel,activation,notes,
            Button("Guardar perfil",async()=>{var p=Read();if(p.Activation==MouseActivation.Automatic&&!await confirm("Guardar aplicação automática?",$"O perfil {p.Name} poderá alterar velocidade Windows para {p.WindowsSpeed}/20"+(p.DisableAcceleration?" e desligar aceleração":"")+" quando autorizares o acompanhamento. DPI/polling são apenas valores pretendidos. Restauro no fim da sessão."))return;Model.Store.Save(p);PauseAutomation();auto.IsChecked=false;Model.Status="Perfil guardado. Nenhuma configuração foi aplicada. Reativa o acompanhamento para usar novos perfis.";profiles.ItemsSource=Model.Store.Profiles();profiles.SelectedItem=Model.Store.Profiles().First(x=>x.Name==p.Name);}),
            Button("Aplicar apenas definições Windows deste perfil",async()=>{EnsureCanChange();var p=Read();var prepared=MouseProfileSession.Prepare(p,WindowsSettings.Resolve);if(!await confirm("Aplicar definições suportadas?",Describe(prepared)))return;EnsureCanChange();optimizer.ApplyBatch(prepared);Model.Status="Definições Windows verificadas. DPI, polling e sensor não foram alterados.";}),auto,
            Button("Terminar e restaurar sessão de rato",RestoreMouseAsync),Text("Automação usa a identidade e o caminho dos jogos da biblioteca existente. O perfil CS2/FiveM só é oferecido se o processo corresponder a uma instalação registada. Nenhuma alteração a ficheiros de jogo.")));
        BackupCard();
    }
    private void Health()
    {
        var d=devices.SelectedItem as MouseDevice;
        body.Children.Add(Card("Estado observado",Text(d is null?"Sem dispositivo selecionado.":$"Presente na última consulta: {d.Name}\n{d.Connection}\nBateria, firmware e desgaste: desconhecidos; não são estimados."),Button("Verificar ligação agora",DetectAsync),Button("Testar botões, roda e movimento",()=>Navigate(3))));
        var events=Model.Store.History("health");body.Children.Add(Card("Problemas observados",Text(events.Count==0?"Nenhuma desconexão observada entre consultas. Não existe vigilância permanente.":string.Join("\n",events.Take(15).Select(e=>$"{e.At.ToLocalTime():g} · {e.Label}")))));
        body.Children.Add(Card("Manutenção e ligação USB",Text("Experimenta uma porta direta e evita hubs sobrecarregados. Confirma cabo/recetor, distância e interferências. Limpa a superfície conforme o fabricante. A NEXOS mostra identificadores HID disponíveis; não confirma velocidade USB, qualidade de rádio nem desgaste físico."),OfficialLink()));
        if(Model.Advanced && d is not null)body.Children.Add(Card("Identificadores locais",Text($"VID: {d.VendorId??"desconhecido"} · PID: {d.ProductId??"desconhecido"}\nFornecedor: {d.Provider}\n{d.Id}")));
    }
    private void Advanced()
    {
        var priority=Choice("Prioridade",new[]{"Competitive FPS","Precision","Tracking","Balanced","Productivity"},3);var polling=Choice("Polling pretendido",MouseLabMath.PollingRates.Select(p=>p+" Hz"),3);var experience=Choice("Experiência",new[]{"Principiante","Intermédio","Experiente"});var preference=Choice("Preferência de sensibilidade",new[]{"Ainda a descobrir","Movimentos amplos","Movimentos curtos"});var result=Text("As recomendações são regras locais explicáveis; não enviam dados a uma IA externa.");
        body.Children.Add(Card("Smart Mouse Optimization",priority,polling,experience,preference,
            Button("Analisar dados disponíveis",()=>
            {var a=MousePrecision.ParseState(MousePrecision.Resolve().Read())[2]!=0;var rules=MouseRecommendations.Create((string)priority.SelectedItem=="Productivity"?"Productivity":"Gaming",Model.Analysis,a,MouseLabMath.PollingRates[polling.SelectedIndex]);result.Text=$"Experiência declarada: {experience.SelectedItem}; preferência: {preference.SelectedItem}. Não determinam uma sensibilidade ideal.\n\n"+string.Join("\n\n",rules.Select(r=>$"{r.Title}\nRazão: {r.Reason}\nBenefício possível: {r.Benefit}\nDesvantagem/limite: {r.Tradeoff}\nConfiança: {r.Confidence}"));return Task.CompletedTask;}),result,
            Button("Aplicar alterações suportadas — rever primeiro",()=>Navigate(1)),Button("Testar configuração",()=>Navigate(3)),Button("Restaurar definições anteriores",RestoreMouseAsync)));
        body.Children.Add(Card("Centro de ajuda",Text("Configuração: definições Windows têm restauro; hardware depende do fabricante. Análise: o teste mede entrega de eventos, incluindo influência da aplicação e do sistema. Treino: mede apenas este exercício. Importar perfis não os ativa. Capturas param ao sair/perder foco; automação só funciona com autorização nesta execução.")));
        BackupCard();
    }
    private void History()
    {
        var analyses=Model.Store.History("analysis");
        var before=new ComboBox{Header="Antes",ItemsSource=analyses.Select(e=>new EntryChoice(e)).ToArray(),HorizontalAlignment=HorizontalAlignment.Stretch};
        var after=new ComboBox{Header="Depois",ItemsSource=analyses.Select(e=>new EntryChoice(e)).ToArray(),HorizontalAlignment=HorizontalAlignment.Stretch};
        if(analyses.Count>0)after.SelectedIndex=0;if(analyses.Count>1)before.SelectedIndex=1;var result=Text("Escolhe dois testes do mesmo dispositivo e condições semelhantes. Não existe melhoria garantida.");
        body.Children.Add(Card("Before vs After",before,after,Button("Comparar medições",()=>
        {
            if(before.SelectedItem is not EntryChoice b||after.SelectedItem is not EntryChoice a||b.Entry.Id==a.Entry.Id)throw new ArgumentException("Seleciona dois testes diferentes.");
            var x=JsonSerializer.Deserialize<MouseAnalysisRecord>(b.Entry.Json)!;var y=JsonSerializer.Deserialize<MouseAnalysisRecord>(a.Entry.Json)!;
            result.Text=$"Antes: {x.DeviceName}\n{Format(x.Analysis)}\n\nDepois: {y.DeviceName}\n{Format(y.Analysis)}\n\nPodem variar com movimento, pausas, dispositivo, carga e duração. Não demonstra ganho de FPS ou menor latência física.";return Task.CompletedTask;
        }),result));
        var history=Model.Store.History();body.Children.Add(Card("Histórico local",Text(history.Count==0?"Ainda sem testes, treinos ou comparações.":string.Join("\n",history.Take(25).Select(e=>$"{e.At.ToLocalTime():dd/MM HH:mm} · {e.Kind} · {e.Label}"))),Button("Atualizar histórico",()=>{Render();return Task.CompletedTask;})));
        var changes=optimizer.History().Where(x=>x.Key is MousePrecision.SpeedKey or MousePrecision.AccelerationKey).Take(20);
        body.Children.Add(Card("Alterações Windows e recuperação",Text(profileSession.RecoveryRequired?"Há uma sessão interrompida por recuperar. Usa Restaurar.":profileSession.Active?"Sessão temporária ativa: "+profileSession.ActiveProfile:"Sem sessão temporária ativa."),Text(string.Join("\n",changes.Select(x=>$"{x.Name}: {x.Before} → {x.After} · {x.State}"))),Button("Restaurar alterações do rato",RestoreMouseAsync)));
        BackupCard();RenderTrainingHistory();
    }
    private void BackupCard()=>body.Children.Add(Card("Backups e relatórios",Button("Exportar perfis (JSON)",()=>SaveTextAsync("NEXOS-Mouse-Profiles",Model.Store.ExportProfiles())),Button("Importar perfis (não os aplica)",ImportAsync),Button("Exportar relatório local",()=>SaveTextAsync("NEXOS-Mouse-Report",JsonSerializer.Serialize(new{Schema=1,GeneratedAt=DateTimeOffset.UtcNow,Limitations=MouseAnalysis.Limitation,Events=Model.Store.History()},new JsonSerializerOptions{WriteIndented=true}))),Text("O backup contém preferências declaradas. Não importa o diário de alterações do Windows de outro PC nem aplica configurações automaticamente.")));
    private async Task SaveTextAsync(string name,string content)
    {
        var picker=new Microsoft.Windows.Storage.Pickers.FileSavePicker(owner.AppWindow.Id){SuggestedFileName=name};picker.FileTypeChoices.Add("JSON",new List<string>{".json"});var file=await picker.PickSaveFileAsync();if(file is null)return;await File.WriteAllTextAsync(file.Path,content);Model.Status="Ficheiro exportado: "+file.Path;
    }
    private async Task ImportAsync()
    {
        var picker=new Microsoft.Windows.Storage.Pickers.FileOpenPicker(owner.AppWindow.Id);picker.FileTypeFilter.Add(".json");var file=await picker.PickSingleFileAsync();if(file is null)return;
        if(new FileInfo(file.Path).Length>MouseLabStore.MaxImportBytes)throw new InvalidDataException("Backup superior a 256 KiB.");
        var json=await File.ReadAllTextAsync(file.Path);if(!await confirm("Importar perfis?","Perfis com o mesmo nome serão substituídos. Não aplica alterações ao Windows ou ao hardware. Os restantes perfis e o histórico ficam guardados."))return;
        Model.Store.ImportProfiles(json);PauseAutomation();Model.Status="Perfis importados e validados. Nenhuma definição foi aplicada. O acompanhamento exige nova autorização.";Render();
    }
    private void EnsureCanChange()
    {if(conflict()||profileSession.Active)throw new InvalidOperationException("Termina e repõe a sessão ou operação ativa antes de aplicar definições permanentes.");}
    private static string Describe(IReadOnlyList<PreparedAdjustment> prepared)=>string.Join("\n",prepared.Select(x=>$"{x.Key}: {x.Expected} → {x.Value}"))+"\nO valor anterior é guardado antes da escrita. O Windows confirma o resultado. Jogos com Raw Input podem ignorar estas opções. DPI/polling/sensor não são alterados.";
    private async Task RestoreMouseAsync()
    {
        if(conflict())throw new InvalidOperationException("Termina primeiro a operação ou sessão de jogo ativa.");
        if(!await confirm("Restaurar definições do rato?","Repõe alterações do rato guardadas no diário deste PC, respeitando alterações externas. Não importa valores de outros computadores."))return;
        var session=profileSession.End();if(!session.Complete)throw new InvalidOperationException(string.Join("\n",session.Errors));
        var restored=optimizer.RestoreAll(x=>x.Key is MousePrecision.SpeedKey or MousePrecision.AccelerationKey);
        if(!restored.Complete)throw new InvalidOperationException(string.Join("\n",restored.Errors));Model.Status=$"Restauro confirmado: {restored.Restored+session.Restored} alterações.";
    }
    private async Task PollAutomationAsync()
    {
        if(disposed||automationBusy||busy)return;automationBusy=true;
        try
        {
            profileSession.Poll();
            if(!automationEnabled){if(!profileSession.Active)automationTimer.Stop();return;}
            if(conflict()||profileSession.Active)return;
            var detected=await Task.Run(()=>GameLibrary.Detect(automationLibrary));if(disposed||busy||conflict())return;
            var game=detected.FirstOrDefault(d=>d.App.Name.Contains("FiveM",StringComparison.OrdinalIgnoreCase)||d.App.Name.Contains("Counter-Strike",StringComparison.OrdinalIgnoreCase)||string.Equals(System.IO.Path.GetFileName(d.ActualPath),"cs2.exe",StringComparison.OrdinalIgnoreCase));
            if(game is null){lastOfferedProcess=null;return;}if(lastOfferedProcess==game.ProcessKey)return;
            var isFiveM=game.App.Name.Contains("FiveM",StringComparison.OrdinalIgnoreCase);var profile=automationProfiles.FirstOrDefault(p=>p.Game==(isFiveM?"FiveM":"CS2") && p.Activation!=MouseActivation.Never);if(profile is null)return;
            if(profile.Activation==MouseActivation.Ask&&!active){Model.Status="Perfil disponível para "+game.App.Name+". Abre Gaming Profiles para o rever.";return;}
            lastOfferedProcess=game.ProcessKey;
            if(profile.Activation==MouseActivation.Ask&&!await confirm("Ativar perfil de rato para "+game.App.Name+"?",Describe(MouseProfileSession.Prepare(profile,WindowsSettings.Resolve))+"\nRestauro ao terminar o jogo ou fechar a NEXOS."))return;
            if(disposed||!automationEnabled||busy||conflict())return;
            profileSession.Start(profile,game.ProcessKey);Model.Status="Perfil Windows temporário ativo: "+profile.Name+". DPI/sensor configuram-se no fabricante.";
        }
        catch(Exception e){automationEnabled=false;Model.Status="Automação interrompida: "+e.Message+". Verifica o histórico e restaura a sessão.";if(!profileSession.Active)automationTimer.Stop();}
        finally{automationBusy=false;}
    }
    public void PrepareClose()
    {
        if(busy||automationBusy)throw new InvalidOperationException("Espera que a operação do Mouse Lab termine.");automationEnabled=false;automationTimer.Stop();LoseFocus();
        if(profileSession.RecoveryRequired)throw new InvalidOperationException("Há uma sessão de rato anterior por recuperar no histórico do Mouse Lab.");
        var restored=profileSession.End();if(!restored.Complete)throw new InvalidOperationException(string.Join("\n",restored.Errors));
    }
    private void PauseAutomation(){automationEnabled=false;automationLibrary=[];automationProfiles=[];if(!profileSession.Active)automationTimer.Stop();}
    public void Dispose(){if(disposed)return;Deactivate();captureTimer.Stop();aimTimer.Stop();automationTimer.Stop();disposed=true;}
    public sealed record MouseAnalysisRecord(MouseAnalysis Analysis,string StopReason,string DeviceName);
    public sealed record MouseTrainingRecord(AimResult Result,bool Completed,string StopReason);
    private sealed record EntryChoice(MouseLabEntry Entry){public override string ToString()=>$"{Entry.At.ToLocalTime():dd/MM HH:mm:ss} · {Entry.Label}";}
}
