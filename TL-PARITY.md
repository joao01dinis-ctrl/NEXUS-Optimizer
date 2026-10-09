# Cobertura TL Optimizer → NEXUS 0.6

Referência: vídeo do utilizador em VIDEO-FEATURES.md e site/guia https://tloptimizer.com/ consultados em 9/10/2026. A NEXUS tem código, design e perfis próprios, gratuitos. **Não tem paridade integral comprovada.**

140 entradas rastreáveis: 138 da referência e duas ferramentas próprias. Isto não significa 140 executores. Catálogo embebido em Core, disponível na Ajuda.

A 0.6 aproxima a utilização: PC Boost com análise/seleção/aplicação, 14 cartões de preferências, sete políticas e Teclas de Aderência, Game Mode automático opt-in, App Tuner e restauro global. O automático TL também anuncia gestão de apps de fundo; a NEXUS não congela, fecha ou reduz prioridades de apps de fundo. As opções Premium desfocadas, onze avançadas e três Toque não estão identificadas. Benchmark de jogos, FPS ao vivo/overlay, latência de entrada, motor adaptativo e políticas específicas de produtos continuam ausentes.

Build/testes e efeito real de uma preferência constam de VALIDATION.md. A nova interface ainda precisa de arranque e validação visual; um catálogo ou teste de lógica não comprova todas as ações reais.

## Resumo

| Estado | Entradas |
|---|---:|
| Consulta e remoção individual; reinstalação via Store | 28 |
| Controlo oficial do Windows | 12 |
| Excluída para preservar segurança/recuperação | 4 |
| Implementação própria com limites | 32 |
| Implementação própria limitada | 1 |
| Não identificadas | 3 |
| Não identificados | 1 |
| Políticas Spotlight com compatibilidade limitada | 1 |
| Por implementar / especificação por confirmar | 57 |
| Por implementar: diagnóstico do Edge | 1 |

## Catálogo rastreável

