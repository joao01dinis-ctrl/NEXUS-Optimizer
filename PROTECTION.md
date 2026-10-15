# Proteção com Microsoft Defender — NEXUS 0.10

Pedido do utilizador em 15/10/2026: manter o otimizador e integrar proteção com Microsoft Defender. Esta área não é um motor antivírus próprio e não regista a NEXUS como fornecedor de antivírus.

## Funcionamento

- Consulta o estado agregado do antivírus através de WscGetSecurityProviderHealth e, separadamente, o estado do Defender por Get-MpComputerStatus.
- Mostra os dados disponíveis de proteção em tempo real, proteção contra alterações, versão/data das definições e últimas análises terminadas. Campos em falta ficam indisponíveis. As datas não provam ausência de malware.
- Com Defender confirmado como ativo, direitos de administrador e confirmação individual, permite pedir Update-MpSignature, Start-MpScan QuickScan ou FullScan. O fornecedor e os direitos são verificados novamente no momento do pedido.
- Abre a Segurança do Windows para utilizar o antivírus sem direitos nesta NEXUS e consultar deteções/quarentena. A página de definições pode exigir mais um clique em Abrir Segurança do Windows.
- Guarda no histórico local apenas a operação solicitada e o resultado do pedido, sem caminhos de ameaças, amostras ou conteúdo de ficheiros.

A consulta não pede elevação. Sem direitos, os botões dos cmdlets estão desativados e a Segurança do Windows continua acessível. Políticas podem recusar operações mesmo com direitos. Outro fornecedor ou o modo passivo não autoriza forçar o Defender a ficar ativo.

## Limites

As análises podem consumir CPU/disco e colocar ameaças em quarentena conforme as políticas do Defender. Não integram o Undo do otimizador. Uma análise já iniciada pode continuar se a NEXUS fechar ou deixar de a acompanhar. Um pedido terminado sem erro não significa que o PC esteja limpo. Não são inventadas percentagens de progresso ou resultados de deteção.

Não há comandos para desligar antivírus, firewall, Smart App Control, proteção contra alterações ou criar exclusões. Otimização, Game Boost e OpenAI não iniciam análises, alteram estes controlos ou recebem dados de segurança. Não há eliminação/restauro de ameaças pela NEXUS, upload de amostras, motor de IA para classificar malware ou uma segunda proteção em tempo real.

## Validação

Build local sem avisos nem erros; suite local com 225 aprovados, 5 ignorados e zero falhas. Os testes de autorização, revalidação do fornecedor, concorrência e interpretação de estados/datas usam um backend de teste e não executam análises no PC. O modo de captura da página apenas consulta dados do Windows e verifica zero pedidos. Deteção e quarentena não são ensaiadas nos dados pessoais do utilizador.

O arranque da publicação 0.10 neste portátil em 15/10/2026 foi bloqueado pelo App Control ao carregar Nexus.Restore.dll sem assinatura (0x800711C7). Nenhuma política foi alterada. A página precisa de validação nativa no Windows de testes e de uma distribuição aceite neste PC para utilização local. A 0.9 tem arranque local confirmado e permanece no iniciador principal. São versões diferentes.

A revisão base 0.9 passou 201 testes e quatro testes nativos no [Windows descartável](https://github.com/joao01dinis-ctrl/NEXUS-Optimizer/actions/runs/38797601374), com doze capturas reais da interface. O primeiro ensaio Máximo tinha os cinco valores já em zero; nesta revisão o teste prepara cinco valores opostos antes da sessão para exigir alterações e reposição reais.

## Fontes oficiais

- [Estado do Defender](https://learn.microsoft.com/en-us/powershell/module/defender/get-mpcomputerstatus)
- [Iniciar análises](https://learn.microsoft.com/en-us/powershell/module/defender/start-mpscan)
- [Atualizar definições](https://learn.microsoft.com/en-us/powershell/module/defender/update-mpsignature)
- [Estado agregado do Windows](https://learn.microsoft.com/en-us/windows/win32/api/wscapi/nf-wscapi-wscgetsecurityproviderhealth)
- [Compatibilidade com outros antivírus](https://learn.microsoft.com/en-us/defender-endpoint/microsoft-defender-antivirus-compatibility)

Usar a publicação completa em artifacts/app-v0.10-protection ou Start-Nexus-0.10.cmd. A distribuição continua experimental e sem assinatura, independente da submissão Store 0.7.
