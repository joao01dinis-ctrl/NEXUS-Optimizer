using Microsoft.Data.Sqlite;
using Nexus.Core;

namespace Nexus.Optimization;

// Uses the same cross-window journal mutex and durable setting ownership as existing optimizations.
public sealed class MouseProfileSession
{
    private readonly SystemOptimizer optimizer;
    private readonly Func<string,ISystemSetting> resolve;
    private readonly string connectionString;
    private string? ownedId;
    public MouseProfileSession(SystemOptimizer optimizer,Func<string,ISystemSetting>? resolve=null)
    {
        this.optimizer=optimizer;this.resolve=resolve??WindowsSettings.Resolve;
        connectionString=new SqliteConnectionStringBuilder{DataSource=optimizer.DatabasePath}.ToString();
        optimizer.ExecuteExclusive(()=>{using var c=Open();using var q=c.CreateCommand();q.CommandText="CREATE TABLE IF NOT EXISTS mouse_lab_session(id TEXT PRIMARY KEY, process_key TEXT NOT NULL, profile TEXT NOT NULL, ended INTEGER NOT NULL DEFAULT 0)";q.ExecuteNonQuery();return true;});
    }
    private SqliteConnection Open(){var c=new SqliteConnection(connectionString);c.Open();return c;}
    private (string Id,string Process,string Profile)? Read()
    {using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT id,process_key,profile FROM mouse_lab_session WHERE ended=0 ORDER BY rowid DESC LIMIT 1";using var r=q.ExecuteReader();return r.Read()?(r.GetString(0),r.GetString(1),r.GetString(2)):null;}
    public bool Active=>optimizer.ExecuteExclusive(()=>Read() is not null);
    public bool RecoveryRequired=>optimizer.ExecuteExclusive(()=>Read() is {} s && s.Id!=ownedId);
    public string? ActiveProfile=>optimizer.ExecuteExclusive(()=>Read()?.Profile);
    public void Start(MouseProfile profile,string processKey)
    {
        profile.Validate();
        if(!processKey.StartsWith("priority:",StringComparison.Ordinal))throw new ArgumentException("Identidade de processo inválida.");
        optimizer.ExecuteExclusive(()=>
        {
            if(Read() is not null)throw new InvalidOperationException("Repõe primeiro a sessão de rato anterior.");
            if(resolve(processKey).Read()=="ended")throw new InvalidOperationException("O jogo já terminou.");
            var changes=Prepare(profile,resolve);
            var id="mouse-"+Guid.NewGuid().ToString("N");
            using(var c=Open())using(var q=c.CreateCommand())
            {q.CommandText="INSERT INTO mouse_lab_session(id,process_key,profile) VALUES($i,$p,$n)";q.Parameters.AddWithValue("$i",id);q.Parameters.AddWithValue("$p",processKey);q.Parameters.AddWithValue("$n",profile.Name);q.ExecuteNonQuery();}
            ownedId=id;
            try{foreach(var item in changes){if(resolve(item.Key).Read()!=item.Expected)throw new InvalidOperationException("As definições mudaram durante a aplicação.");optimizer.ApplyOwned(id,item.Key,item.Value);}}
            catch{End();throw;}return true;
        });
    }
    public static IReadOnlyList<PreparedAdjustment> Prepare(MouseProfile profile,Func<string,ISystemSetting> resolve)
    {
        profile.Validate();
        var changes=new List<PreparedAdjustment>{new(MousePrecision.SpeedKey,profile.WindowsSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture),resolve(MousePrecision.SpeedKey).Read())};
        // False means leave acceleration as-is. Never invent previous acceleration thresholds to enable it.
        if(profile.DisableAcceleration)changes.Add(new(MousePrecision.AccelerationKey,"0,0,0",resolve(MousePrecision.AccelerationKey).Read()));
        return changes;
    }
    public RestoreResult End()=>optimizer.ExecuteExclusive(()=>
    {
        var session=Read();if(session is null)return new RestoreResult(0,[]);
        var result=optimizer.RestoreAll(x=>x.Owner==session.Value.Id);
        if(result.Complete)
        {using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE mouse_lab_session SET ended=1 WHERE id=$i";q.Parameters.AddWithValue("$i",session.Value.Id);q.ExecuteNonQuery();ownedId=null;}
        return result;
    });
    public void Poll()=>optimizer.ExecuteExclusive(()=>
    {var session=Read();if(session is {} s && s.Id==ownedId && resolve(s.Process).Read()=="ended")End();return true;});
}