| Área | Referência | Estado | Área da NEXUS / limite |
|---|---|---|---|
| Navegação e informação geral | Início, Ajuda, PC Boost, Game Mode, App Tuner, Latência, Memória, Rede, Tweaks, Windows | Implementação própria com limites | overview · Navegação por áreas, cartões de ajuste direto, pesquisa e restauro global. Apresentação e código próprios. |
| Navegação e informação geral | BIOS | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Navegação e informação geral | Pesquisa de comandos, páginas e ações | Implementação própria com limites | overview · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Navegação e informação geral | Gráfico de utilização da CPU | Implementação própria com limites | overview · Gráficos de amostras CPU/RAM, tempo desde o arranque e verificação de valores aplicados. Reaplicar exige revisão; não mede ganhos de FPS. |
| Navegação e informação geral | Gráfico de utilização da RAM e memória total | Implementação própria com limites | overview · Gráficos de amostras CPU/RAM, tempo desde o arranque e verificação de valores aplicados. Reaplicar exige revisão; não mede ganhos de FPS. |
| Navegação e informação geral | Monitorização de GPU | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Navegação e informação geral | Estado geral e tempo desde o arranque | Implementação própria com limites | overview · Gráficos de amostras CPU/RAM, tempo desde o arranque e verificação de valores aplicados. Reaplicar exige revisão; não mede ganhos de FPS. |
| Navegação e informação geral | Recomendações e opção de reaplicar um ajuste que voltou ao padrão | Implementação própria com limites | overview · Gráficos de amostras CPU/RAM, tempo desde o arranque e verificação de valores aplicados. Reaplicar exige revisão; não mede ganhos de FPS. |
| Navegação e informação geral | Histórico de otimizações por memória, rede e limpeza | Implementação própria com limites | history · Eventos locais SQLite, alterações e recuperação. Uma ação sem undo não recebe um restauro inventado. |
| Navegação e informação geral | Ajuda, tutorial em vídeo e comunidade | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Navegação e informação geral | Análise gratuita PC Boost e aplicação do plano Premium | Implementação própria com limites | optimize · Análise de CPU/RAM e preferências existentes; perfis próprios Normal/Gaming/Trabalho, seleção de diferenças e aplicação reversível em conjunto. Gratuito; não é o motor Premium do TL. |
| Game Mode | Perfis Balanceado, Competitivo, Extremo, Streaming e Personalizado | Implementação própria com limites | gaming · Perfis próprios, manuais ou automáticos: prioridade Normal/AboveNormal, animações e plano existente opcionais. Sem congelar/fechar apps de fundo; sem ganho medido. |
| Game Mode | Motor adaptativo | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Game Mode | Aumentar prioridade do processo do jogo | Implementação própria com limites | gaming · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Game Mode | TL Optimizer Performance | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Game Mode | Otimização específica da GPU | Controlo oficial do Windows | gaming · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Game Mode | Reduzir latência de entrada | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Game Mode | Mostrar avançadas (11) | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Game Mode | Resultado da última sessão | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Game Mode | Biblioteca de jogos | Implementação própria com limites | gaming · Executáveis escolhidos ou adicionados de processos abertos; deteção por caminho/PID/arranque. Automático opt-in com autorização fixa nesta abertura e reposição ao terminar; sem importar todos os launchers. |
| App Tuner e Latência | App Tuner | Implementação própria com limites | tuner · Seleciona uma aplicação aberta elegível e aplica prioridade AboveNormal com undo. Não modifica configurações internas específicas de cada produto. |
| App Tuner e Latência | Latência | Implementação própria com limites | latency · Mede cinco respostas ICMP ao IP escolhido. Não mede nem promete reduzir latência de entrada. |
| Memória e limpeza de armazenamento | Uso de RAM, percentagem, memória em cache, standby e livre | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Memória e limpeza de armazenamento | Liberar memória | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Memória e limpeza de armazenamento | Reduzir memória em uso | Implementação própria com limites | memory · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Memória e limpeza de armazenamento | Limpar memória em cache | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Memória e limpeza de armazenamento | Descarregar páginas modificadas | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Memória e limpeza de armazenamento | Quem mais consome RAM | Implementação própria com limites | memory · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Memória e limpeza de armazenamento | Arquivos temporários | Implementação própria com limites | tools · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Memória e limpeza de armazenamento | Cache de shaders DirectX | Implementação própria com limites | tools · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Memória e limpeza de armazenamento | Lixeira | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Memória e limpeza de armazenamento | Cache de launchers | Implementação própria com limites | tools · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Memória e limpeza de armazenamento | Limpar tudo | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Rede | Analisar DNS | Implementação própria com limites | network · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Rede | Escolher servidor DNS | Implementação própria com limites | network · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Rede | Restaurar automático | Implementação própria com limites | network · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Rede | Limpar DNS após aplicar | Implementação própria com limites | network · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Rede | Renovar IP após aplicar | Implementação própria com limites | network · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Tweaks | Reduzir atraso do teclado | Implementação própria com limites | tweaks · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Tweaks | Desativar telemetria de entrada do Windows | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Otimizar estabilidade do polling USB | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Otimizar comportamento de paginação | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Desativar SysMain / SuperFetch | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Desativar otimizações de tela cheia | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Desativar gravação em segundo plano da Game Bar | Controlo oficial do Windows | gaming · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Tweaks | Desativar economia de energia da rede | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Desativar serviços Xbox | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Desativar serviços de telemetria | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Desativar indexação do Windows Search | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Desativar dicas do Windows | Controlo oficial do Windows | tools · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Tweaks | Reduzir serviços de inicialização | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Plano de desempenho TL Optimizer | Implementação própria com limites | tweaks · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Tweaks | Desativar economia de energia do PCIe | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Desativar atualizações de último acesso do NTFS | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Desativar apps em segundo plano | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Tweaks | Reduzir notificações do Windows | Controlo oficial do Windows | tools · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Sistema | Mostrar todos os ícones de notificação | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Desativar Serviço de Relatório de Erros | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Desativar o Assistente de Compatibilidade | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Desabilitar Serviço de Impressão | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Desabilitar Serviço de Fax | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Otimizações para jogos em janela | Controlo oficial do Windows | gaming · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Sistema | Desativar transparência da interface | Controlo oficial do Windows | tools · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Sistema | Desativar reabertura automática de apps | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Desabilitar Teclas de Aderência | Implementação própria com limites | preferences · API STICKYKEYS: desliga função, atalho Shift e confirmação; guarda e repõe a configuração anterior, preservando os outros flags. |
| Sistema | Desabilitar o SmartScreen | Excluída para preservar segurança/recuperação | Conflita com o pedido original. Não existe botão para desativar esta proteção. |
| Sistema | Usar UTC no relógio de hardware | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Desativar Histórico de Acesso Rápido | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Habilitar Caminhos Longos | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Desabilitar Serviços de Sensores | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Remover a Transmissão de Miracast | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Habilitar Mixer de Volume Clássico | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Desativar Modo de Espera Moderno | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Desativar o Copilot do Windows | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Alinhar barra de tarefas à esquerda | Controlo oficial do Windows | tools · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Sistema | Ocultar botão Chat / Teams | Controlo oficial do Windows | tools · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Sistema | Menu de contexto clássico | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Desativar sugestões de Snap Layout | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Sistema | Desativar anúncios do Explorer | Controlo oficial do Windows | tools · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Sistema | Desativar “Recomendado” do Iniciar | Controlo oficial do Windows | tools · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Sistema | Ocultar pesquisa na barra de tarefas | Controlo oficial do Windows | tools · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Privacidade | Desabilitar tarefas agendadas de Telemetria | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Privacidade | Desabilitar compartilhamento de mídia | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Privacidade | Desabilitar Grupo Home | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Privacidade | Desativar protocolo SMBv1 | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Privacidade | Desabilitar a Assistência da Cortana | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Privacidade | Desativar telemetria de borda | Por implementar: diagnóstico do Edge | preferences · A preferência implementada limita personalização, não todo o diagnóstico/telemetria. A política antiga MetricsReportingEnabled é obsoleta e não é usada. |
| Privacidade | Desativar descoberta de borda | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Privacidade | Desativar experiências personalizadas | Implementação própria com limites | preferences · Revê a descrição e confirmação na área. O comportamento interno do TL não é conhecido. |
| Privacidade | Desativar dicas e Spotlight da tela de bloqueio | Políticas Spotlight com compatibilidade limitada | preferences · Opções de Spotlight estão implementadas para Enterprise/Education. Noutras edições usa as Definições; não se apresenta a gravação de política como prova de efeito. |
| Privacidade | Desativar ID de publicidade | Controlo oficial do Windows | preferences · A alteração é feita nas Definições; não entra no undo da NEXUS. |
| Discos e Windows Update | Desabilitar Recuperação do Sistema | Excluída para preservar segurança/recuperação | Conflita com o pedido original. Não existe botão para desativar esta proteção. |
| Discos e Windows Update | Desativar hibernação | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Discos e Windows Update | Desativar Atualizações Automáticas | Excluída para preservar segurança/recuperação | Conflita com o pedido original. Não existe botão para desativar esta proteção. |
| Discos e Windows Update | Desativar atualizações da Microsoft Store | Excluída para preservar segurança/recuperação | Conflita com o pedido original. Não existe botão para desativar esta proteção. |
| Discos e Windows Update | Desabilitar Windows Insider Service | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Discos e Windows Update | Excluir Drivers de Atualizações | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Auto Limpeza | Escolha visível | Implementação própria com limites | autoclean · Área própria com intervalo, estado e execução manual. Apenas temporários locais antigos dentro dos limites documentados. |
| Auto Limpeza | Desligado, 15 min, 30 min, 60 min | Implementação própria com limites | autoclean · Desligada em cada arranque; ativação revista por intervalo. Suspende durante jogos/sessões identificados. |
| Auto Limpeza | Executar agora | Implementação própria com limites | autoclean · Executa a limpeza limitada do Temp do utilizador após confirmação. Não esvazia a Lixeira ou limpa RAM. |
| Auto Limpeza | Limpar DNS | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Auto Limpeza | %TEMP% | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Auto Limpeza | Temp | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Auto Limpeza | Recent | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Auto Limpeza | Prefetch | Por implementar / especificação por confirmar | O vídeo não revela APIs, valores, condições ou execução suficientes. Não é anunciada como pronta. |
| Aplicações nativas | 3D Builder | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Alarms & Clock | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Calculator | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Camera | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Get Office | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Skype | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Get Started / Tips | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Groove Music | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Mail and Calendar | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Maps | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Movies & TV | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | OneNote | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | People | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Photos | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Solitaire | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Weather | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Voice Recorder | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Xbox | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Feedback Hub | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Phone Link | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Xbox Game Bar | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Copilot | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Clipchamp | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Dev Home | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Family | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Power Automate | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Aplicações nativas | Outlook (novo) | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Auto Limpeza | Auto Limpeza: 15 / 30 / 60 minutos | Implementação própria limitada | autoclean · Só TEMP do utilizador com 30 dias; máximo 100 ficheiros por execução, enviados à Lixeira. Desligada em cada arranque. Sem working-set purge ou Prefetch automático. |
| Especificação em falta | Ajustes Premium desfocados | Não identificados | Precisam de nomes e comportamento para poderem ser comparados. Não serão inventados. |
| Especificação em falta | Onze opções avançadas de Game Mode | Não identificadas | O vídeo não expande a lista. |
| Especificação em falta | Três opções Toque | Não identificadas | Os nomes não aparecem no vídeo. |
| Especificação em falta | Aplicações abaixo do enquadramento | Não identificadas | A grelha continua abaixo do vídeo. |
| Aplicações nativas | Novidades | Consulta e remoção individual; reinstalação via Store | apps · Só pacote opcional elegível, para o utilizador atual. A Store pode não ter aplicações antigas; não recupera dados apagados. |
| Verificação própria NEXUS | Análise de capturas PresentMon e comparação declarada | Implementação própria com limites | benchmark · Importa CSV MsBetweenPresents escolhido, separa fluxos e preserva pausas. Não captura/gera FPS, certifica dados ou atribui ganho a um ajuste. A execução desta revisão está pendente de validação completa. |
| Verificação própria NEXUS | CPU por aplicação durante uma amostra curta | Implementação própria com limites | memory · Consulta passiva em duas amostras, com PID/arranque e capacidade total. Não fecha apps, altera CPU/GPU ou prova impacto nos FPS. |

