namespace Nexus.Optimization;

public sealed record AdjustmentState(string Key, string Desired, string? Current,
    OptimizationRecord? OwnedChange, string? Error)
{
    public bool IsOn => Error is null && Current == Desired;
    public bool CanRestore => OwnedChange is not null && Error is null && Current == OwnedChange.After;
    public string Label => Error is not null ? "Indisponível" : CanRestore ? "Aplicado pela NEXUS" : IsOn ? "Já configurado" : "Desligado";
}

public static class AdjustmentStates
{
    public static AdjustmentState Read(SystemOptimizer optimizer, string key, string desired,
        Func<string, ISystemSetting>? resolve = null, string? unavailable = null)
    {
        if (unavailable is not null) return new(key, desired, null, null, unavailable);
        try
        {
            return optimizer.ExecuteExclusive(() =>
            {
                var latest = optimizer.History().FirstOrDefault(x => x.Key == key && x.State is "applied" or "pending");
                var owned = latest?.Owner is null ? latest : null;
                return new AdjustmentState(key, desired, (resolve ?? WindowsSettings.Resolve)(key).Read(), owned, null);
            });
        }
        catch (Exception error) { return new(key, desired, null, null, error.Message); }
    }
    public static void Set(SystemOptimizer optimizer, AdjustmentState observed, bool enabled,
        Func<string, ISystemSetting>? resolve = null)
    {
        optimizer.ExecuteExclusive(() =>
        {
            if (observed.Error is not null) throw new InvalidOperationException(observed.Error);
            var latest = Read(optimizer, observed.Key, observed.Desired, resolve);
            if (latest.Current != observed.Current || latest.OwnedChange?.Id != observed.OwnedChange?.Id)
                throw new InvalidOperationException("O estado mudou. Atualiza a página antes de aplicar.");
            if (enabled) optimizer.Apply(observed.Key, observed.Desired);
            else if (latest.CanRestore) optimizer.Undo(latest.OwnedChange!.Id);
            else throw new InvalidOperationException("A NEXUS não tem um valor anterior para repor esta opção. Não foi aplicado um padrão inventado.");
            return true;
        });
    }
}
