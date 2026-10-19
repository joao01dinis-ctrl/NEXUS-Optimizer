namespace Nexus.Core;

public enum AimMode { Flick, Tracking, Microadjustment, Reaction, TargetSwitching }
public sealed record AimResult(AimMode Mode, int Difficulty, double DurationSeconds, int Hits, int Misses,
    double? MeanResponseMs, double TrackingPercent, double Sensitivity, int DeclaredDpi)
{
    public double Accuracy => Hits + Misses == 0 ? 0 : 100.0 * Hits / (Hits + Misses);
    public void Validate()
    {
        if (!Enum.IsDefined(Mode) || Difficulty is < 1 or > 3 || !double.IsFinite(DurationSeconds) || DurationSeconds is <= 0 or > 120 ||
            Hits is < 0 or > 100000 || Misses is < 0 or > 100000 || MeanResponseMs is < 0 || (MeanResponseMs is double m && !double.IsFinite(m)) ||
            !double.IsFinite(TrackingPercent) || TrackingPercent is < 0 or > 100) throw new ArgumentException("Sessão de treino inválida.");
        MouseLabMath.Edpi(DeclaredDpi, Sensitivity);
    }
}
public sealed class AimExercise
{
    private readonly Random random;
    private double lastTime, targetAt, nextVisible, trackingTime, totalTime;
    private readonly List<double> responses = [];
    private int hits, misses;
    private double? pointerX, pointerY;
    public AimMode Mode { get; }
    public int Difficulty { get; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public double SecondaryX => 1-X;
    public double SecondaryY => 1-Y;
    public double Radius => Mode == AimMode.Microadjustment ? .018 / Difficulty : .05 / Difficulty;
    public bool Visible { get; private set; }
    public bool Finished { get; private set; }
    public double Elapsed => lastTime;
    public int Hits => hits;
    public int Misses => misses;
    public AimExercise(AimMode mode, int difficulty, Random? random = null)
    {
        if (!Enum.IsDefined(mode) || difficulty is < 1 or > 3) throw new ArgumentException("Exercício inválido.");
        Mode=mode; Difficulty=difficulty; this.random=random ?? Random.Shared;
        NextTarget(0);
    }
    private void NextTarget(double now)
    {
        if (Mode == AimMode.Microadjustment) { X=.5 + (random.NextDouble()-.5)*.18; Y=.5 + (random.NextDouble()-.5)*.18; }
        else { X=.12+random.NextDouble()*.76; Y=.15+random.NextDouble()*.7; }
        nextVisible = Mode == AimMode.Reaction ? now + .7+random.NextDouble()*1.8 : now;
        targetAt=nextVisible; Visible=nextVisible<=now;
    }
    public void Move(double? x, double? y) { pointerX=x; pointerY=y; }
    public void Tick(double seconds)
    {
        if (Finished) return;
        if (!double.IsFinite(seconds) || seconds<lastTime) throw new ArgumentException("Tempo inválido.");
        var now=Math.Min(30,seconds); var delta=now-lastTime;
        if (Mode==AimMode.Tracking)
        {
            // Large dispatcher stalls are not credited as tracking time.
            if (delta <= .1 && delta>0) { totalTime+=delta; if (pointerX is double px && pointerY is double py && Inside(px,py,X,Y)) trackingTime+=delta; }
            X=.5+.32*Math.Sin(now*(.7+.25*Difficulty)); Y=.5+.26*Math.Sin(now*(1+.18*Difficulty));
        }
        lastTime=now;
        if(!Visible && now>=nextVisible){Visible=true;targetAt=now;}
        Finished=seconds>=30;
    }
    private bool Inside(double px,double py,double x,double y) => Math.Pow(px-x,2)+Math.Pow(py-y,2)<=Radius*Radius;
    public bool Click(double x,double y,double seconds)
    {
        Tick(seconds);
        if (Finished || Mode==AimMode.Tracking) return false;
        if (Visible && Inside(x,y,X,Y))
        {
            hits++; responses.Add(Math.Max(0,(seconds-targetAt)*1000));
            if(Mode==AimMode.TargetSwitching){X=1-X;Y=1-Y;targetAt=seconds;}
            else NextTarget(seconds);
            return true;
        }
        misses++; return false;
    }
    public AimResult Result(double sensitivity,int declaredDpi)
    {
        if (lastTime<=0) throw new InvalidOperationException("O exercício ainda não tem duração.");
        var result=new AimResult(Mode,Difficulty,lastTime,hits,misses,responses.Count==0?null:responses.Average(),
            totalTime==0?0:100*trackingTime/totalTime,sensitivity,declaredDpi); result.Validate(); return result;
    }
}
