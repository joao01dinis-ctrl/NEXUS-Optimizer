# Inventário do vídeo de referência

Fonte fornecida pelo utilizador: `C:/Users/joao0/AppData/Local/Temp/codex-file-preview-qZ2r71/20261007-1325-27.0116588.mp4`.

Análise em 7 de outubro de 2026. O vídeo tem cerca de 96 segundos e mostra a interface do TL Optimizer, versão visível **1.2.38**, com uma conta no plano Free. Os quadros analisados estão em `work/video-frames`, extraídos de quatro em quatro segundos. Os nomes abaixo descrevem os controlos que se conseguem ler; não são uma confirmação de que funcionam, nem uma especificação dos ajustes internos do concorrente.

O pedido é criar funções equivalentes no NEXUS com código e apresentação próprios, gratuitamente. A presença de um interruptor ativado no vídeo não prova que a mudança tenha sido aplicada ao Windows. O vídeo não inclui ensaios antes/depois, comandos executados ou documentação técnica do motor.

## Navegação e informação geral

| Função visível | Evidência | Limite da observação |
|---|---|---|
| Início, Ajuda, PC Boost, Game Mode, App Tuner, Latência, Memória, Rede, Tweaks, Windows | Todos os quadros | PC Boost, Game Mode, App Tuner e Latência têm indicação Premium. |
| BIOS | Todos os quadros | Aparece como **Em breve**; não constitui uma função disponível. |
| Pesquisa de comandos, páginas e ações | Barra superior | Não foi demonstrado o resultado de uma pesquisa. |
| Gráfico de utilização da CPU | 0008 | Valor e gráfico visíveis. |
| Gráfico de utilização da RAM e memória total | 0016 | Valor, gráfico e total visíveis. |
| Monitorização de GPU | 0012 | A interface informa **Placa de vídeo não detectada** e utilização indisponível. |
| Estado geral e tempo desde o arranque | 0008, 0016 | Estado e uptime visíveis. |
| Recomendações e opção de reaplicar um ajuste que voltou ao padrão | 0008 | Recomendação relativa à energia PCIe; reaplicação não demonstrada. |
| Histórico de otimizações por memória, rede e limpeza | 0000 | Contagem e entradas anteriores visíveis; os ganhos indicados não foram validados. |
| Ajuda, tutorial em vídeo e comunidade | 0008 | Ligações visíveis. |
| Análise gratuita PC Boost e aplicação do plano Premium | 0008, 0012 | Anunciadas na interface. O plano interno e a execução não aparecem. |

## Game Mode

Quadros 0024, 0028 e 0032. A interface anuncia deteção automática de jogos, otimização durante a sessão e reversão ao fechar. A automação está **Ociosa / pausada**, não há jogo detetado e não há sessão concluída no vídeo.

| Função / escolha visível | Observação |
|---|---|
| Perfis Balanceado, Competitivo, Extremo, Streaming e Personalizado | Os cinco nomes estão legíveis. O conteúdo de cada perfil não foi mostrado. |
| Motor adaptativo | Marcado Novo; não há descrição técnica ou comportamento demonstrado. |
| Aumentar prioridade do processo do jogo | Opção legível; nível de prioridade interno desconhecido. |
| TL Optimizer Performance | Opção de energia; seletor **Restaurar anterior** visível. |
| Otimização específica da GPU | Não identifica driver, ajuste ou placa compatível. |
| Reduzir latência de entrada | Não especifica o mecanismo. |
| Mostrar avançadas (11) | A lista não foi expandida; os onze nomes são desconhecidos. |
| Resultado da última sessão | Nenhuma sessão concluída. |
| Biblioteca de jogos | GTA V, Minecraft, Fortnite, Counter-Strike 2, VALORANT e League of Legends; botão para biblioteca completa. |

O quadro 0024 também mostra um erro do PC Boost: falha ao invocar `engine:getHardwareProfile`, com `spawn UNKNOWN`. Não há evidência de uma análise funcional concluída do PC Boost nesta gravação.

## App Tuner e Latência

