# NEXOS Mouse Lab 1.0 — implementação

Integração na NEXUS Optimizer existente (.NET 10, C# 14, WinUI 3). A identidade de produção `Zokamg.NEXUSOptimizer` não muda. Versão de destino 1.0.0 / pacote 1.0.0.0, posterior à 0.10.

## Plano e limites

1. Core: contratos de dispositivos, perfis, validação, cálculos, métricas, recomendações locais e exercícios. Hardware: enumeração Raw Input e identificação HID apenas em leitura. Storage: tabelas SQLite adicionais, sem substituir as tabelas 0.10. UI: página integrada com dez separadores, modo simples/avançado.
2. Captura explícita e limitada de eventos Raw Input por dispositivo, apenas com a janela em primeiro plano. Intervalos são tempos de entrega observados no software, nunca latência física. Não existe captura durante jogos externos.
3. Treino local com cinco exercícios, tempos monotónicos, estatísticas persistidas e evolução. Nenhuma interação com a mira de jogos.
4. Perfis e recomendações explicáveis. Só as definições Windows suportadas podem ser aplicadas através do diário existente. Automação opt-in, identificação de processo e restauro de sessões; falhas ficam recuperáveis.
5. Integrações de fabricante só com interface documentada. DPI/polling/sensor/bateria/firmware indisponíveis permanecem desconhecidos; valores pretendidos ou declarados não são leituras.
6. Build e testes de regressão, testes nativos quando viáveis, navegação, migração de dados e pacote. Só depois: revisão GitHub, versão distribuível e substituição da certificação 0.10, caso o acesso à Store esteja disponível.

## Attack Shark R5 Ultra

A [página oficial](https://attackshark.com/products/attack-shark-r5ultra-carbon-fibre-wireless-8k-paw3950max-gaming-mouse) anuncia PAW3950MAX, até 42000 DPI, até 8000 Hz e ligação USB/2.4 GHz/Bluetooth. São especificações do fabricante, não leituras do dispositivo ligado. A página fornece software e Web Driver. Ainda não foi validada uma API pública de configuração para a NEXUS: não enviar relatórios HID de configuração nem alterar firmware por protocolos deduzidos.

## Medição e privacidade

Eventos de movimento, botões e roda são recolhidos apenas depois de iniciar um teste, até parar, mudar de página, perder foco, fechar, atingir tempo/limite de amostras ou ocorrer erro. Resumos ficam locais em SQLite. Importações não aplicam definições. Não exportar identificadores completos do dispositivo por predefinição. Não executar serviços, drivers, ligações cloud ou alterações à segurança para este módulo.

CS2 e FiveM têm perfis distintos. eDPI só é comparável dentro da mesma escala; conversão entre DPI mantém o produto DPI × sensibilidade. cm/360 exige um coeficiente angular conhecido ou calibração manual. FiveM/GTA V não recebe uma equivalência exata inventada.

## Estado das funcionalidades

| Função | Estado e evidência |
|---|---|
| Dez áreas, modo simples/avançado e integração na navegação/pesquisa | Implementada. Renderizações Windows da primeira revisão passaram; a revisão compacta e o teste de ciclo Raw Input estão em validação. |
| Enumeração HID, nome/VID/PID e capacidades | Implementada e testada em leitura no Windows local. Identificação genérica e recetores podem não revelar o modelo. |
| Velocidade/aceleração Windows e restauro | Implementada; executor existente tem round trips nativos em CI. Testes de sessão verificam rollback, conflito externo, processo terminado e recuperação após reinício. |
| DPI/polling pretendidos e perfis por jogo | Implementada e testada com SQLite, associação explícita por jogo, backup/importação e modo Nunca. Não configura hardware. |
| Teste Raw Input de polling/movimento/cliques/roda e continuidade | Implementada; métricas verificadas com séries de teste, incluindo 120000 eventos/8000 eventos por segundo, limites de memória e tempo. Ciclo de registo/remoção tem teste nativo reservado ao Windows descartável. Falta validação física no Attack Shark. |
| Aceleração | Leitura e alteração Windows suportadas. A curva física/latência do ponteiro não é medida; Raw Input ignora a aceleração Windows. |
| Cinco exercícios e estatísticas de treino | Implementados e testados nos motores de exercício. Sessões completas/parciais, precisão, resposta observada, tracking, DPI/sensibilidade declarados, recordes e evolução ficam locais. Navegação/temporizadores continuam sujeitos à validação nativa e utilização manual. |
| eDPI, conversão entre DPI, cm/360 e calibração manual | Implementadas e testadas. CS2/FiveM têm perfis; não existe conversão exata automática validada entre os dois. Coeficientes angulares e distâncias são fornecidos pelo utilizador. |
| Recomendações e guia de calibração | Implementados com regras locais explicáveis, prioridade de utilização e dados disponíveis. Não usa IA cloud nem determina uma sensibilidade ideal. |
| Perfis automáticos por jogo | Implementados; autorização por execução, snapshot da biblioteca/perfis, um perfil escolhido por jogo, identidade PID/arranque e restauro com diário. Testes de regras/sessões passaram; falta validar durante um jogo real no PC gaming. |
| Health, diagnóstico USB básico e notificações | Presença/ausência entre consultas, identificadores HID, desconexão durante captura, botões e roda. Não mede velocidade USB, qualidade de rádio ou desgaste físico. |
| Histórico, comparação de medições/treinos, relatórios e backups | Implementados. Importação validada e atómica, limitada a 256 KiB; não aplica definições ou importa o diário de outro PC. Compara treinos completos do mesmo modo/dificuldade. |
| DPI/polling configurados, bateria, firmware e sensor | Dependentes de compatibilidade. Fornecedor genérico mantém valores desconhecidos; especificações do R5 Ultra são identificadas como tais. |
| Motion Sync, Angle Snapping, Ripple, LOD, Debounce e energia do rato | Não suportados para escrita na integração atual. Controlos identificam o software oficial; não são enviados relatórios HID não documentados. |
| Consumo durante FiveM | Não testado. O teste de processamento não é um benchmark de jogos ou prova de melhoria de FPS. |
| Windows 10 | O código tem alvo 19041, mas o MSIX de produção continua a exigir Windows 11. Não existe validação de instalação/execução desta versão em Windows 10. |
| Assinatura e arranque local da 1.0 | Pendentes. A publicação local foi bloqueada por App Control numa DLL sem assinatura. A certificação 0.10 é preservada até a substituição passar as verificações. |

Validação local mais recente: solução compilada sem erros/avisos; 52 testes de Mouse Lab, sessões e diário passaram (34 específicos do Mouse Lab/sessões). A regressão local completa encontrou bloqueio de `Nexus.Network.dll` pelo Windows; usar CI para separar políticas locais de falhas de código. Uma falha de concorrência nos testes SQLite foi corrigida eliminando limpeza global de pools em favor de limpeza apenas das bases da fixture.

A primeira execução completa de CI [39308254530](https://github.com/joao01dinis-ctrl/NEXUS-Optimizer/actions/runs/39308254530), revisão `2d87e9f`, passou build, testes, quatro testes nativos, 25 renderizações e atualização real 0.10→1.0 com migração aditiva de dados e reinstalação sob identidade de teste. Isto não valida automaticamente revisões posteriores, assinatura de produção ou o rato físico. Os relatórios do candidato final devem ser verificados separadamente.

## Fontes de integração

- [Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input)
- [Registo e remoção de Raw Input](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerrawinputdevices)
- [RAWMOUSE](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)
- [Verificação do registo Raw Input](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getregisteredrawinputdevices)
- [SystemParametersInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow)

Backups da 0.10: `outputs/NexusBackups/0.10-20261019` no workspace, incluindo histórico Git completo e manifesto com hashes dos pacotes preservados.
