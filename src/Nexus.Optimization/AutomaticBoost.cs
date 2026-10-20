namespace Nexus.Optimization;

public sealed record AutomaticBoostPlan(DateTimeOffset AnalyzedAt, IReadOnlyList<PreparedAdjustment> Changes)
{
    public int Apply(SystemOptimizer optimizer, DateTimeOffset now)
    {
        if (now < AnalyzedAt || now - AnalyzedAt > TimeSpan.FromMinutes(5))
            throw new InvalidOperationException("A análise expirou. Analisa novamente antes de otimizar.");
        if (Changes.Any(c => !SmartBoost.VisualKeys.Contains(c.Key) || c.Value != "0"))
            throw new InvalidOperationException("O plano automático contém um ajuste não permitido.");
        return optimizer.ApplyBatch(Changes);
    }
}

// Model output cannot add permissions. Recompute the rules from the observations,
// then allow only the five reversible visual preferences with readable state.
public static class AutomaticBoost
{
    public static AutomaticBoostPlan RespectUserChanges(AutomaticBoostPlan plan, IReadOnlyList<OptimizationRecord> history, IReadOnlyList<SettingDrift> drift)
    {
        var protectedKeys = history.DistinctBy(x => x.Key).Where(x => x.State == "reverted").Select(x => x.Key)
            .Concat(drift.Select(x => x.Record.Key)).ToHashSet(StringComparer.Ordinal);
        return plan with { Changes = Array.AsReadOnly(plan.Changes.Where(x => !protectedKeys.Contains(x.Key)).ToArray()) };
    }
    public static AutomaticBoostPlan Prepare(BoostReport report, DateTimeOffset now)
    {
        if (!SmartBoost.IsFresh(report, now)) throw new InvalidOperationException("Analisa primeiro o PC.");
        var verified = SmartBoost.Evaluate(report.Input);
        if (verified.At != report.At) throw new InvalidOperationException("A origem das amostras mudou. Repete a análise.");
        var changes = verified.Rules.Where(r => r.Decision == BoostDecision.Apply && r.SettingKey is not null)
            .Select(r => report.Input.Settings.Single(s => s.Key == r.SettingKey))
            .Where(s => SmartBoost.VisualKeys.Contains(s.Key) && s.Desired == "0" && s.Current is not null && s.Error is null)
            .Select(s => new PreparedAdjustment(s.Key, s.Desired, s.Current!)).ToArray();
        return new(report.At, Array.AsReadOnly(changes));
    }
}