| Página | Evidência | Observação |
|---|---|---|
| App Tuner | 0036 | Anuncia otimização de programas com backup/reversão, mas mostra zero programas detetados e erro `app-tuner:list` / `spawn UNKNOWN`. Não revela uma lista de programas ou ajustes específicos. |
| Latência | 0040 | Mostra **Engine indisponível** e indicação para reiniciar como administrador. Não mostra medições, controlos ou ajustes disponíveis. |

Estas páginas fornecem intenções de produto, não uma lista completa de operações reproduzíveis.

## Memória e limpeza de armazenamento

Quadros 0044 e 0048.

| Função visível | Observação |
|---|---|
| Uso de RAM, percentagem, memória em cache, standby e livre | Indicadores de estado e pressão de memória. |
| Liberar memória | Botão geral; a frase de segurança da interface não comprova ausência de impacto. |
| Reduzir memória em uso | Identificado como redução do working set dos processos. |
| Limpar memória em cache | Descrição refere libertação de páginas em cache. Não revela a API utilizada. |
| Descarregar páginas modificadas | Operação sobre páginas de memória modificadas; detalhes técnicos não mostrados. |
| Quem mais consome RAM | Lista de processos, consumo e classificação. |
| Arquivos temporários | Botão individual de limpeza. |
| Cache de shaders DirectX | Botão individual de limpeza. |
| Lixeira | Botão individual de limpeza. |
| Cache de launchers | Não identifica os launchers nem pastas abrangidas. |
| Limpar tudo | Indicação de whitelist; os caminhos e regras não aparecem. |

Reduzir working sets, apagar cache ou eliminar shaders não pode ser apresentado no NEXUS como aumento garantido de desempenho. A gravação não comprova um ganho. Limpeza de armazenamento e mudanças reversíveis são operações diferentes: apagar ficheiros pode não permitir undo e requer seleção e revisão dos dados abrangidos.

## Rede

Quadros 0052 e 0056. A página mostra interface ativa, IPv4, gateway, DNS atual e estado DHCP.

| Função visível | Observação |
|---|---|
| Analisar DNS | Ranking com tempo em milissegundos. Não mostra domínio de teste, amostras ou algoritmo. |
| Escolher servidor DNS | Botão por fornecedor. Cloudflare aparece como aplicado. |
| Restaurar automático | A interface descreve regresso ao DNS automático/DHCP. Não foi demonstrado. |
| Limpar DNS após aplicar | Opção ativada no vídeo. |
| Renovar IP após aplicar | Opção opcional, desligada; a interface avisa que pode interromper a ligação brevemente. |

Fornecedores legíveis no ranking: **Cloudflare, Quad9, Google, Control D, OpenDNS, NextDNS, Verisign, Neustar, AdGuard e Comodo Secure**. O Comodo aparece com erro de benchmark. Os números do vídeo são resultados dessa interface naquele instante, não resultados do NEXUS nem uma recomendação universal.

## Tweaks

Quadros 0060, 0064 e 0068. Estes controlos são legíveis; as descrições não expõem os valores, serviços ou APIs exatos utilizados.

| Nome visível | Quadro | Consideração para a implementação |
|---|---|---|
| Reduzir atraso do teclado | 0060 | Distinguir repetição de teclas de latência real de um jogo. |
| Desativar telemetria de entrada do Windows | 0060 | Precisar de política e compatibilidade documentadas. |
| Otimizar estabilidade do polling USB | 0060 | A descrição refere energia USB; não demonstra alteração da taxa de polling. |
| Otimizar comportamento de paginação | 0060 | Ajuste interno desconhecido; não desativar ou dimensionar cegamente o ficheiro de paginação. |
| Desativar SysMain / SuperFetch | 0060 | Pode afetar comportamento de arranque/cache; exige contexto e reversão. |
| Desativar otimizações de tela cheia | 0060 | Pode prejudicar jogos; deve ser avaliado por aplicação. |
| Desativar gravação em segundo plano da Game Bar | 0060 | Separar captura de vídeo das funções gerais de Game Bar. |
| Desativar economia de energia da rede | 0064 | Depende do adaptador e pode aumentar consumo. |
| Desativar serviços Xbox | 0064 | Pode afetar jogos, Game Pass e funcionalidades Xbox. |
| Desativar serviços de telemetria | 0064 | Serviços exatos desconhecidos. |
| Desativar indexação do Windows Search | 0064 | Pode degradar pesquisa. |
| Desativar dicas do Windows | 0064 | Personalização, não ganho de FPS comprovado. |
| Reduzir serviços de inicialização | 0064 | A lista de serviços não é revelada. |
| Plano de desempenho TL Optimizer | 0068 | Descrição anuncia boost de CPU e ausência de core parking; configuração completa não mostrada. |
| Desativar economia de energia do PCIe | 0068 | Pode aumentar consumo, calor e autonomia reduzida. |
| Desativar atualizações de último acesso do NTFS | 0068 | Necessita compatibilidade com as aplicações e preservação do valor original. |
| Desativar apps em segundo plano | 0068 | Pode afetar sincronização e notificações. |
| Reduzir notificações do Windows | 0068 | Personalização/foco; não ganho de FPS comprovado. |

