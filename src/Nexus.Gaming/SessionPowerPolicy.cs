namespace Nexus.Gaming;

public static class SessionPowerPolicy
{
    public static bool CanChangePower(bool? onAc, bool? saver) => onAc == true && saver == false;
    public static void RequireAc(bool? onAc, bool? saver)
    {
        if (!CanChangePower(onAc, saver)) throw new InvalidOperationException("Para testar energia por sessão, liga o PC à corrente, desliga a poupança de bateria e atualiza a seleção. Podes acompanhar o jogo mantendo o plano atual.");
    }
}
