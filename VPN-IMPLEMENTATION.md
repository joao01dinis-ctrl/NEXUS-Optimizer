# Nexos VPN — integração WARP e arquitetura de fornecedores

Plano de implementação de 16/10/2026, no projeto existente C# 14 / .NET 10 / WinUI 3 / Windows App SDK. Mantêm-se os módulos, SQLite, MVVM, logs e funções do otimizador. A decisão anterior de não contratar servidores mantém-se; o novo pedido permite uma integração gratuita com o cliente oficial Cloudflare WARP.

## Etapas e critérios

1. **Modelo e segurança:** contrato de fornecedor em Nexus.Network, deteção apenas nos caminhos oficiais Windows, validação Authenticode do editor Cloudflare antes de executar, comandos fixos e parsers conservadores. Testar estados desconhecidos, respostas incompletas, DNS sem túnel, configuração de organização, consentimento e concorrência antes da UI.
2. **Página e instalação assistida:** página Nexos VPN em preto e branco, estado local, configuração guiada, termos/privacidade e abertura da página oficial Windows. O utilizador descarrega, instala e aceita os termos no cliente oficial; a NEXUS não redistribui o WARP, aceita termos ou inscreve dispositivos. A ausência de CLI compatível mantém disponível a aplicação oficial e bloqueia controlos diretos.
3. **Ligação e recuperação:** ligar/desligar só após confirmação individual, sem mudar modo, DNS, protocolo, registo ou configurações. Consultar de novo antes/depois; um comando aceite não significa túnel estabelecido. Timeout/cancelamento não mata o serviço, repõe DNS ou desliga uma ligação anterior. Disponibilizar consulta e aplicação oficial para recuperação.
4. **Medições autorizadas:** teste HTTPS ao endpoint oficial de verificação e ICMP a 1.1.1.1, com limites, cancelamento e amostras reais. Acompanhar durante um minuto só com consentimento, suspender ao sair da página. Não guardar IP público, saída bruta ou navegação. Uma resposta warp=on confirma apenas o percurso desse pedido; não comprova ausência de fugas nem cobertura de todas as aplicações.
5. **Entrega:** testes de lógica/erros, build, renderização nativa, arranque no PC e relato separado da ligação real. Instalação e ligação neste PC exigem consentimento; testes com substitutos não serão apresentados como túnel real validado.

## Fornecedores

`IVpnProvider` separa estado, capacidades e pedidos de ligação. `CloudflareWarpProvider` usa exclusivamente o cliente oficial instalado. Um fornecedor WireGuard futuro permanece explicitamente indisponível até existirem servidores, configuração autenticada, componentes assinados, orçamento e testes de DNS/IPv6/recuperação. Não instala serviço ou driver próprio nesta versão.

## Interfaces e condições verificadas

- [Windows consumidor](https://developers.cloudflare.com/warp-client/get-started/windows/): caminhos oficiais e instalação interativa.
- [CLI desktop suportada](https://developers.cloudflare.com/cloudflare-one/team-and-resources/devices/cloudflare-one-client/troubleshooting/troubleshooting-guide/): `warp-cli`, consultas `status` e `settings`. A ajuda da versão instalada confirma os comandos antes de os habilitar.
- [Modos](https://developers.cloudflare.com/warp-client/warp-modes/): distinguir túnel, só DNS e proxy; não prometer anonimato, seleção de país ou ganhos em jogos.
- [Verificação oficial](https://developers.cloudflare.com/warp-client/get-started/linux/): endpoint HTTPS `https://www.cloudflare.com/cdn-cgi/trace`, campo `warp`.
- [Termos da aplicação](https://www.cloudflare.com/application/terms/) e [privacidade](https://www.cloudflare.com/application/privacypolicy/): uso pelo titular, sem modificar/reempacotar o cliente ou retirar atribuição. A NEXUS é independente, não uma parceria aprovada pela Cloudflare. A revisão destas páginas não constitui autorização especial de integração ou redistribuição.

Não são usados endpoints privados de registo, extração de chaves WARP, `--accept-tos`, APIs de revenda, WARP+ pago, ajustes automáticos de jogos, captura de pacotes, kill switch ou servidores simulados.

## Evidência

- Etapa 1: 32 testes de modelos, parsers, consentimento e concorrência passaram.
- Etapa 2: 47 testes específicos da VPN passaram, incluindo verificação WinTrust real de um executável oficial .NET, sem o executar, respostas desconhecidas, DNS/proxy, medições expiradas, cancelamento e limite de amostras.
- Compilação completa sem erros/avisos. Suite local final: **272 passaram, 5 ignorados, 0 falhas**, de 277. Os testes nativos que alteram preferências/Lixeira não foram ativados neste PC. Nenhum teste instalou, registou ou ligou WARP; as operações de ligação são testadas com substitutos, não com um túnel real.
- Os ficheiros da publicação WinUI estão presentes, incluindo App.xbf, MainWindow.xbf e Nexus.UI.pri. A compilação local com WARP foi bloqueada por App Control em Nexus.Restore.dll, 0x800711C7. A política não foi alterada. A publicação CI e as imagens nativas são a próxima validação.
- O arranque da compilação CI 0.10 anterior a esta integração foi confirmado neste portátil em 16/10/2026, com Smart App Control=1, estado do Defender consultado e zero pedidos de análise/alterações Windows. Isso não valida antecipadamente esta revisão nem uma ligação VPN.
- WARP não foi encontrado nos caminhos oficiais deste PC. Instalação, aceitação de termos e ligação real continuam pendentes de ação/consentimento do utilizador.