Há várias caixas Premium desfocadas nestes quadros. Não é possível determinar os nomes ou especificações. O cartão do Início menciona **12 ajustes trancados** (0012/0016); isso é uma contagem anunciada, não uma lista identificada. Não adivinhar nem anunciar paridade com esses ajustes.

## Windows: recursos e serviços

Os quadros 0072, 0076 e 0080 mostram os contadores **Sistema 25**, **Aplicativos 5**, **Privacidade 10**, **Unidades de disco 2**, **Windows Update 4** e **Toque 3**. A vista mostra 7 opções ativas, 40 disponíveis e 9 avançadas. Estes são números do concorrente, não do NEXUS.

### Sistema

| Opção legível | Quadro |
|---|---|
| Mostrar todos os ícones de notificação | 0072 |
| Desativar Serviço de Relatório de Erros | 0072, 0080 |
| Desativar o Assistente de Compatibilidade | 0072, 0080 |
| Desabilitar Serviço de Impressão | 0072, 0080 |
| Desabilitar Serviço de Fax | 0072, 0080 |
| Otimizações para jogos em janela | 0072, 0080 |
| Desativar transparência da interface | 0072, 0080 |
| Desativar reabertura automática de apps | 0080 |
| Desabilitar Teclas de Aderência | 0080 |
| **Desabilitar o SmartScreen** | 0080 |
| Usar UTC no relógio de hardware | 0080 |
| Desativar Histórico de Acesso Rápido | 0080 |
| Habilitar Caminhos Longos | 0076 |
| Desabilitar Serviços de Sensores | 0076 |
| Remover a Transmissão de Miracast | 0076 |
| Habilitar Mixer de Volume Clássico | 0076 |
| Desativar Modo de Espera Moderno | 0076 |
| Desativar o Copilot do Windows | 0076 |
| Alinhar barra de tarefas à esquerda | 0076 |
| Ocultar botão Chat / Teams | 0076 |
| Menu de contexto clássico | 0076 |
| Desativar sugestões de Snap Layout | 0076 |
| Desativar anúncios do Explorer | 0076 |
| Desativar “Recomendado” do Iniciar | 0076 |
| Ocultar pesquisa na barra de tarefas | extra-74 |

25 nomes Sistema identificados após acrescentar o quadro extra-74. Desativar serviços de impressão, sensores, Miracast ou espera moderna pode retirar funções do PC. Várias opções dependem da edição e versão do Windows e não são otimizações de desempenho.

### Aplicativos

Quadros 0072 e 0080: desabilitar/desativar telemetria do **Office, Firefox, Chrome, NVIDIA e Visual Studio**. Os cinco nomes são legíveis; as políticas, produtos instalados e versões compatíveis não foram mostrados.

### Privacidade

| Opção legível | Quadro |
|---|---|
| Desabilitar tarefas agendadas de Telemetria | 0072, 0080 |
| Desabilitar compartilhamento de mídia | 0080 |
| Desabilitar Grupo Home | 0080 |
| Desativar protocolo SMBv1 | 0080 |
| Desabilitar a Assistência da Cortana | 0080 |
| Desativar telemetria de borda | 0080 |
| Desativar descoberta de borda | 0080 |
| Desativar experiências personalizadas | 0076 |
| Desativar dicas e Spotlight da tela de bloqueio | 0076 |
| Desativar ID de publicidade | extra-78 |

