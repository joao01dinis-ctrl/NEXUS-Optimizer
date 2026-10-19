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

## Estado inicial

Plano definido; implementação e validação em curso. Os estados finais de cada funcionalidade serão registados após os testes efetivamente executados. Não cancelar a certificação 0.10 por existir apenas interface ou build.

## Fontes de integração

- [Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input)
- [Registo e remoção de Raw Input](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerrawinputdevices)
- [RAWMOUSE](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)
- [SystemParametersInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow)

Backups da 0.10: `outputs/NexusBackups/0.10-20261019` no workspace, incluindo histórico Git completo e manifesto com hashes dos pacotes preservados.
