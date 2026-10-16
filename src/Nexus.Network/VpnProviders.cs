namespace Nexus.Network;

public enum WarpTrafficMode { Unknown, TrafficAndDns, TrafficOnly, DnsOnly, LocalProxy }

public sealed record WarpObservation(bool Installed, bool Trusted, bool CanOpenClient, bool CompatibleCli,
    VpnConnectionState State, WarpTrafficMode Mode, bool OrganizationManaged = false, bool SetupRequired = false,
    string? Version = null, string? Error = null)
{
    public bool FullTunnelMode => Mode is WarpTrafficMode.TrafficAndDns or WarpTrafficMode.TrafficOnly;
}

public sealed record VpnStatus(string ProviderId, string ProviderName, DateTimeOffset CheckedAt, VpnConnectionState State,
    bool Installed, bool Trusted, bool CanOpenClient, bool CanConnect, bool CanDisconnect, WarpTrafficMode Mode,
    string Summary, string? Version = null)
{
    public bool ReportsTunnel => State == VpnConnectionState.Connected && Trusted && Mode is WarpTrafficMode.TrafficAndDns or WarpTrafficMode.TrafficOnly;
}

public interface IVpnProvider
{
    string Id { get; }
    Task<VpnStatus> ReadAsync(CancellationToken cancellation = default);
    Task<VpnStatus> RequestConnectionAsync(bool connect, bool confirmed, CancellationToken cancellation = default);
}

public interface ICloudflareWarpClient
{
    Task<WarpObservation> InspectAsync(CancellationToken cancellation);
    Task RequestAsync(bool connect, CancellationToken cancellation);
}

public sealed class CloudflareWarpProvider(ICloudflareWarpClient client, Func<CancellationToken, Task>? pause = null) : IVpnProvider
{
    private readonly SemaphoreSlim request = new(1, 1);
    public string Id => "cloudflare-warp";
    public async Task<VpnStatus> ReadAsync(CancellationToken cancellation = default)
    {
        var current = await client.InspectAsync(cancellation);
        var compatible = current.Installed && current.Trusted && current.CompatibleCli && !current.OrganizationManaged && !current.SetupRequired;
        var canConnect = compatible && current.FullTunnelMode && current.State is VpnConnectionState.Disconnected;
        // Recovery may still offer a confirmed disconnect after a failed connection.
        var canDisconnect = compatible && current.FullTunnelMode && current.State is VpnConnectionState.Connected or VpnConnectionState.Connecting or VpnConnectionState.Error;
        var summary = !current.Installed ? "O Cloudflare WARP ainda não está instalado neste PC." :
            !current.Trusted ? "Não foi possível validar a instalação como cliente oficial Cloudflare. Não será executada." :
            !current.CompatibleCli ? "Cliente detetado. Usa a aplicação oficial; os controlos diretos não são compatíveis com esta instalação." :
            current.SetupRequired ? "Conclui a configuração e aceita os termos na aplicação oficial Cloudflare WARP." :
            current.OrganizationManaged ? "Foi identificada uma configuração de organização. Os controlos ficam na aplicação oficial." :
            current.Mode == WarpTrafficMode.DnsOnly ? "O WARP está configurado apenas para DNS. Este modo não é uma VPN de tráfego." :
            current.Mode == WarpTrafficMode.LocalProxy ? "Modo proxy: só as aplicações configuradas usam o WARP. Usa a aplicação oficial." :
            current.Mode == WarpTrafficMode.Unknown ? "Não foi possível confirmar o modo. A NEXUS não assume que existe um túnel." :
            current.State switch
            {
                VpnConnectionState.Connected => "O cliente comunica ligação estabelecida. Verifica a passagem de tráfego pelo botão abaixo.",
                VpnConnectionState.Disconnected => "O cliente comunica WARP desligado.",
                VpnConnectionState.Connecting => "O cliente comunica que está a ligar.",
                VpnConnectionState.Disconnecting => "O cliente comunica que está a desligar.",
                VpnConnectionState.Error => "O WARP comunica um erro. Revê o estado ou abre a aplicação oficial.",
                _ => "Estado não reconhecido. Consulta a aplicação oficial; a proteção não foi confirmada."
            };
        if (current.Error is not null) summary += " " + current.Error;
        return new(Id, "Cloudflare WARP · gratuito", DateTimeOffset.UtcNow, current.State, current.Installed, current.Trusted,
            current.CanOpenClient && current.Trusted, canConnect, canDisconnect, current.Mode, summary, current.Version);
    }

    public async Task<VpnStatus> RequestConnectionAsync(bool connect, bool confirmed, CancellationToken cancellation = default)
    {
        if (!confirmed) throw new InvalidOperationException("Confirma a alteração da ligação antes de continuar.");
        if (!await request.WaitAsync(0, cancellation)) throw new InvalidOperationException("Já existe um pedido ao WARP em curso.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(35));
        try
        {
            var before = await ReadAsync(deadline.Token);
            if (!(connect ? before.CanConnect : before.CanDisconnect))
                throw new InvalidOperationException("O estado ou o modo do WARP não permite este pedido. Atualiza o estado ou usa a aplicação oficial.");
            await client.RequestAsync(connect, deadline.Token);
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var observed = await ReadAsync(deadline.Token);
                if (connect && observed.State == VpnConnectionState.Connected && observed.ReportsTunnel ||
                    !connect && observed.State == VpnConnectionState.Disconnected) return observed;
                if (observed.State is VpnConnectionState.Error or VpnConnectionState.Unavailable || !observed.Trusted)
                    throw new InvalidOperationException(observed.Summary);
                await (pause?.Invoke(deadline.Token) ?? Task.Delay(1500, deadline.Token));
            }
            throw new InvalidOperationException("O WARP não confirmou o estado pedido a tempo. O pedido pode continuar no cliente oficial; atualiza o estado antes de tentar novamente.");
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new InvalidOperationException("Terminou a espera pelo WARP. O pedido pode continuar na aplicação oficial. Não foram repostas definições de rede à força."); }
        finally { request.Release(); }
    }
}

public sealed class NexosWireGuardProvider : IVpnProvider
{
    public string Id => "nexos-wireguard";
    public Task<VpnStatus> ReadAsync(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        return Task.FromResult(new VpnStatus(Id, "Nexos WireGuard · futuro", DateTimeOffset.UtcNow, VpnConnectionState.Unavailable,
            false, false, false, false, false, WarpTrafficMode.Unknown,
            "Sem servidores Nexos configurados, componentes assinados ou registo autenticado. Este fornecedor não liga nem protege tráfego."));
    }
    public Task<VpnStatus> RequestConnectionAsync(bool connect, bool confirmed, CancellationToken cancellation = default) =>
        throw new InvalidOperationException("A VPN própria WireGuard ainda não está disponível.");
}