Os dois nomes “de borda” são a tradução exibida para funções do Microsoft Edge. Dez nomes identificados após acrescentar o quadro extra-78. O próprio vídeo assinala Grupo Home como obsoleto. Desativar SMBv1 é um ajuste de segurança; não equivale a desativar as proteções do Windows.

### Discos e Windows Update

| Opção legível | Quadro | Consideração |
|---|---|---|
| **Desabilitar Recuperação do Sistema** | 0076 | Remove a possibilidade de recuperação por pontos de restauro. |
| Desativar hibernação | 0076 | Liberta espaço; afeta hibernação e pode afetar arranque rápido. |
| **Desativar Atualizações Automáticas** | 0076 | Pode impedir receção automática de correções de segurança. |
| Desativar atualizações da Microsoft Store | 0076 | Pode impedir atualizações de aplicações. |
| Desabilitar Windows Insider Service | 0076 | Não identifica participação em Insider ou estado necessário. |
| Excluir Drivers de Atualizações | 0076 | Requer um plano de atualização de drivers e compatibilidade. |

### Toque

A categoria e a contagem **3** estão visíveis em 0076; os nomes individuais ficam fora do enquadramento dos quadros analisados.

## Windows: aplicações nativas

Quadro 0084: grelha com aplicações, estados **Removido**, interação para reinstalar e botões **Reinstalar tudo / Remover todos**. Não há uma remoção ou reinstalação demonstrada.

Aplicações com nomes legíveis: **3D Builder, Alarms & Clock, Calculator, Camera, Get Office, Skype, Get Started / Tips, Groove Music, Mail and Calendar, Maps, Movies & TV, Novidades, OneNote, People, Photos, Solitaire, Weather, Voice Recorder, Xbox, Feedback Hub, Phone Link, Xbox Game Bar, Copilot, Clipchamp, Dev Home, Family, Power Automate e Outlook (novo)**.

A grelha continua abaixo da imagem, portanto esta lista não é completa. Os nomes no vídeo incluem aplicações que podem já não existir em determinadas instalações atuais. Uma implementação segura deve detetar os pacotes existentes, excluir componentes essenciais e informar que a reinstalação depende da disponibilidade dos pacotes/loja.

## Auto Limpeza

Quadros 0088 e 0092. A página está bloqueada no plano Free e anuncia execução sem perturbar o jogo; essa condição não foi demonstrada.

| Escolha visível | Observação |
|---|---|
| Desligado, 15 min, 30 min, 60 min | Intervalos de execução. |
| Executar agora | Botão manual. |
| Reduzir memória em uso | Selecionado no vídeo. |
| Limpar DNS | Não selecionado no vídeo. |
| `%TEMP%` | Selecionado no vídeo. |
| `Temp` | Selecionado; a raiz exata não é mostrada. |
| `Recent` | Não selecionado. |
| `Prefetch` | Não selecionado. |

Não tratar a limpeza recorrente de Prefetch, shaders ou working sets como melhoria universal. A implementação deve explicar finalidade, limites e efeitos, rever a seleção e impedir eliminação fora dos caminhos autorizados.

## Restrições herdadas do pedido do NEXUS

O pedido original determina **não aplicar tweaks agressivos nem desativar segurança do Windows**. As opções visíveis **desativar SmartScreen**, **desativar atualizações automáticas** e **desabilitar Recuperação do Sistema** entram em conflito com esse requisito ou com o mecanismo de recuperação pedido. Não devem ser ativadas automaticamente nem incluídas no conjunto recomendado como otimização.

O vídeo fornece uma lista substancialmente maior que o site, mas ainda deixa desconhecidos: ajustes Premium desfocados, onze opções avançadas de Game Mode, especificações dos motores PC Boost/App Tuner/Latência, três opções Toque e a parte inferior da grelha de aplicações. A paridade completa não está demonstrada e não pode ser anunciada a partir desta evidência.

Este inventário é a base para implementar e validar cada função separadamente. A funcionalidade efetivamente entregue deve ser verificada no código e nos relatórios do NEXUS, com estado explícito de implementação, compatibilidade e reversão.