## Como continuar

As pendências são explícitas e não têm botões que fingem executá-las. Para uma função nova: confirmar nome/comportamento e compatibilidade numa fonte primária, implementar um executor próprio com validação dos alvos, captar/restaurar estado quando reversível, usar confirmação proporcional, verificar falhas/concorrência e testar o efeito num Windows de teste. Atualizar o catálogo apenas depois de código e validação corresponderem à descrição.

SmartScreen, atualizações automáticas, recuperação, drivers/paginação e políticas essenciais são preservados pelo pedido original. Remoções em massa, purgas de memória e desativação automática de serviços/poupança energética não são tratadas como ganhos universais. A BIOS “em breve” do vídeo não identifica uma função disponível. Opções escondidas precisam de especificação do utilizador; não são inventadas.

## Fontes técnicas primárias (8/10/2026)

- Políticas e edições Windows: https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-experience
- Personalização Edge, distinta de todo o diagnóstico: https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/PersonalizationReportingEnabled
- A antiga MetricsReportingEnabled do Edge é obsoleta: https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/MetricsReportingEnabled
- Configuração DNS por adaptador, automático sem parâmetros e códigos de resultado: https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/setdnsserversearchorder-method-in-class-win32-networkadapterconfiguration
- Working sets e direitos de processo: https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-emptyworkingset
- Remoção de pacote para o utilizador atual e perda de acesso/dados: https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.packagemanager.removepackageasync
- Store e Definições: https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-store-app e https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings

Não foi integrado/distribuído PresentMon nesta revisão. É uma possibilidade futura para medições reais, não uma função já implementada: https://github.com/GameTechDev/PresentMon
